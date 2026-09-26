<script setup lang="ts">
/** Bookmark toggle for monitored/unmonitored state; caller owns persistence. */
withDefaults(defineProps<{
	modelValue: boolean
	label: string
	disabled?: boolean
}>(), {
	disabled: false,
})

const emit = defineEmits<{
	'update:modelValue': [value: boolean]
}>()
</script>

<template>
	<button
		type="button"
		class="monitor-toggle"
		:class="{ 'monitor-toggle-on': modelValue }"
		:disabled="disabled"
		:aria-pressed="modelValue"
		:aria-label="label"
		@click="emit('update:modelValue', !modelValue)"
	>
		<Icon
			:name="modelValue ? 'lucide:bookmark-check' : 'lucide:bookmark'"
			aria-hidden="true"
		/>
	</button>
</template>

<style scoped>
.monitor-toggle {
	display: inline-flex;
	align-items: center;
	justify-content: center;
	width: 28px;
	height: 28px;
	border: none;
	border-radius: var(--r-control);
	background: transparent;
	color: var(--fg-faint);
	cursor: pointer;
}

.monitor-toggle:hover:not(:disabled) {
	background: var(--surface-2);
	color: var(--fg-muted);
}

.monitor-toggle-on {
	color: var(--accent);
}

.monitor-toggle:disabled {
	cursor: default;
	opacity: 0.6;
}
</style>
