<template>
	<span
		class="s-badge"
		:class="`s-badge-${tone}`"
	>
		<span
			class="s-badge-dot"
			aria-hidden="true"
		/>
		<slot />
	</span>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
	tone?: 'ok' | 'warn' | 'danger' | 'info' | 'neutral'
}>(), {
	tone: 'neutral',
})
</script>
