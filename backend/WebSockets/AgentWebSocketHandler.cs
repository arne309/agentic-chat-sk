using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using AgentApp.Backend.Models;
using AgentApp.Backend.Services;

namespace AgentApp.Backend.WebSockets;

public static class AgentWebSocketHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task HandleAsync(HttpContext ctx)
    {
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        var agentService = ctx.RequestServices.GetRequiredService<AgentService>();

        // Thread-safe send queue — WebSocket.SendAsync must not be called concurrently
        var sendChannel = Channel.CreateUnbounded<ServerMessage>(
            new UnboundedChannelOptions { SingleReader = true });

        var sendTask = SendLoopAsync(ws, sendChannel.Reader, ctx.RequestAborted);

        CancellationTokenSource? activeCts = null;

        try
        {
            await ReceiveLoopAsync(ws, async clientMsg =>
            {
                switch (clientMsg)
                {
                    case SendMessageRequest req:
                        activeCts?.Cancel();
                        activeCts?.Dispose();
                        activeCts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
                        var token = activeCts.Token;
                        // Fire and forget — responses stream back via sendChannel
                        _ = Task.Run(() => agentService.StreamResponseAsync(req, sendChannel.Writer, token), token);
                        break;

                    case CancelRequest:
                        activeCts?.Cancel();
                        break;

                    case PingMessage:
                        await sendChannel.Writer.WriteAsync(new PongMessage(), ctx.RequestAborted);
                        break;
                }
            }, ctx.RequestAborted);
        }
        finally
        {
            activeCts?.Cancel();
            activeCts?.Dispose();
            sendChannel.Writer.TryComplete();
            await sendTask;
        }
    }

    private static async Task ReceiveLoopAsync(
        WebSocket ws,
        Func<ClientMessage, Task> handler,
        CancellationToken ct)
    {
        var buffer = new ArraySegment<byte>(new byte[8 * 1024]);

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close) return;
                ms.Write(buffer.Array!, buffer.Offset, result.Count);
            }
            while (!result.EndOfMessage);

            ms.Seek(0, SeekOrigin.Begin);
            ClientMessage? msg;
            try
            {
                msg = JsonSerializer.Deserialize<ClientMessage>(ms, JsonOpts);
            }
            catch
            {
                continue; // ignore malformed messages
            }

            if (msg is not null)
                await handler(msg);
        }
    }

    private static async Task SendLoopAsync(
        WebSocket ws,
        ChannelReader<ServerMessage> reader,
        CancellationToken ct)
    {
        await foreach (var msg in reader.ReadAllAsync(ct))
        {
            if (ws.State != WebSocketState.Open) break;

            var json = JsonSerializer.SerializeToUtf8Bytes(msg, JsonOpts);
            await ws.SendAsync(
                new ArraySegment<byte>(json),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken: ct);
        }
    }
}
