<template>
	<div
		class="s-progress"
		role="progressbar"
		:aria-label="label"
		:aria-valuemin="0"
		:aria-valuemax="100"
		:aria-valuenow="clamped"
	>
		<div
			class="s-progress-bar"
			:style="{ width: `${clamped}%` }"
		/>
	</div>
</template>

<script setup lang="ts">
const props = withDefaults(defineProps<{
	value: number
	/** Accessible name for screen readers. */
	label?: string
}>(), {
	label: undefined,
})

const clamped = computed(() => Math.min(100, Math.max(0, Math.round(props.value))))
</script>
