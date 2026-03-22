# Backend Specification

## Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Runtime | .NET | 9.0 |
| Framework | ASP.NET Core | 9.0 |
| AI/Agent | Microsoft Semantic Kernel | 1.74.0 |
| Agent Framework | SK Agents Core | 1.74.0 |
| LLM Provider | OpenRouter (OpenAI-compatible) | — |
| Default Model | anthropic/claude-sonnet-4-5 | — |
| Script Runtime | Deno | (system-installed) |
| Serialization | System.Text.Json | (built-in) |

## Configuration

### `appsettings.json`

```json
{
  "OpenAI": {
    "BaseUrl": "https://openrouter.ai/api/v1",
    "Model": "anthropic/claude-sonnet-4-5"
  },
  "Workspace": {
    "Root": "../workspace"
  }
}
```

### `appsettings.Development.json` (gitignored)

```json
{
  "OpenAI": {
    "ApiKey": "sk-or-v1-..."
  }
}
```

### Overriding via environment

All settings can be overridden via environment variables:
- `OpenAI__ApiKey` → API key
- `OpenAI__Model` → Model name
- `OpenAI__BaseUrl` → API base URL
- `Workspace__Root` → Workspace directory

## Service Architecture

### Dependency Injection

```
Singleton:
  ├── Kernel (Semantic Kernel instance)
  │   └── FileSystemPlugin (registered as plugin)
  ├── ConversationStore (in-memory)
  └── ScriptSandbox (Deno executor)

Scoped (per-request):
  └── AgentService (orchestrates agent interactions)
```

### AgentService

Manages the `ChatCompletionAgent` and orchestrates streaming responses.

**Agent configuration:**
- Name: `FileAgent`
- Function choice: `FunctionChoiceBehavior.Auto()` (agent decides when to call tools)
- Plugins: `FileSystemPlugin` (registered on the kernel)

**Streaming flow:**
1. Receives `SendMessageRequest` via WebSocket
2. Records user message in conversation store
3. Installs `WsToolCallFilter` on the kernel
4. Emits `agent_start` message
5. Invokes agent with streaming (`InvokeStreamingAsync`)
6. For each text chunk: appends to filter's parts list, emits `token` message
7. Tool calls are intercepted by the filter (see below)
8. On completion: saves assistant message with ordered parts, emits `agent_done`
9. Emits `conversation_updated` with new metadata
10. Cleans up filter from kernel

**Error handling:**
- `OperationCanceledException` → emits `error` with code `"cancelled"`
- General exceptions → emits `error` with code `"agent_error"`
- Filter is always removed in `finally` block

### WsToolCallFilter

Implements `IFunctionInvocationFilter` to intercept Semantic Kernel function calls.

**Responsibilities:**
- Maintains an ordered `List<MessagePart>` (the parts list)
- `AppendToken(delta)`: Appends text to the last TextPart, or creates a new one
- On tool invocation:
  1. Creates `ToolCallPart` and adds to parts list
  2. Emits `tool_call` WebSocket message
  3. Executes the function (via `next(context)`)
  4. Records result and duration on the `ToolCallPart`
  5. Emits `tool_result` WebSocket message
- Special handling for `response_include`:
  - Adds `ContentBlockPart` to parts list
  - Emits `content_block` WebSocket message
  - Emits `tool_result` with summary text ("Rendered {filename}")

### ConversationStore

In-memory conversation persistence using `ConcurrentDictionary`.

**Operations:**
- `GetOrCreate(id)` — lazy creation
- `Get(id)` — nullable lookup
- `Create()` — new conversation with UUID
- `Delete(id)` — removal
- `GetAll()` — sorted by creation time (descending)
- `DeriveTitle(c)` — extracts title from first user message (truncated to 60 chars)

**Limitations:**
- No persistent storage — data lost on restart
- No pagination for large conversation lists

### ScriptSandbox

Executes TypeScript code using the Deno runtime.

**Methods:**
- `RunAsync(scriptContent)` — executes inline script (writes to temp file, runs, cleans up)
- `RunFileAsync(scriptPath)` — executes existing .ts file from workspace

**Deno permissions:**
```
--no-prompt                              # Never prompt for permissions
--allow-read=<workspace>                 # Read workspace files
--allow-write=<workspace>                # Write workspace files
--allow-import=npm:,jsr:,esm.sh          # Import packages
```

**Return format:**
```json
{
  "exitCode": 0,
  "stdout": "...",
  "stderr": "..."
}
```

### FileSystemPlugin

Semantic Kernel plugin that exposes file system and script execution capabilities.

**Path security:**
- All paths resolved via `ResolveSafe(path)` against workspace root
- Uses `Path.GetFullPath` to resolve relative paths
- Validates resolved path starts with workspace root (prefix check)
- Throws on path traversal attempts

## REST API

### `GET /api/conversations`

Returns list of all conversations (summary only).

**Response:** `ConversationSummary[]`

### `POST /api/conversations`

Creates a new empty conversation.

**Response:** `ConversationSummary`

### `GET /api/conversations/{id}`

Returns full conversation with all messages and their parts.

**Response:**
```json
{
  "id": "...",
  "title": "...",
  "createdAt": "...",
  "messages": [
    {
      "id": "...",
      "role": "User",
      "parts": [{ "kind": "text", "content": "..." }],
      "createdAt": "..."
    }
  ]
}
```

### `DELETE /api/conversations/{id}`

Deletes a conversation. Returns 204 on success, 404 if not found.

## WebSocket Handler

Endpoint: `/ws`

**Architecture:**
- Uses `System.Threading.Channels.Channel<ServerMessage>` for thread-safe message queuing
- Two concurrent loops:
  - **Receive loop**: Reads client WebSocket frames → deserializes → dispatches
  - **Send loop**: Reads from channel → serializes → writes WebSocket frames
- Supports multiple concurrent requests per connection via CancellationTokenSource

**Connection lifecycle:**
1. Validates WebSocket upgrade request
2. Creates unbounded channel
3. Launches send loop and receive loop concurrently
4. On close: cancels active operations, completes channel

## JSON Serialization

- **REST API**: Uses `JsonNamingPolicy.CamelCase` via `AddJsonOptions`
- **WebSocket messages**: Use explicit `[JsonPropertyName]` attributes
- **Polymorphic types**: Use `[JsonPolymorphic]` + `[JsonDerivedType]` attributes
- **Enums**: Use `[JsonStringEnumConverter]` (preserves member name casing)
