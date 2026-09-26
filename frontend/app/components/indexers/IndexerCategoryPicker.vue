<script setup lang="ts">
/** One level of the standard Newznab category tree, recursing into subCategories. */
import type { components } from '~/types/api'

type IndexerCategoryDto = components['schemas']['IndexerCategoryDto']

defineProps<{
	categories: IndexerCategoryDto[]
}>()

const selected = defineModel<number[]>({ default: () => [] })

function toggle(id: number, value: boolean) {
	selected.value = value ? [...selected.value, id] : selected.value.filter(existing => existing !== id)
}
</script>

<template>
	<ul class="category-picker-list">
		<li
			v-for="category in categories"
			:key="category.id"
		>
			<SCheckbox
				:model-value="selected.includes(category.id)"
				:label="`${category.name} (${category.id})`"
				@update:model-value="value => toggle(category.id, value)"
			/>
			<IndexerCategoryPicker
				v-if="category.subCategories.length > 0"
				v-model="selected"
				:categories="category.subCategories"
				class="category-picker-children"
			/>
		</li>
	</ul>
</template>

<style scoped>
.category-picker-list {
	display: flex;
	flex-direction: column;
	gap: 4px;
}

.category-picker-children {
	margin-left: 24px;
	margin-top: 4px;
}
</style>
