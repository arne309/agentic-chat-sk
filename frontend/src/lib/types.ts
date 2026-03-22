// ── Server → Client messages ─────────────────────────────────────────────────

export type ServerMessage =
	| { type: 'agent_start'; conversationId: string; messageId: string }
	| { type: 'token'; conversationId: string; messageId: string; delta: string }
	| {
			type: 'tool_call';
			conversationId: string;
			messageId: string;
			toolName: string;
			arguments: Record<string, unknown>;
	  }
	| {
			type: 'tool_result';
			conversationId: string;
			messageId: string;
			toolName: string;
			result: string;
			durationMs: number;
	  }
	| { type: 'agent_done'; conversationId: string; messageId: string }
	| { type: 'error'; conversationId: string; code: string; message: string }
	| { type: 'pong' }
	| { type: 'conversation_updated'; conversation: ConversationSummary }
	| { type: 'content_block'; conversationId: string; messageId: string; source: string; content: string }
	| {
			type: 'data_block';
			conversationId: string;
			messageId: string;
			source: string;
			columns: Array<{ name: string; type: string }>;
			rows: Array<Array<unknown>>;
			totalRowCount: number;
			previewRowCount: number;
	  };

// ── Client → Server messages ─────────────────────────────────────────────────

export type ClientMessage =
	| { type: 'send_message'; conversationId: string; content: string }
	| { type: 'cancel'; conversationId: string }
	| { type: 'ping' };

// ── Domain types ─────────────────────────────────────────────────────────────

export interface ConversationSummary {
	id: string;
	title: string;
	createdAt: string;
	messageCount: number;
}

export interface ToolCallEvent {
	toolName: string;
	arguments: Record<string, unknown>;
	result?: string;
	durationMs?: number;
}

export type MessagePart =
	| { kind: 'text'; content: string }
	| { kind: 'tool_call'; toolCall: ToolCallEvent }
	| { kind: 'content_block'; source: string; content: string }
	| {
			kind: 'data_block';
			source: string;
			columns: Array<{ name: string; type: string }>;
			rows: Array<Array<unknown>>;
			totalRowCount: number;
			previewRowCount: number;
	  };

export interface Message {
	id: string;
	role: 'user' | 'assistant' | 'error';
	parts: MessagePart[];
	streaming: boolean;
}

export interface FullConversation {
	id: string;
	title: string;
	createdAt: string;
	messages: Array<{
		id: string;
		role: 'User' | 'Assistant';
		parts: MessagePart[];
		createdAt: string;
	}>;
}
