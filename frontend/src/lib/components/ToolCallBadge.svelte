<script lang="ts">
	import type { ToolCallEvent } from '../types';

	let { toolCall }: { toolCall: ToolCallEvent } = $props();

	let expanded = $state(false);

	let pending = $derived(toolCall.result === undefined);
	let argsStr = $derived(JSON.stringify(toolCall.arguments, null, 2));
</script>

<div class="my-1 rounded border border-slate-200 bg-slate-50 text-xs font-mono">
	<button
		class="flex w-full items-center gap-2 px-3 py-2 text-left hover:bg-slate-100 transition-colors"
		onclick={() => (expanded = !expanded)}
	>
		<span class="shrink-0">
			{#if pending}
				<span class="inline-block h-3 w-3 rounded-full border-2 border-blue-400 border-t-transparent animate-spin"></span>
			{:else}
				<span class="text-green-600">✓</span>
			{/if}
		</span>
		<span class="text-slate-600">
			{#if pending}
				calling <span class="font-semibold text-blue-700">{toolCall.toolName}</span>…
			{:else}
				<span class="font-semibold text-slate-800">{toolCall.toolName}</span>
				<span class="text-slate-400 ml-1">({toolCall.durationMs}ms)</span>
			{/if}
		</span>
		<span class="ml-auto text-slate-400">{expanded ? '▲' : '▼'}</span>
	</button>

	{#if expanded}
		<div class="border-t border-slate-200 px-3 py-2 space-y-2">
			<div>
				<div class="text-slate-500 mb-1">Arguments</div>
				<pre class="whitespace-pre-wrap text-slate-700 bg-white rounded p-2 border border-slate-100 overflow-x-auto">{argsStr}</pre>
			</div>
			{#if toolCall.result !== undefined}
				<div>
					<div class="text-slate-500 mb-1">Result</div>
					<pre class="whitespace-pre-wrap text-slate-700 bg-white rounded p-2 border border-slate-100 overflow-x-auto max-h-48">{toolCall.result}</pre>
				</div>
			{/if}
		</div>
	{/if}
</div>
