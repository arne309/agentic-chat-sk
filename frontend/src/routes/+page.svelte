<script lang="ts">
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { conversations, createConversation } from '$lib/stores/conversations';
	import { get } from 'svelte/store';

	onMount(async () => {
		const list = get(conversations);
		if (list.length > 0) {
			goto(`/c/${list[0].id}`, { replaceState: true });
		} else {
			const c = await createConversation();
			goto(`/c/${c.id}`, { replaceState: true });
		}
	});
</script>
