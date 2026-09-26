<template>
	<textarea
		v-model="model"
		:rows="rows"
		:placeholder="placeholder"
		:disabled="disabled"
		:aria-invalid="invalid || undefined"
		class="s-input s-textarea"
		:class="{ 's-invalid': invalid }"
	/>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
	rows?: number
	placeholder?: string
	disabled?: boolean
	invalid?: boolean
}>(), {
	rows: 3,
	placeholder: undefined,
	disabled: false,
	invalid: false,
})

const model = defineModel<string>({ default: '' })
</script>
