<script lang="ts">
	import { renderMarkdown } from '../markdown';

	let { source, content }: { source: string; content: string } = $props();
</script>

<div class="my-2 rounded-xl border border-slate-200 bg-slate-50 overflow-hidden shadow-sm">
	<div class="flex items-center gap-2 px-3 py-2 bg-slate-100 border-b border-slate-200">
		<span class="text-slate-400 text-xs">📄</span>
		<span class="text-xs font-mono text-slate-600 truncate">{source}</span>
	</div>
	<div class="px-4 py-3 prose prose-sm prose-slate max-w-none overflow-y-auto max-h-96
	            prose-p:my-1 prose-pre:bg-white prose-pre:border prose-pre:border-slate-200
	            prose-code:text-slate-700 prose-code:bg-white prose-code:px-1 prose-code:rounded">
		{@html renderMarkdown(content)}
	</div>
</div>
