<template>
	<header class="s-page-header">
		<h1 class="s-page-title">
			{{ title }}
		</h1>
		<div
			v-if="$slots.actions"
			class="s-page-actions"
		>
			<slot name="actions" />
		</div>
	</header>
</template>

<script setup lang="ts">
defineProps<{
	title: string
}>()
</script>
