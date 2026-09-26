<template>
	<STooltip
		:text="tooltipText"
		side="right"
	>
		<NuxtLink
			to="/system/tasks"
			class="task-indicator"
			:class="iconOnly ? 's-btn s-btn-ghost s-btn-icon' : triggerClass"
			:aria-label="iconOnly ? tooltipText : undefined"
		>
			<SSpinner
				v-if="active.length > 0"
				:size="16"
			/>
			<Icon
				v-else
				name="lucide:check-circle-2"
				class="task-indicator-icon"
				aria-hidden="true"
			/>
			<span v-if="!iconOnly">{{ label }}</span>
		</NuxtLink>
	</STooltip>
</template>

<script setup lang="ts">
import { commandLabel } from '~/utils/system-labels'
import { useCommandsStore } from '~/stores/commands'

withDefaults(defineProps<{
	/** Render a compact icon-only trigger. */
	iconOnly?: boolean
	/** Extra classes for the wide trigger, e.g. rail styling. */
	triggerClass?: string
}>(), {
	iconOnly: false,
	triggerClass: '',
})

const commandsStore = useCommandsStore()
const active = computed(() => commandsStore.active)

const label = computed(() => {
	const count = active.value.length
	return count === 0 ? 'No tasks running' : `${count} ${count === 1 ? 'task' : 'tasks'} running`
})

const tooltipText = computed(() => {
	if (active.value.length === 0) {
		return 'No tasks running'
	}
	return active.value.map(command => commandLabel(command.name ?? '')).join(', ')
})
</script>

<style scoped>
.task-indicator {
	display: inline-flex;
	align-items: center;
	gap: 8px;
	text-decoration: none;
	color: inherit;
}

.task-indicator-icon {
	width: 16px;
	height: 16px;
	flex: none;
	color: var(--ok);
}
</style>
