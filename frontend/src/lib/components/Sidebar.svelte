<script lang="ts">
	import { page } from '$app/stores';
	import { goto } from '$app/navigation';
	import { createConversation, removeConversation } from '../stores/conversations';
	import { deleteConversation } from '../api';
	import type { ConversationSummary } from '../types';

	let { conversations }: { conversations: ConversationSummary[] } = $props();

	async function newChat() {
		const c = await createConversation();
		goto(`/c/${c.id}`);
	}

	async function handleDelete(e: MouseEvent, id: string) {
		e.preventDefault();
		e.stopPropagation();
		await deleteConversation(id);
		removeConversation(id);
		const current = $page.params.id;
		if (current === id) goto('/');
	}

	function formatDate(iso: string) {
		const d = new Date(iso);
		const now = new Date();
		const diff = now.getTime() - d.getTime();
		if (diff < 60_000) return 'just now';
		if (diff < 3_600_000) return Math.floor(diff / 60_000) + 'm ago';
		if (diff < 86_400_000) return Math.floor(diff / 3_600_000) + 'h ago';
		return d.toLocaleDateString();
	}
</script>

<aside class="w-64 shrink-0 flex flex-col bg-slate-900 text-slate-100 h-full">
	<div class="p-3 border-b border-slate-700">
		<button
			onclick={newChat}
			class="w-full rounded-lg bg-slate-700 hover:bg-slate-600 transition-colors px-3 py-2
			       text-sm font-medium text-slate-100 text-left flex items-center gap-2"
		>
			<span class="text-lg leading-none">+</span>
			New chat
		</button>
	</div>

	<nav class="flex-1 overflow-y-auto py-2">
		{#each conversations as c (c.id)}
			{@const active = $page.params.id === c.id}
			<a
				href="/c/{c.id}"
				class="group flex items-start gap-2 px-3 py-2 mx-1 rounded-lg transition-colors
				       {active ? 'bg-slate-700 text-white' : 'text-slate-300 hover:bg-slate-800 hover:text-white'}"
			>
				<div class="flex-1 min-w-0">
					<p class="text-sm truncate">{c.title}</p>
					<p class="text-xs text-slate-500 mt-0.5">{formatDate(c.createdAt)}</p>
				</div>
				<button
					onclick={(e) => handleDelete(e, c.id)}
					class="shrink-0 text-slate-500 hover:text-red-400 opacity-0 group-hover:opacity-100 transition-opacity text-xs mt-0.5"
					title="Delete conversation"
				>✕</button>
			</a>
		{:else}
			<p class="px-4 py-3 text-xs text-slate-500">No conversations yet</p>
		{/each}
	</nav>
</aside>
