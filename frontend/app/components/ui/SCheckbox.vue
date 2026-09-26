<template>
	<div
		class="s-check"
		:data-disabled="disabled || undefined"
	>
		<CheckboxRoot
			v-bind="$attrs"
			v-model="model"
			:disabled="disabled"
			:aria-labelledby="label ? labelId : undefined"
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
		<span
			v-if="label"
			:id="labelId"
			@click="model = !model"
		>{{ label }}</span>
	</div>
</template>

<script setup lang="ts">
import { CheckboxIndicator, CheckboxRoot } from 'reka-ui'

defineOptions({ inheritAttrs: false })

withDefaults(defineProps<{
	label?: string
	disabled?: boolean
}>(), {
	label: undefined,
	disabled: false,
})

const model = defineModel<boolean>({ default: false })
const labelId = useId()
</script>
