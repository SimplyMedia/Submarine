<template>
	<button
		type="button"
		:aria-label="label"
		:disabled="disabled"
		class="s-btn"
		:class="[`s-btn-${variant}`, size === 'sm' ? 's-btn-sm s-btn-icon' : 's-btn-icon']"
	>
		<slot />
	</button>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
	/** Accessible name describing the action. */
	label: string
	variant?: 'ghost' | 'secondary' | 'primary' | 'danger'
	size?: 'sm' | 'md'
	disabled?: boolean
}>(), {
	variant: 'ghost',
	size: 'md',
	disabled: false,
})
</script>
