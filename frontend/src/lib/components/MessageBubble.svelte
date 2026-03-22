<script lang="ts">
	import type { Message } from '../types';
	import { renderMarkdown } from '../markdown';
	import ToolCallBadge from './ToolCallBadge.svelte';
	import ContentBlock from './ContentBlock.svelte';
	import StreamingCursor from './StreamingCursor.svelte';

	let { message }: { message: Message } = $props();

	let hasText = $derived(message.parts.some((p) => p.kind === 'text' && p.content.length > 0));
</script>

<div class="flex {message.role === 'user' ? 'justify-end' : 'justify-start'} mb-4">
	<div
		class="max-w-[80%] {message.role === 'user'
			? 'bg-blue-600 text-white rounded-2xl rounded-br-sm px-4 py-2'
			: message.role === 'error'
			? 'bg-red-50 text-red-700 border border-red-200 rounded-2xl rounded-bl-sm px-4 py-2'
			: 'bg-white border border-slate-200 text-slate-800 rounded-2xl rounded-bl-sm px-4 py-3 shadow-sm'}"
	>
		{#each message.parts as part}
			{#if part.kind === 'tool_call'}
				<ToolCallBadge toolCall={part.toolCall} />
			{:else if part.kind === 'content_block'}
				<ContentBlock source={part.source} content={part.content} />
			{:else if part.kind === 'text' && part.content}
				{#if message.role === 'assistant'}
					<div
						class="prose prose-sm prose-slate max-w-none
						       prose-p:my-1 prose-pre:bg-slate-100 prose-pre:text-slate-800
						       prose-code:text-slate-700 prose-code:bg-slate-100 prose-code:px-1 prose-code:rounded
						       prose-headings:text-slate-900"
						>{@html renderMarkdown(part.content)}</div>
				{:else}
					<p class="whitespace-pre-wrap leading-relaxed">{part.content}</p>
				{/if}
			{/if}
		{/each}

		{#if message.streaming && !hasText}
			<span class="text-slate-400 text-sm italic">Thinking…</span>
		{/if}

		{#if message.streaming}
			<StreamingCursor />
		{/if}
	</div>
</div>
