<template>
	<TabsRoot v-model="model">
		<TabsList
			class="s-tabs"
			:aria-label="label"
		>
			<TabsTrigger
				v-for="tab in tabs"
				:key="tab.value"
				:value="tab.value"
				class="s-tab"
			>
				{{ tab.label }}
			</TabsTrigger>
		</TabsList>
		<TabsContent
			v-for="tab in tabs"
			:key="tab.value"
			:value="tab.value"
			class="s-tabpanel"
		>
			<slot :name="`panel-${tab.value}`" />
		</TabsContent>
	</TabsRoot>
</template>

<script setup lang="ts">
import { TabsContent, TabsList, TabsRoot, TabsTrigger } from 'reka-ui'
import type { TabEntry } from '~/types/ui'

withDefaults(defineProps<{
	tabs: TabEntry[]
	/** Accessible name for the tab list. */
	label?: string
}>(), {
	label: undefined,
})

const model = defineModel<string>()
</script>
