import { writable, get } from 'svelte/store';
import type { Message, MessagePart, ServerMessage, ToolCallEvent } from '../types';
import { upsertConversation } from './conversations';

export const messages = writable<Message[]>([]);
export const isStreaming = writable(false);
export const activeConversationId = writable<string | null>(null);

export function initChat(conversationMessages: Message[]): void {
	messages.set(conversationMessages);
	isStreaming.set(false);
}

export function addUserMessage(content: string): void {
	messages.update((msgs) => [
		...msgs,
		{
			id: crypto.randomUUID(),
			role: 'user',
			parts: [{ kind: 'text', content }],
			streaming: false
		}
	]);
}

export function handleServerMessage(msg: ServerMessage): void {
	const convId = get(activeConversationId);

	if ('conversationId' in msg && msg.conversationId !== convId) return;

	switch (msg.type) {
		case 'agent_start':
			isStreaming.set(true);
			messages.update((msgs) => [
				...msgs,
				{
					id: msg.messageId,
					role: 'assistant',
					parts: [{ kind: 'text', content: '' }],
					streaming: true
				}
			]);
			break;

		case 'token':
			messages.update((msgs) =>
				msgs.map((m) => {
					if (m.id !== msg.messageId) return m;
					const parts = [...m.parts];
					const last = parts[parts.length - 1];
					if (last?.kind === 'text') {
						parts[parts.length - 1] = { kind: 'text', content: last.content + msg.delta };
					} else {
						parts.push({ kind: 'text', content: msg.delta });
					}
					return { ...m, parts };
				})
			);
			break;

		case 'tool_call': {
			const tc: ToolCallEvent = { toolName: msg.toolName, arguments: msg.arguments };
			messages.update((msgs) =>
				msgs.map((m) =>
					m.id === msg.messageId
						? { ...m, parts: [...m.parts, { kind: 'tool_call', toolCall: tc }] }
						: m
				)
			);
			break;
		}

		case 'tool_result':
			messages.update((msgs) =>
				msgs.map((m) => {
					if (m.id !== msg.messageId) return m;
					const parts: MessagePart[] = m.parts.map((p) => {
						if (
							p.kind === 'tool_call' &&
							p.toolCall.toolName === msg.toolName &&
							p.toolCall.result === undefined
						) {
							return {
								kind: 'tool_call',
								toolCall: { ...p.toolCall, result: msg.result, durationMs: msg.durationMs }
							};
						}
						return p;
					});
					return { ...m, parts };
				})
			);
			break;

		case 'content_block':
			messages.update((msgs) =>
				msgs.map((m) =>
					m.id === msg.messageId
						? {
								...m,
								parts: [
									...m.parts,
									{ kind: 'content_block', source: msg.source, content: msg.content }
								]
						  }
						: m
				)
			);
			break;

		case 'agent_done':
			messages.update((msgs) =>
				msgs.map((m) => (m.id === msg.messageId ? { ...m, streaming: false } : m))
			);
			isStreaming.set(false);
			break;

		case 'error':
			isStreaming.set(false);
			if (msg.code !== 'cancelled') {
				messages.update((msgs) => [
					...msgs,
					{
						id: crypto.randomUUID(),
						role: 'error',
						parts: [{ kind: 'text', content: msg.message }],
						streaming: false
					}
				]);
			}
			break;

		case 'conversation_updated':
			upsertConversation(msg.conversation);
			break;
	}
}
