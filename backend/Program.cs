using AgentApp.Backend.Plugins;
using AgentApp.Backend.Services;
using AgentApp.Backend.WebSockets;
using Microsoft.SemanticKernel;
using OpenAI;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

// Controllers — use camelCase for REST responses to match frontend expectations
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

// CORS — allow SvelteKit dev server
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

// Semantic Kernel
builder.Services.AddSingleton<IScriptSandbox, ScriptSandbox>();
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILoggerFactory>();

    var apiKey = config["OpenAI:ApiKey"]
        ?? throw new InvalidOperationException(
            "OpenAI:ApiKey is not configured. Set it in appsettings.Development.json.");
    var model = config["OpenAI:Model"] ?? "anthropic/claude-sonnet-4-5";
    var baseUrl = config["OpenAI:BaseUrl"] ?? "https://openrouter.ai/api/v1";

    var openAiClient = new OpenAIClient(
        new ApiKeyCredential(apiKey),
        new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

    var kb = Kernel.CreateBuilder();
    kb.AddOpenAIChatCompletion(modelId: model, openAIClient: openAiClient);

    var kernel = kb.Build();

    // Register file system plugin (plugin dependencies resolved manually)
    var sandbox = sp.GetRequiredService<IScriptSandbox>();
    kernel.Plugins.AddFromObject(new FileSystemPlugin(sandbox, config), "FileSystem");

    return kernel;
});

// In-memory conversation store
builder.Services.AddSingleton<ConversationStore>();

// Agent service (depends on Kernel + ConversationStore)
builder.Services.AddScoped<AgentService>();

var app = builder.Build();

app.UseCors();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

// WebSocket endpoint
app.Map("/ws", AgentWebSocketHandler.HandleAsync);

app.MapControllers();

app.Run();
