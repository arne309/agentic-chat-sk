<script lang="ts">
	let {
		disabled = false,
		isStreaming = false,
		onsend,
		oncancel
	}: {
		disabled?: boolean;
		isStreaming?: boolean;
		onsend: (content: string) => void;
		oncancel: () => void;
	} = $props();

	let value = $state('');
	let textarea: HTMLTextAreaElement;

	function submit() {
		const trimmed = value.trim();
		if (!trimmed || disabled) return;
		onsend(trimmed);
		value = '';
		resize();
	}

	function onkeydown(e: KeyboardEvent) {
		if (e.key === 'Enter' && !e.shiftKey) {
			e.preventDefault();
			submit();
		}
	}

	function resize() {
		if (!textarea) return;
		textarea.style.height = 'auto';
		textarea.style.height = Math.min(textarea.scrollHeight, 160) + 'px';
	}
</script>

<div class="border-t border-slate-200 bg-white px-4 py-3">
	<div class="flex items-end gap-2">
		<textarea
			bind:this={textarea}
			bind:value
			{onkeydown}
			oninput={resize}
			placeholder="Message the agent… (Shift+Enter for newline)"
			rows="1"
			class="flex-1 resize-none rounded-xl border border-slate-300 bg-slate-50 px-3 py-2 text-sm
			       text-slate-800 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500
			       focus:border-transparent overflow-hidden"
			disabled={disabled && !isStreaming}
		></textarea>

		{#if isStreaming}
			<button
				onclick={oncancel}
				class="shrink-0 rounded-xl bg-red-500 px-3 py-2 text-sm text-white font-medium
				       hover:bg-red-600 transition-colors"
			>
				Stop
			</button>
		{:else}
			<button
				onclick={submit}
				disabled={!value.trim() || disabled}
				class="shrink-0 rounded-xl bg-blue-600 px-3 py-2 text-sm text-white font-medium
				       hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
			>
				Send
			</button>
		{/if}
	</div>
</div>
