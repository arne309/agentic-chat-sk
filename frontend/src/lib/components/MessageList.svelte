<script lang="ts">
	import type { Message } from '../types';
	import MessageBubble from './MessageBubble.svelte';

	let { messages }: { messages: Message[] } = $props();

	let container: HTMLElement;

	$effect(() => {
		// Depend on messages to re-run when new messages arrive
		messages;
		if (container) {
			// Use setTimeout to scroll after DOM update
			setTimeout(() => {
				container.scrollTop = container.scrollHeight;
			}, 0);
		}
	});
</script>

<div bind:this={container} class="flex-1 overflow-y-auto px-4 py-4">
	{#if messages.length === 0}
		<div class="flex h-full items-center justify-center text-slate-400 text-sm">
			Start a conversation…
		</div>
	{:else}
		{#each messages as message (message.id)}
			<MessageBubble {message} />
		{/each}
	{/if}
</div>
