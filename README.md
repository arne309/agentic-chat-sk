# Agentic Chat

A full-stack agentic chat application where an AI agent can browse files, write code, execute TypeScript scripts, and present results — all through a real-time streaming chat interface.

![Stack](https://img.shields.io/badge/.NET_9-512BD4?logo=dotnet&logoColor=white)
![Stack](https://img.shields.io/badge/Semantic_Kernel-744FC6?logo=microsoft&logoColor=white)
![Stack](https://img.shields.io/badge/SvelteKit_5-FF3E00?logo=svelte&logoColor=white)
![Stack](https://img.shields.io/badge/Tailwind_CSS_4-06B6D4?logo=tailwindcss&logoColor=white)
![Stack](https://img.shields.io/badge/Deno-000000?logo=deno&logoColor=white)

## What it does

You chat with an AI agent that has access to:

- **File system** — list, read, and write files in a sandboxed workspace
- **TypeScript runtime** — execute scripts via Deno with npm/jsr package imports
- **Rich output** — render markdown documents inline as content cards

Tool calls appear inline in the conversation at the exact position they were executed, not grouped at the top. The agent streams its response in real time over WebSocket.

## Architecture

```
SvelteKit (5173) ──WebSocket/REST──▶ ASP.NET Core (5092) ──▶ Semantic Kernel ──▶ OpenRouter API
                                          │
                                          ├── FileSystemPlugin (ls, read, write)
                                          └── ScriptSandbox (Deno runtime)
                                                    │
                                                    ▼
                                              ../workspace/
```

See [`spec/`](spec/) for the full specification.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js](https://nodejs.org/) 18+ and [pnpm](https://pnpm.io/)
- [Deno](https://deno.land/) 2.x
- An [OpenRouter](https://openrouter.ai/) API key (or any OpenAI-compatible provider)

## Setup

### 1. Clone and install

```bash
git clone https://github.com/arne309/agentic-chat-sk.git
cd agentic-chat-sk
pnpm --dir frontend install
```

### 2. Configure API key

Create the secrets file (gitignored):

```bash
cat > backend/appsettings.Development.json << 'EOF'
{
  "OpenAI": {
    "ApiKey": "sk-or-v1-your-key-here"
  }
}
EOF
```

Or set via environment variable:

```bash
export OpenAI__ApiKey="sk-or-v1-your-key-here"
```

### 3. (Optional) Change model or provider

Edit `backend/appsettings.json`:

```json
{
  "OpenAI": {
    "BaseUrl": "https://openrouter.ai/api/v1",
    "Model": "anthropic/claude-sonnet-4-5"
  }
}
```

Works with any OpenAI-compatible API (OpenAI, Azure, Ollama, etc.) — just change `BaseUrl` and `Model`.

### 4. Run

Start both servers (in separate terminals):

```bash
# Backend
dotnet run --project backend --urls http://localhost:5092

# Frontend
pnpm --dir frontend dev --port 5173
```

Open **http://localhost:5173**

## Project Structure

```
├── backend/                    # C# / ASP.NET Core 9
│   ├── Controllers/            # REST API (conversations CRUD)
│   ├── Models/                 # Message, Conversation, WS messages
│   ├── Plugins/                # FileSystemPlugin (agent tools)
│   ├── Services/               # AgentService, ConversationStore, ScriptSandbox
│   ├── WebSockets/             # WebSocket handler
│   └── Program.cs              # App bootstrap & DI
├── frontend/                   # SvelteKit / TypeScript / Tailwind
│   └── src/
│       ├── lib/
│       │   ├── components/     # Chat UI components
│       │   ├── stores/         # WebSocket, messages, conversations
│       │   ├── types.ts        # Shared type definitions
│       │   └── markdown.ts     # Markdown rendering (marked + DOMPurify)
│       └── routes/             # SvelteKit pages
├── workspace/                  # Agent's sandboxed file system
└── spec/                       # Detailed specification docs
```

## Agent Tools

| Tool | Description |
|------|-------------|
| `ls(path)` | List directory contents (name, size, type) |
| `read_file(path, start?, maxChars?)` | Read file content (full or partial) |
| `write_file(path, content)` | Create or overwrite a file |
| `exec_script(code)` | Execute inline TypeScript via Deno |
| `exec_script_file(path)` | Execute a .ts file from the workspace |
| `response_include(path)` | Render a markdown file as an inline content card |

All file operations are sandboxed to the `workspace/` directory. Scripts run in Deno with restricted permissions (workspace read/write only, no network access, package imports allowed).

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | C# / ASP.NET Core 9.0 |
| AI Agent | Microsoft Semantic Kernel 1.74 |
| LLM | OpenRouter (anthropic/claude-sonnet-4-5) |
| Frontend | SvelteKit 2 + Svelte 5 (runes) |
| Styling | Tailwind CSS 4 + Typography plugin |
| Markdown | marked + DOMPurify |
| Script Sandbox | Deno 2.x |
| Real-time | WebSocket + System.Threading.Channels |

## License

Private repository.
