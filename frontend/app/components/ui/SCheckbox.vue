<template>
	<label
		class="s-check"
		:data-disabled="disabled || undefined"
	>
		<CheckboxRoot
			v-model="model"
			:disabled="disabled"
			class="s-checkbox"
		>
			<CheckboxIndicator>
				<Icon
					name="lucide:check"
					class="s-checkbox-icon"
					aria-hidden="true"
				/>
			</CheckboxIndicator>
		</CheckboxRoot>
		<span v-if="label">{{ label }}</span>
	</label>
</template>

<script setup lang="ts">
import { CheckboxIndicator, CheckboxRoot } from 'reka-ui'

withDefaults(defineProps<{
	label?: string
	disabled?: boolean
}>(), {
	label: undefined,
	disabled: false,
})

const model = defineModel<boolean>({ default: false })
</script>
