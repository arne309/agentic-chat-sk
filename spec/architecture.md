# Architecture Overview

## System Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        Frontend (SvelteKit)                     │
│                        http://localhost:5173                     │
│                                                                 │
│  ┌──────────┐  ┌────────────┐  ┌──────────────────────────────┐│
│  │ Sidebar   │  │ ChatPanel  │  │ Stores                      ││
│  │ (convos)  │  │ (messages) │  │  - websocket.ts (WS client) ││
│  │           │  │            │  │  - activeChat.ts (messages)  ││
│  │           │  │            │  │  - conversations.ts (list)   ││
│  └──────────┘  └────────────┘  └──────────────────────────────┘│
│        │              │                      │                  │
│        │  REST API     │  WebSocket           │                 │
└────────┼──────────────┼──────────────────────┼──────────────────┘
         │              │                      │
    ┌────┴──────────────┴──────────────────────┴──────────────────┐
    │    Vite Proxy (dev mode)                                    │
    │    /api/* → localhost:5092    /ws → ws://localhost:5092      │
    └────┬──────────────┬──────────────────────┬──────────────────┘
         │              │                      │
┌────────┴──────────────┴──────────────────────┴──────────────────┐
│                    Backend (ASP.NET Core 9.0)                   │
│                    http://localhost:5092                         │
│                                                                 │
│  ┌──────────────────┐  ┌────────────────────────────────────┐  │
│  │ REST Controller   │  │ WebSocket Handler                  │  │
│  │ /api/conversations│  │ /ws                                │  │
│  │  GET  / (list)    │  │  - receive loop (client messages)  │  │
│  │  POST / (create)  │  │  - send loop (server messages)     │  │
│  │  GET  /:id        │  │  - Channel<ServerMessage>          │  │
│  │  DELETE /:id      │  │                                    │  │
│  └──────────────────┘  └────────────┬───────────────────────┘  │
│                                     │                           │
│  ┌──────────────────────────────────┴───────────────────────┐  │
│  │ AgentService                                              │  │
│  │  - ChatCompletionAgent (Semantic Kernel)                  │  │
│  │  - WsToolCallFilter (intercepts function calls)           │  │
│  │  - Streams tokens + tool events via Channel               │  │
│  └──────────────────────────────────┬───────────────────────┘  │
│                                     │                           │
│  ┌──────────────────────────────────┴───────────────────────┐  │
│  │ Semantic Kernel                                           │  │
│  │  - OpenRouter API (OpenAI-compatible)                     │  │
│  │  - Model: anthropic/claude-sonnet-4-5                     │  │
│  │  - Auto function calling                                  │  │
│  └──────────────────────────────────┬───────────────────────┘  │
│                                     │                           │
│  ┌──────────────────────────────────┴───────────────────────┐  │
│  │ FileSystemPlugin (Kernel Functions)                       │  │
│  │  - ls(path)              → directory listing              │  │
│  │  - read_file(path,...)   → file content                   │  │
│  │  - write_file(path,...)  → create/update file             │  │
│  │  - exec_script(code)     → run inline TypeScript          │  │
│  │  - exec_script_file(path)→ run .ts file                   │  │
│  │  - response_include(path)→ render MD as content block     │  │
│  └──────────────────────────────────┬───────────────────────┘  │
│                                     │                           │
│  ┌──────────────────────────────────┴───────────────────────┐  │
│  │ ScriptSandbox (Deno)                                      │  │
│  │  - Workspace-scoped read/write permissions                │  │
│  │  - --allow-import (npm:, jsr:, esm.sh)                    │  │
│  │  - Captures stdout, stderr, exit code                     │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ ConversationStore (in-memory)                             │  │
│  │  - ConcurrentDictionary<string, Conversation>             │  │
│  │  - CRUD + title derivation                                │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────┐
│  ../workspace/       │
│  (sandboxed file     │
│   system for agent)  │
└─────────────────────┘
```

## Communication Protocols

### REST API

Used for CRUD operations on conversations (listing, loading history, creating, deleting).

### WebSocket

Used for real-time streaming of agent responses. All messages are JSON-serialized with a `type` discriminator field.

**Client → Server:**
| Type | Purpose |
|------|---------|
| `send_message` | Send user prompt |
| `cancel` | Cancel in-flight response |
| `ping` | Keep-alive |

**Server → Client:**
| Type | Purpose |
|------|---------|
| `agent_start` | Agent begins processing |
| `token` | Streaming text delta |
| `tool_call` | Tool invocation started |
| `tool_result` | Tool completed with result |
| `agent_done` | Agent finished responding |
| `content_block` | Rendered markdown file |
| `error` | Error occurred |
| `pong` | Keep-alive response |
| `conversation_updated` | Conversation metadata changed |

### Message Flow

```
Client                    Server                  Agent/SK
  │                         │                        │
  │── send_message ────────▶│                        │
  │                         │── invoke agent ───────▶│
  │◀── agent_start ────────│                        │
  │                         │                        │
  │                         │    (agent streams)     │
  │◀── token ──────────────│◀── text chunk ────────│
  │◀── token ──────────────│◀── text chunk ────────│
  │                         │                        │
  │                         │    (agent calls tool)  │
  │◀── tool_call ──────────│◀── function call ─────│
  │                         │── execute function ───▶│
  │◀── tool_result ────────│◀── function result ───│
  │                         │                        │
  │◀── token ──────────────│◀── text chunk ────────│
  │◀── agent_done ─────────│                        │
  │◀── conversation_updated│                        │
  │                         │                        │
```
