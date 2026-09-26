<template>
	<div class="s-field">
		<label
			v-if="label"
			:for="controlId"
			class="s-field-label"
		>{{ label }}</label>
		<slot />
		<p
			v-if="error"
			class="s-field-error"
			role="alert"
		>
			{{ error }}
		</p>
		<p
			v-else-if="hint"
			class="s-field-hint"
		>
			{{ hint }}
		</p>
	</div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
	label?: string
	hint?: string
	error?: string
	/** Id of the wrapped control, wired to the label. */
	controlId?: string
}>(), {
	label: undefined,
	hint: undefined,
	error: undefined,
	controlId: undefined,
})
</script>
