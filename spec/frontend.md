# Frontend Specification

## Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Framework | SvelteKit | 2.50.2 |
| UI Library | Svelte | 5.51.0 (runes mode) |
| Styling | Tailwind CSS | 4.2.2 |
| Typography | @tailwindcss/typography | plugin |
| Markdown | marked | 17.0.5 |
| Sanitization | DOMPurify | 3.3.3 |
| Language | TypeScript | 5.9.3 |
| Build | Vite | 7.3.1 |
| Package Manager | pnpm | — |

## Layout

Two-column layout:

```
┌───────────────────┬──────────────────────────────────────────┐
│                   │                                          │
│     Sidebar       │              Chat Panel                  │
│   (240px dark)    │                                          │
│                   │  ┌────────────────────────────────────┐  │
│  [+ New chat]     │  │                                    │  │
│                   │  │         Message List                │  │
│  conversation 1   │  │     (scrollable, auto-scroll)      │  │
│  conversation 2   │  │                                    │  │
│  conversation 3   │  │  User bubbles: right, blue         │  │
│                   │  │  Agent bubbles: left, white         │  │
│                   │  │  Error bubbles: left, red border    │  │
│                   │  │                                    │  │
│                   │  │  Tool calls: inline badges          │  │
│                   │  │  Content blocks: inline cards       │  │
│                   │  │                                    │  │
│                   │  └────────────────────────────────────┘  │
│                   │  ┌────────────────────────────────────┐  │
│                   │  │  Chat Input (auto-resize textarea) │  │
│                   │  │                          [Send]     │  │
│                   │  └────────────────────────────────────┘  │
└───────────────────┴──────────────────────────────────────────┘
```

## Components

### Sidebar (`Sidebar.svelte`)
- Dark background (slate-900)
- "New chat" button → navigates to `/c/new`
- Conversation list sorted by creation time
- Each item shows: title (truncated), relative time ("just now", "5m ago", "2h ago", or date)
- Active conversation highlighted (slate-700)
- Hover reveals delete button (✕)
- Delete navigates to next conversation or home

### ChatPanel (`ChatPanel.svelte`)
- Container for MessageList + ChatInput
- Handles send: adds user message to store, sends via WebSocket
- Handles cancel: sends cancel message via WebSocket

### MessageList (`MessageList.svelte`)
- Scrollable vertical list of MessageBubble components
- Auto-scrolls to bottom when new messages arrive (via `$effect`)
- Empty state: "Send a message to start the conversation"

### MessageBubble (`MessageBubble.svelte`)
- Renders a single message with role-based styling
- **User**: Blue background, right-aligned, rounded corners
- **Assistant**: White background with border, left-aligned, shadow
- **Error**: Red border, left-aligned
- Iterates over `message.parts` in order:
  - `text`: Rendered as markdown (assistant) or plain text (user)
  - `tool_call`: Renders ToolCallBadge component
  - `content_block`: Renders ContentBlock component
- Shows "Thinking..." with blinking cursor while streaming with no text yet
- Shows blinking cursor at end while streaming

### ToolCallBadge (`ToolCallBadge.svelte`)
- Compact expandable badge showing tool name
- **Pending state**: Spinner icon, no duration
- **Complete state**: Checkmark icon, duration in ms
- Expandable dropdown showing:
  - Arguments as formatted JSON
  - Result as formatted JSON (with scrollable container)

### ContentBlock (`ContentBlock.svelte`)
- Card-style container with light blue background
- Header: document icon + file path
- Body: rendered markdown (sanitized HTML)
- Max height 24rem with scroll overflow

### StreamingCursor (`StreamingCursor.svelte`)
- Blinking block cursor character (▋)
- Animates opacity between 1.0 and 0.0

### ChatInput (`ChatInput.svelte`)
- Auto-resizing textarea (max 160px height)
- Placeholder: "Message the agent... (Shift+Enter for newline)"
- Enter to send, Shift+Enter for newline
- Disabled while streaming
- Button toggles between "Send" (blue) and "Stop" (red) based on streaming state

## Stores

### `websocket.ts`
- `wsStatus`: Reactive store — `'connecting' | 'open' | 'closed' | 'error'`
- `connect()`: Establishes WebSocket, auto-reconnects with exponential backoff (1s → 2s → 4s → 8s → 16s, max 5 attempts)
- `disconnect()`: Closes socket, resets state
- `send(msg)`: Sends ClientMessage if socket is open
- `onMessage(fn)`: Registers listener, returns unsubscribe function

### `conversations.ts`
- `conversations`: Reactive store of `ConversationSummary[]`
- `loadConversations()`: Fetches from REST API
- `createConversation()`: Creates via REST, prepends to list
- `upsertConversation(c)`: Inserts or updates existing entry
- `removeConversation(id)`: Removes from list, deletes via REST

### `activeChat.ts`
- `messages`: Reactive store of `Message[]` for current conversation
- `isStreaming`: Boolean indicating active agent response
- `activeConversationId`: Current conversation ID
- `initChat(msgs)`: Resets messages for a conversation
- `addUserMessage(content)`: Appends user message with text part
- `handleServerMessage(msg)`: State machine for processing server events

## Routing

| Route | Purpose |
|-------|---------|
| `/` | Landing — redirects to first conversation or creates new one |
| `/c/new` | Creates new conversation, redirects to `/c/{id}` |
| `/c/{id}` | Chat view — loads conversation from API, renders ChatPanel |

## Rendering

- **SSR disabled** (`ssr = false`) — runs entirely client-side
- **Prerendering disabled** (`prerender = false`)
- **Markdown**: Parsed with `marked`, sanitized with `DOMPurify`, rendered as HTML
- **Prose styling**: Uses `@tailwindcss/typography` for markdown output

## Dev Server

- Port: 5173
- Proxies `/api/*` → `http://localhost:5092` (backend REST)
- Proxies `/ws` → `ws://localhost:5092` (backend WebSocket)
