<script lang="ts">
	import '../app.css';
	import { onMount, onDestroy } from 'svelte';
	import { conversations, loadConversations } from '$lib/stores/conversations';
	import { connect, disconnect, onMessage } from '$lib/stores/websocket';
	import { handleServerMessage } from '$lib/stores/activeChat';
	import Sidebar from '$lib/components/Sidebar.svelte';

	let { children } = $props();

	let unsubscribe: (() => void) | undefined;

	onMount(() => {
		loadConversations();
		connect();
		unsubscribe = onMessage(handleServerMessage);
	});

	onDestroy(() => {
		unsubscribe?.();
		disconnect();
	});
</script>

<div class="flex h-screen overflow-hidden bg-slate-50">
	<Sidebar conversations={$conversations} />
	<main class="flex-1 overflow-hidden">
		{@render children()}
	</main>
</div>
