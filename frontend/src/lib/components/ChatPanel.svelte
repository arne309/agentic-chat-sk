<script lang="ts">
	import { messages, isStreaming, addUserMessage } from '../stores/activeChat';
	import { send } from '../stores/websocket';
	import MessageList from './MessageList.svelte';
	import ChatInput from './ChatInput.svelte';

	let { conversationId }: { conversationId: string } = $props();

	function handleSend(content: string) {
		addUserMessage(content);
		send({ type: 'send_message', conversationId, content });
	}

	function handleCancel() {
		send({ type: 'cancel', conversationId });
	}
</script>

<div class="flex flex-col h-full">
	<MessageList messages={$messages} />
	<ChatInput
		isStreaming={$isStreaming}
		onsend={handleSend}
		oncancel={handleCancel}
	/>
</div>
