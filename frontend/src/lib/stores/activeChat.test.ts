import { describe, it, expect, beforeEach, vi } from 'vitest';
import { get } from 'svelte/store';
import {
	messages,
	isStreaming,
	activeConversationId,
	initChat,
	addUserMessage,
	handleServerMessage
} from './activeChat';
import type { Message, ServerMessage } from '../types';

// Mock the conversations store to avoid side effects
vi.mock('./conversations', () => ({
	upsertConversation: vi.fn()
}));

import { upsertConversation } from './conversations';

beforeEach(() => {
	initChat([]);
	activeConversationId.set(null);
	isStreaming.set(false);
	vi.clearAllMocks();
});

describe('initChat', () => {
	it('sets messages and clears streaming', () => {
		const initial: Message[] = [
			{
				id: '1',
				role: 'user',
				parts: [{ kind: 'text', content: 'hi' }],
				streaming: false
			}
		];

		initChat(initial);

		expect(get(messages)).toEqual(initial);
		expect(get(isStreaming)).toBe(false);
	});
});

describe('addUserMessage', () => {
	it('appends a user message with text part', () => {
		addUserMessage('hello');

		const msgs = get(messages);
		expect(msgs).toHaveLength(1);
		expect(msgs[0].role).toBe('user');
		expect(msgs[0].streaming).toBe(false);
		expect(msgs[0].parts).toEqual([{ kind: 'text', content: 'hello' }]);
	});
});

describe('handleServerMessage', () => {
	const convId = 'conv1';

	beforeEach(() => {
		activeConversationId.set(convId);
	});

	it('agent_start creates streaming assistant message', () => {
		handleServerMessage({
			type: 'agent_start',
			conversationId: convId,
			messageId: 'm1'
		});

		const msgs = get(messages);
		expect(msgs).toHaveLength(1);
		expect(msgs[0].role).toBe('assistant');
		expect(msgs[0].streaming).toBe(true);
		expect(msgs[0].parts).toEqual([{ kind: 'text', content: '' }]);
		expect(get(isStreaming)).toBe(true);
	});

	it('ignores messages for different conversation', () => {
		handleServerMessage({
			type: 'agent_start',
			conversationId: 'other',
			messageId: 'm1'
		});

		expect(get(messages)).toHaveLength(0);
	});

	it('token appends to last text part', () => {
		handleServerMessage({
			type: 'agent_start',
			conversationId: convId,
			messageId: 'm1'
		});

		handleServerMessage({
			type: 'token',
			conversationId: convId,
			messageId: 'm1',
			delta: 'hello'
		});

		const parts = get(messages)[0].parts;
		expect(parts).toHaveLength(1);
		expect(parts[0]).toEqual({ kind: 'text', content: 'hello' });
	});

	it('multiple tokens concatenate', () => {
		handleServerMessage({
			type: 'agent_start',
			conversationId: convId,
			messageId: 'm1'
		});

		handleServerMessage({
			type: 'token',
			conversationId: convId,
			messageId: 'm1',
			delta: 'he'
		});
		handleServerMessage({
			type: 'token',
			conversationId: convId,
			messageId: 'm1',
			delta: 'llo'
		});

		const parts = get(messages)[0].parts;
		expect(parts[0]).toEqual({ kind: 'text', content: 'hello' });
	});

	it('token after tool_call creates new text part', () => {
		handleServerMessage({
			type: 'agent_start',
			conversationId: convId,
			messageId: 'm1'
		});

		handleServerMessage({
			type: 'tool_call',
			conversationId: convId,
			messageId: 'm1',
			toolName: 'ls',
			arguments: { path: '.' }
		});

		handleServerMessage({
			type: 'token',
			conversationId: convId,
			messageId: 'm1',
			delta: 'result'
		});

		const parts = get(messages)[0].parts;
		expect(parts).toHaveLength(3); // text("") + tool_call + text("result")
		expect(parts[2]).toEqual({ kind: 'text', content: 'result' });
	});

	it('tool_call appends tool_call part', () => {
		handleServerMessage({
			type: 'agent_start',
			conversationId: convId,
			messageId: 'm1'
		});

		handleServerMessage({
			type: 'tool_call',
			conversationId: convId,
			messageId: 'm1',
			toolName: 'ls',
			arguments: { path: '.' }
		});

		const parts = get(messages)[0].parts;
		expect(parts).toHaveLength(2); // initial text + tool_call
		const tc = parts[1];
		expect(tc.kind).toBe('tool_call');
		if (tc.kind === 'tool_call') {
			expect(tc.toolCall.toolName).toBe('ls');
			expect(tc.toolCall.arguments).toEqual({ path: '.' });
			expect(tc.toolCall.result).toBeUndefined();
		}
	});

	it('tool_result patches matching tool_call', () => {
		handleServerMessage({
			type: 'agent_start',
			conversationId: convId,
			messageId: 'm1'
		});

		handleServerMessage({
			type: 'tool_call',
			conversationId: convId,
			messageId: 'm1',
			toolName: 'ls',
			arguments: {}
		});

		handleServerMessage({
			type: 'tool_result',
			conversationId: convId,
			messageId: 'm1',
			toolName: 'ls',
			result: '["file.txt"]',
			durationMs: 5
		});

		const parts = get(messages)[0].parts;
		const tc = parts[1];
		if (tc.kind === 'tool_call') {
			expect(tc.toolCall.result).toBe('["file.txt"]');
			expect(tc.toolCall.durationMs).toBe(5);
		}
	});

	it('content_block appends content_block part', () => {
		handleServerMessage({
			type: 'agent_start',
			conversationId: convId,
			messageId: 'm1'
		});

		handleServerMessage({
			type: 'content_block',
			conversationId: convId,
			messageId: 'm1',
			source: 'report.md',
			content: '# Hello'
		});

		const parts = get(messages)[0].parts;
		const cb = parts[parts.length - 1];
		expect(cb.kind).toBe('content_block');
		if (cb.kind === 'content_block') {
			expect(cb.source).toBe('report.md');
			expect(cb.content).toBe('# Hello');
		}
	});

	it('agent_done sets streaming to false', () => {
		handleServerMessage({
			type: 'agent_start',
			conversationId: convId,
			messageId: 'm1'
		});

		handleServerMessage({
			type: 'agent_done',
			conversationId: convId,
			messageId: 'm1'
		});

		expect(get(messages)[0].streaming).toBe(false);
		expect(get(isStreaming)).toBe(false);
	});

	it('error with non-cancelled code adds error message', () => {
		handleServerMessage({
			type: 'error',
			conversationId: convId,
			code: 'agent_error',
			message: 'boom'
		});

		const msgs = get(messages);
		expect(msgs).toHaveLength(1);
		expect(msgs[0].role).toBe('error');
		expect(msgs[0].parts[0]).toEqual({ kind: 'text', content: 'boom' });
		expect(get(isStreaming)).toBe(false);
	});

	it('error with cancelled code does not add message', () => {
		handleServerMessage({
			type: 'error',
			conversationId: convId,
			code: 'cancelled',
			message: 'Response cancelled'
		});

		expect(get(messages)).toHaveLength(0);
		expect(get(isStreaming)).toBe(false);
	});

	it('conversation_updated calls upsertConversation', () => {
		const summary = {
			id: 'c1',
			title: 'Test',
			createdAt: '2026-01-01',
			messageCount: 3
		};

		handleServerMessage({
			type: 'conversation_updated',
			conversation: summary
		} as ServerMessage);

		expect(upsertConversation).toHaveBeenCalledWith(summary);
	});
});
