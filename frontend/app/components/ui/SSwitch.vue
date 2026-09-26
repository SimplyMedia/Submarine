<template>
	<label
		class="s-check"
		:data-disabled="disabled || undefined"
	>
		<SwitchRoot
			v-model="model"
			:disabled="disabled"
			class="s-switch"
		>
			<SwitchThumb class="s-switch-thumb" />
		</SwitchRoot>
		<span v-if="label">{{ label }}</span>
	</label>
</template>

<script setup lang="ts">
import { SwitchRoot, SwitchThumb } from 'reka-ui'

withDefaults(defineProps<{
	label?: string
	disabled?: boolean
}>(), {
	label: undefined,
	disabled: false,
})

const model = defineModel<boolean>({ default: false })
</script>
