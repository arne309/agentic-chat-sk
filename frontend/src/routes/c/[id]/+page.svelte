<script lang="ts">
	import { onMount } from 'svelte';
	import { activeConversationId, initChat } from '$lib/stores/activeChat';
	import ChatPanel from '$lib/components/ChatPanel.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	onMount(() => {
		activeConversationId.set(data.conversationId);
		initChat(data.messages);
	});

	$effect(() => {
		activeConversationId.set(data.conversationId);
		initChat(data.messages);
	});
</script>

<div class="h-full">
	<ChatPanel conversationId={data.conversationId} />
</div>
