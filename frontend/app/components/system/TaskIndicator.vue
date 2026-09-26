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
import { useI18n } from 'vue-i18n'
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
const { t } = useI18n()
const active = computed(() => commandsStore.active)

const label = computed(() => {
	const count = active.value.length
	return count === 0
		? t('components.system.TaskIndicator.noTasksRunning')
		: t('components.system.TaskIndicator.tasksRunning', { count }, count)
})

const tooltipText = computed(() => {
	if (active.value.length === 0) {
		return t('components.system.TaskIndicator.noTasksRunning')
	}
	return active.value.map(command => t(commandLabel(command.name ?? ''))).join(', ')
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
