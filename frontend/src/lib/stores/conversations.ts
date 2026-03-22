import { writable } from 'svelte/store';
import { fetchConversations, createConversation as apiCreate } from '../api';
import type { ConversationSummary } from '../types';

export const conversations = writable<ConversationSummary[]>([]);

export async function loadConversations(): Promise<void> {
	try {
		const list = await fetchConversations();
		conversations.set(list);
	} catch {
		// Backend not yet running — start with empty list
	}
}

export async function createConversation(): Promise<ConversationSummary> {
	const c = await apiCreate();
	conversations.update((list) => [c, ...list]);
	return c;
}

export function upsertConversation(updated: ConversationSummary): void {
	conversations.update((list) => {
		const idx = list.findIndex((c) => c.id === updated.id);
		if (idx === -1) return [updated, ...list];
		const next = [...list];
		next[idx] = updated;
		return next;
	});
}

export function removeConversation(id: string): void {
	conversations.update((list) => list.filter((c) => c.id !== id));
}
