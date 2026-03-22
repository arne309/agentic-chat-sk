import type { ConversationSummary, FullConversation } from './types';

export async function fetchConversations(): Promise<ConversationSummary[]> {
	const res = await fetch('/api/conversations');
	if (!res.ok) throw new Error('Failed to fetch conversations');
	return res.json();
}

export async function createConversation(): Promise<ConversationSummary> {
	const res = await fetch('/api/conversations', { method: 'POST' });
	if (!res.ok) throw new Error('Failed to create conversation');
	return res.json();
}

export async function fetchConversation(
	id: string,
	fetchFn: typeof fetch = fetch
): Promise<FullConversation | null> {
	const res = await fetchFn(`/api/conversations/${id}`);
	if (res.status === 404) return null;
	if (!res.ok) throw new Error('Failed to fetch conversation');
	return res.json();
}

export async function deleteConversation(id: string): Promise<void> {
	await fetch(`/api/conversations/${id}`, { method: 'DELETE' });
}
