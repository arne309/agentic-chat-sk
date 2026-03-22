using System.Diagnostics;
using System.Threading.Channels;
using AgentApp.Backend.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AgentApp.Backend.Services;

public class AgentService(Kernel kernel, ConversationStore store, ILogger<AgentService> logger)
{
    private readonly ChatCompletionAgent _agent = new()
    {
        Name = "FileAgent",
        Instructions = """
            You are a helpful assistant with access to a file system and a TypeScript/Deno runtime.
            You can list directories (ls), read files (read_file), write files (write_file),
            and execute TypeScript scripts (exec_script).

            Always think step by step. When using tools, use precise relative paths.
            After using tools, summarize what you found or did in clear language.
            """,
        Kernel = kernel,
        Arguments = new KernelArguments(new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        })
    };

    public async Task StreamResponseAsync(
        SendMessageRequest request,
        ChannelWriter<ServerMessage> channel,
        CancellationToken ct)
    {
        var conversation = store.GetOrCreate(request.ConversationId);
        var messageId = Guid.NewGuid().ToString();

        // Record user message
        conversation.Messages.Add(new Message
        {
            Role = MessageRole.User,
            Parts = [new TextPart { Content = request.Content }]
        });
        store.DeriveTitle(conversation);

        // Install tool call filter for this invocation
        var filter = new WsToolCallFilter(channel, request.ConversationId, messageId);
        kernel.FunctionInvocationFilters.Add(filter);

        try
        {
            await channel.WriteAsync(new AgentStartMessage(request.ConversationId, messageId), ct);

            var userMessage = new ChatMessageContent(AuthorRole.User, request.Content);

            await foreach (var chunk in _agent.InvokeStreamingAsync(
                userMessage, conversation.Thread, cancellationToken: ct))
            {
                var delta = chunk.Message.Content;
                if (!string.IsNullOrEmpty(delta))
                {
                    filter.AppendToken(delta);
                    await channel.WriteAsync(
                        new TokenMessage(request.ConversationId, messageId, delta), ct);
                }
            }

            var assistantMessage = new Message
            {
                Role = MessageRole.Assistant,
                Parts = filter.Parts
            };
            conversation.Messages.Add(assistantMessage);

            await channel.WriteAsync(
                new AgentDoneMessage(request.ConversationId, messageId), ct);
            await channel.WriteAsync(
                new ConversationUpdatedMessage(conversation.ToSummary()), ct);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Response cancelled for conversation {Id}", request.ConversationId);
            await channel.WriteAsync(
                new ErrorMessage(request.ConversationId, "cancelled", "Response cancelled"),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Agent error for conversation {Id}", request.ConversationId);
            await channel.WriteAsync(
                new ErrorMessage(request.ConversationId, "agent_error", ex.Message),
                CancellationToken.None);
        }
        finally
        {
            kernel.FunctionInvocationFilters.Remove(filter);
        }
    }
}

// ── Tool call filter: observes SK function calls and emits WS messages ───────

internal class WsToolCallFilter(
    ChannelWriter<ServerMessage> channel,
    string conversationId,
    string messageId) : IFunctionInvocationFilter
{
    public List<MessagePart> Parts { get; } = [];

    /// <summary>Appends a streaming token to the current text part, creating one if needed.</summary>
    public void AppendToken(string delta)
    {
        if (Parts.Count > 0 && Parts[^1] is TextPart textPart)
            textPart.Content += delta;
        else
            Parts.Add(new TextPart { Content = delta });
    }

    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        var args = context.Arguments
            .ToDictionary(k => k.Key, k => (object?)k.Value);

        var toolCallInfo = new ToolCallInfo
        {
            ToolName = context.Function.Name,
            Arguments = args
        };
        var toolCallPart = new ToolCallPart { ToolCall = toolCallInfo };
        Parts.Add(toolCallPart);

        await channel.WriteAsync(
            new ToolCallMessage(conversationId, messageId, context.Function.Name, args));

        var sw = Stopwatch.StartNew();
        await next(context);
        sw.Stop();

        var result = context.Result?.GetValue<object>()?.ToString() ?? "";
        toolCallInfo.Result = result;
        toolCallInfo.DurationMs = sw.ElapsedMilliseconds;

        if (context.Function.Name == "response_include")
        {
            var source = args.TryGetValue("path", out var p) ? p?.ToString() ?? "" : "";
            Parts.Add(new ContentBlockPart { Source = source, Content = result });
            await channel.WriteAsync(
                new ContentBlockMessage(conversationId, messageId, source, result));
            await channel.WriteAsync(
                new ToolResultMessage(conversationId, messageId,
                    context.Function.Name, $"Rendered {source}", sw.ElapsedMilliseconds));
        }
        else
        {
            await channel.WriteAsync(
                new ToolResultMessage(conversationId, messageId,
                    context.Function.Name, result, sw.ElapsedMilliseconds));
        }
    }
}
