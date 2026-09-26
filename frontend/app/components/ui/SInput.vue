<template>
	<input
		v-model="model"
		:type="type"
		:placeholder="placeholder"
		:disabled="disabled"
		:aria-invalid="invalid || undefined"
		class="s-input"
		:class="{ 's-invalid': invalid }"
	>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
	type?: 'text' | 'password' | 'email' | 'number' | 'search' | 'url'
	placeholder?: string
	disabled?: boolean
	invalid?: boolean
}>(), {
	type: 'text',
	placeholder: undefined,
	disabled: false,
	invalid: false,
})

const model = defineModel<string>({ default: '' })
</script>
