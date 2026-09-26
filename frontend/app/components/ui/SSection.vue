<template>
	<section class="s-section">
		<h2
			v-if="title"
			class="s-section-title"
		>
			{{ title }}
		</h2>
		<slot />
	</section>
</template>

<script setup lang="ts">
defineProps<{
	title?: string
}>()
</script>
