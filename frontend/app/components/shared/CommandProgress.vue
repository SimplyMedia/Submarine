<template>
	<div class="command-progress">
		<div class="command-progress-head">
			<div class="command-progress-title">
				<span class="command-progress-name">{{ t(commandLabel(command.name ?? '')) }}</span>
				<span class="command-progress-trigger">{{ t(commandTriggerLabel(command.trigger ?? '')) }}</span>
			</div>
			<div class="command-progress-actions">
				<SBadge :tone="commandStatusTone(command.status ?? '')">
					{{ t(commandStatusLabel(command.status ?? '')) }}
				</SBadge>
				<SIconButton
					v-if="cancellable"
					:label="$t('components.shared.CommandProgress.cancelCommand')"
					variant="ghost"
					size="sm"
					@click="$emit('cancel')"
				>
					<Icon
						name="lucide:x"
						aria-hidden="true"
					/>
				</SIconButton>
			</div>
		</div>
		<SProgress
			v-if="showProgress"
			:value="command.progress ?? 0"
			:label="t('components.shared.CommandProgress.progress', { command: t(commandLabel(command.name ?? '')) })"
		/>
		<p
			v-if="command.message"
			class="command-progress-message"
		>
			{{ command.message }}
		</p>
	</div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { commandLabel, commandStatusLabel, commandStatusTone, commandTriggerLabel } from '~/utils/system-labels'
import type { Command } from '~/stores/commands'

const props = defineProps<{
	command: Command
}>()

const { t } = useI18n()

defineEmits<{
	cancel: []
}>()

const showProgress = computed(() => props.command.status === 'QUEUED' || props.command.status === 'RUNNING')
const cancellable = computed(() => showProgress.value)
</script>

<style scoped>
.command-progress {
	display: flex;
	flex-direction: column;
	gap: 8px;
	padding: 12px 0;
	border-bottom: 1px solid var(--line);
}

.command-progress:last-child {
	border-bottom: 0;
}

.command-progress-head {
	display: flex;
	align-items: center;
	justify-content: space-between;
	gap: 12px;
}

.command-progress-title {
	display: flex;
	align-items: baseline;
	gap: 8px;
	min-width: 0;
}

.command-progress-name {
	font-weight: 500;
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.command-progress-trigger {
	font-size: 0.75rem;
	color: var(--fg-muted);
	flex: none;
}

.command-progress-actions {
	display: flex;
	align-items: center;
	gap: 8px;
	flex: none;
}

.command-progress-message {
	font-size: 0.8125rem;
	color: var(--fg-muted);
}
</style>
