<template>
	<span
		class="s-skeleton"
		aria-hidden="true"
		:style="{ width, height }"
	/>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
	width?: string
	height?: string
}>(), {
	width: '100%',
	height: '16px',
})
</script>
