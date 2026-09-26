<template>
	<span
		class="s-spinner"
		aria-hidden="true"
		:style="{ width: `${size}px`, height: `${size}px` }"
	/>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
	size?: number
}>(), {
	size: 16,
})
</script>
