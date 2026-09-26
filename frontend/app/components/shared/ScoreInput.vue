<template>
	<input
		type="number"
		class="s-input score-input"
		:value="modelValue"
		@input="emit('update:modelValue', ($event.target as HTMLInputElement).valueAsNumber || 0)"
	>
</template>

<script setup lang="ts">
defineProps<{
	modelValue: number
}>()

const emit = defineEmits<{ 'update:modelValue': [number] }>()
</script>

<style scoped>
.score-input {
	width: 88px;
	text-align: right;
}
</style>
