<template>
	<SDropdownMenu :items="items">
		<template #trigger>
			<button
				v-if="!iconOnly"
				type="button"
				:class="triggerClass"
				aria-label="Theme"
			>
				<Icon
					:name="currentIcon"
					class="theme-menu-icon"
					aria-hidden="true"
				/>
				<span>Theme</span>
			</button>
			<SIconButton
				v-else
				label="Theme"
			>
				<Icon
					:name="currentIcon"
					aria-hidden="true"
				/>
			</SIconButton>
		</template>
	</SDropdownMenu>
</template>

<script setup lang="ts">
import type { MenuEntryOrSeparator } from '~/types/ui'

withDefaults(defineProps<{
	/** Render a compact icon-only trigger. */
	iconOnly?: boolean
	/** Extra classes for the wide trigger button, e.g. rail styling. */
	triggerClass?: string
}>(), {
	iconOnly: false,
	triggerClass: '',
})

const colorMode = useColorMode()

const currentIcon = computed(() => {
	if (colorMode.preference === 'light') {
		return 'lucide:sun'
	}
	if (colorMode.preference === 'dark') {
		return 'lucide:moon'
	}
	return 'lucide:monitor'
})

const items = computed<MenuEntryOrSeparator[]>(() => [
	{ label: 'Light', icon: 'lucide:sun', disabled: colorMode.preference === 'light', onSelect: () => setPreference('light') },
	{ label: 'Dark', icon: 'lucide:moon', disabled: colorMode.preference === 'dark', onSelect: () => setPreference('dark') },
	{ label: 'System', icon: 'lucide:monitor', disabled: colorMode.preference === 'system', onSelect: () => setPreference('system') },
])

function setPreference(value: 'light' | 'dark' | 'system') {
	colorMode.preference = value
}
</script>

<style scoped>
.theme-menu-icon {
	width: 16px;
	height: 16px;
	flex: none;
}
</style>
