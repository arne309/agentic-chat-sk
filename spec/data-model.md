# Data Model

## Backend Models (C#)

### Conversation

```csharp
class Conversation
{
    string Id;                       // UUID
    string Title;                    // Auto-derived from first user message (60 char max)
    DateTimeOffset CreatedAt;
    ChatHistoryAgentThread Thread;   // SK-managed chat history (internal)
    List<Message> Messages;          // UI-facing message log
}
```

### Message

```csharp
class Message
{
    string Id;                       // UUID
    MessageRole Role;                // User | Assistant
    List<MessagePart> Parts;         // Ordered sequence of content parts
    DateTimeOffset CreatedAt;
}
```

### MessagePart (Polymorphic)

Serialized with a `kind` discriminator field.

```csharp
// kind: "text"
class TextPart : MessagePart
{
    string Content;                  // Plain text or markdown
}

// kind: "tool_call"
class ToolCallPart : MessagePart
{
    ToolCallInfo ToolCall;
    // ToolCallInfo contains:
    //   string ToolName;
    //   Dictionary<string, object?> Arguments;
    //   string? Result;
    //   long? DurationMs;
}

// kind: "content_block"
class ContentBlockPart : MessagePart
{
    string Source;                   // File path (e.g., "report.md")
    string Content;                 // Raw markdown content
}
```

### Key Design Decision: Ordered Parts

Messages use an ordered `Parts` list rather than separate `Content` and `ToolCalls` arrays. This preserves the exact interleaving of text and tool calls as they occurred during streaming:

```
Example parts sequence for an assistant message:

  1. TextPart("I'll look at the files.")
  2. ToolCallPart(ls, {path: "."}, result: "...")       ← inline
  3. TextPart("I found 3 files. Let me read one.")
  4. ToolCallPart(read_file, {path: "data.csv"}, ...)   ← inline
  5. ContentBlockPart("report.md", "# Report\n...")      ← rendered card
  6. TextPart("Here's what I found.")
```

This ensures tool call badges appear at their actual execution point in the conversation, not grouped at the top.

## Frontend Types (TypeScript)

### MessagePart (Discriminated Union)

```typescript
type MessagePart =
  | { kind: 'text'; content: string }
  | { kind: 'tool_call'; toolCall: ToolCallEvent }
  | { kind: 'content_block'; source: string; content: string };
```

### Message

```typescript
interface Message {
  id: string;
  role: 'user' | 'assistant' | 'error';
  parts: MessagePart[];
  streaming: boolean;              // true while agent is still responding
}
```

### ToolCallEvent

```typescript
interface ToolCallEvent {
  toolName: string;
  arguments: Record<string, unknown>;
  result?: string;                 // undefined while pending
  durationMs?: number;             // set on completion
}
```

## JSON Serialization

### REST API

The backend uses `System.Text.Json` with camelCase naming policy. All REST responses use camelCase property names.

### WebSocket Messages

WebSocket messages use explicit `[JsonPropertyName]` attributes for camelCase serialization, independent of the controller JSON options.

### Polymorphic MessagePart

Uses `System.Text.Json` polymorphic serialization (`[JsonPolymorphic]`):

```json
{
  "id": "abc-123",
  "role": "Assistant",
  "parts": [
    { "kind": "text", "content": "Let me check..." },
    {
      "kind": "tool_call",
      "toolCall": {
        "toolName": "ls",
        "arguments": { "path": "." },
        "result": "[{\"name\":\"file.txt\",...}]",
        "durationMs": 5
      }
    },
    { "kind": "text", "content": "Found the file." }
  ],
  "createdAt": "2026-03-22T21:00:38Z"
}
```

## Conversation Store

- **Storage**: In-memory (`ConcurrentDictionary`)
- **Persistence**: None — data is lost on backend restart
- **Concurrency**: Thread-safe via `ConcurrentDictionary`
- **Title derivation**: Auto-generated from first user message, truncated to 60 characters
