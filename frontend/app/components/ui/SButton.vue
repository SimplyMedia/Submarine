<template>
	<button
		:type="type"
		:disabled="busy"
		:aria-busy="loading || undefined"
		class="s-btn"
		:class="[`s-btn-${variant}`, size === 'sm' ? 's-btn-sm' : '', iconOnly ? 's-btn-icon' : '']"
	>
		<SSpinner
			v-if="loading"
			:size="size === 'sm' ? 12 : 16"
		/>
		<slot />
	</button>
</template>

<script setup lang="ts">
const props = withDefaults(defineProps<{
	variant?: 'primary' | 'secondary' | 'ghost' | 'danger'
	size?: 'sm' | 'md'
	loading?: boolean
	disabled?: boolean
	type?: 'button' | 'submit'
	/** Square button holding only an icon; pass the accessible name via aria-label. */
	iconOnly?: boolean
}>(), {
	variant: 'secondary',
	size: 'md',
	loading: false,
	disabled: false,
	type: 'button',
	iconOnly: false,
})

const busy = computed(() => props.disabled || props.loading)
</script>
