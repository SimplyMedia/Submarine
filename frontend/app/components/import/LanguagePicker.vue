<script setup lang="ts">
/** Multi-select of the Language enum for a manual import row's language override. */
import { languageLabel, languageOptions } from '~/utils/settings-labels'
import type { components } from '~/types/api'

type Language = components['schemas']['Language']

const selected = defineModel<Language[]>({ default: () => [] })

function toggle(value: Language, checked: boolean) {
	selected.value = checked ? [...selected.value, value] : selected.value.filter(existing => existing !== value)
}

const summary = computed(() => (selected.value.length === 0 ? 'Unknown' : selected.value.map(language => languageLabel(language)).join(', ')))
</script>

<template>
	<SPopover>
		<template #trigger>
			<button
				type="button"
				class="s-input language-picker-trigger"
			>
				<span class="language-picker-summary">{{ summary }}</span>
				<Icon
					name="lucide:chevron-down"
					aria-hidden="true"
				/>
			</button>
		</template>
		<ul class="language-picker-list">
			<li
				v-for="option in languageOptions"
				:key="option.value"
			>
				<SCheckbox
					:model-value="selected.includes(option.value as Language)"
					:label="option.label"
					@update:model-value="value => toggle(option.value as Language, value)"
				/>
			</li>
		</ul>
	</SPopover>
</template>

<style scoped>
.language-picker-trigger {
	display: flex;
	align-items: center;
	justify-content: space-between;
	gap: 8px;
	width: 180px;
	cursor: pointer;
}

.language-picker-summary {
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.language-picker-list {
	display: flex;
	flex-direction: column;
	gap: 4px;
	width: 200px;
	max-height: 280px;
	overflow-y: auto;
}
</style>
