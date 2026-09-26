<template>
	<SelectRoot v-model="model">
		<SelectTrigger
			:id="controlId"
			:disabled="disabled"
			:aria-invalid="invalid || undefined"
			class="s-input s-select-trigger"
			:class="{ 's-invalid': invalid }"
		>
			<SelectValue :placeholder="placeholder" />
			<Icon
				name="lucide:chevron-down"
				class="s-select-chevron"
				aria-hidden="true"
			/>
		</SelectTrigger>
		<SelectContent
			position="popper"
			:side-offset="4"
			class="s-menu"
		>
			<SelectItem
				v-for="option in options"
				:key="option.value"
				:value="option.value"
				class="s-menu-item"
			>
				<SelectItemText>{{ option.label }}</SelectItemText>
			</SelectItem>
		</SelectContent>
	</SelectRoot>
</template>

<script setup lang="ts">
import {
	SelectContent,
	SelectItem,
	SelectItemText,
	SelectRoot,
	SelectTrigger,
	SelectValue,
} from 'reka-ui'
import type { SelectOption } from '~/types/ui'

withDefaults(defineProps<{
	options: SelectOption[]
	placeholder?: string
	disabled?: boolean
	invalid?: boolean
	controlId?: string
}>(), {
	placeholder: undefined,
	disabled: false,
	invalid: false,
	controlId: undefined,
})

const model = defineModel<string>()
</script>

<style scoped>
.s-select-trigger {
	display: flex;
	align-items: center;
	justify-content: space-between;
	gap: 8px;
	width: 100%;
	min-width: 0;
	text-align: left;
	cursor: pointer;
}

.s-select-trigger[data-disabled] {
	cursor: default;
}

.s-select-trigger > span {
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.s-select-chevron {
	width: 16px;
	height: 16px;
	flex: none;
	color: var(--fg-muted);
}
</style>
