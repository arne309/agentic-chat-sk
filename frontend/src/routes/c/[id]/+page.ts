import { redirect } from '@sveltejs/kit';
import { fetchConversation } from '$lib/api';
import { createConversation } from '$lib/stores/conversations';
import type { PageLoad } from './$types';
import type { Message } from '$lib/types';

export const load: PageLoad = async ({ params, fetch }) => {
	if (params.id === 'new') {
		const c = await createConversation();
		redirect(302, `/c/${c.id}`);
	}

	const full = await fetchConversation(params.id, fetch);
	if (!full) redirect(302, '/');

	const messages: Message[] = full.messages.map((m) => ({
		id: m.id,
		role: (m.role === 'User' ? 'user' : 'assistant') as 'user' | 'assistant',
		parts: m.parts,
		streaming: false
	}));

	return { conversationId: full.id, messages };
};
