# Specification Index

This folder contains the detailed specification for the Agentic Chat Application.

## Documents

| File | Description |
|------|-------------|
| [architecture.md](architecture.md) | System architecture, communication protocols, message flow diagrams |
| [data-model.md](data-model.md) | Data models (backend C# + frontend TypeScript), serialization format |
| [agent-tools.md](agent-tools.md) | Agent tool inventory, parameters, sandbox security model |
| [backend.md](backend.md) | Backend services, REST API, WebSocket handler, configuration |
| [frontend.md](frontend.md) | Frontend components, stores, routing, rendering pipeline |

## Quick Reference

**Stack**: C# / ASP.NET Core 9 + Semantic Kernel 1.74 + SvelteKit 5 + Tailwind CSS 4 + Deno

**Ports**: Backend 5092, Frontend 5173

**LLM**: OpenRouter API (anthropic/claude-sonnet-4-5)

**Key features**:
- Real-time streaming chat via WebSocket
- Agent with file system access (ls, read, write) and TypeScript execution
- Tool calls rendered inline at execution position
- Markdown content blocks rendered as document cards
- In-memory conversation store (no persistence across restarts)
