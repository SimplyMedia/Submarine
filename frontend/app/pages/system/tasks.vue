<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { commandLabel, commandStatusLabel, commandStatusTone, commandTriggerLabel } from '~/utils/system-labels'
import { formatDateTime, formatDuration, formatRelative } from '~/composables/useFormat'
import { useCommandsStore } from '~/stores/commands'
import { navChildren } from '~/navigation'
import type { components } from '~/types/api'

const { t } = useI18n()
useHead({ title: t('pages.system.tasks.title') })

type ScheduledTask = components['schemas']['ScheduledTask']

const { toast } = useToast()
const commandsStore = useCommandsStore()

const scheduledTasks = ref<ScheduledTask[]>([])
const loadingTasks = ref(true)
const loadError = ref('')
const runningTaskNames = ref<Record<string, boolean>>({})
const expandedException = ref<number | null>(null)

async function loadScheduledTasks() {
	loadingTasks.value = true
	loadError.value = ''
	const api = useApi()
	const result = await api.GET('/api/v1/scheduled-tasks', { params: { query: { PageSize: 100 } } })
	if (!result.data) {
		loadError.value = t('pages.system.tasks.loadFailed')
	}
	scheduledTasks.value = result.data?.items ?? []
	loadingTasks.value = false
}

async function runNow(task: ScheduledTask) {
	if (!task.id || !task.name) {
		return
	}
	runningTaskNames.value = { ...runningTaskNames.value, [task.name]: true }
	try {
		const api = useApi()
		const result = await api.POST('/api/v1/scheduled-tasks/{id}/run', { params: { path: { id: task.id } } })
		if (result.data) {
			commandsStore.upsert(result.data)
		}
	}
	catch {
		toast({ title: t('pages.system.tasks.runFailed', { task: t(commandLabel(task.name)) }), tone: 'danger' })
	}
	finally {
		runningTaskNames.value = { ...runningTaskNames.value, [task.name]: false }
	}
}

async function cancelCommand(id: number) {
	try {
		await commandsStore.cancel(id)
	}
	catch {
		toast({ title: t('pages.system.tasks.cancelFailed'), tone: 'danger' })
	}
}

function toggleException(id: number) {
	expandedException.value = expandedException.value === id ? null : id
}

const taskColumns = [
	{ key: 'name', label: t('pages.system.tasks.task') },
	{ key: 'interval', label: t('pages.system.tasks.interval') },
	{ key: 'lastRun', label: t('pages.system.tasks.lastRun') },
	{ key: 'nextRun', label: t('pages.system.tasks.nextRun') },
	{ key: 'run', label: '' },
]

const historyColumns = [
	{ key: 'name', label: t('pages.system.tasks.task') },
	{ key: 'trigger', label: t('pages.system.tasks.trigger') },
	{ key: 'status', label: t('pages.system.tasks.status') },
	{ key: 'started', label: t('pages.system.tasks.started') },
	{ key: 'duration', label: t('pages.system.tasks.duration'), align: 'right' as const },
]

function durationText(command: components['schemas']['Command']): string {
	if (!command.startedAt || !command.endedAt) {
		return t('pages.system.tasks.none')
	}
	const minutes = (new Date(command.endedAt).getTime() - new Date(command.startedAt).getTime()) / 60_000
	return minutes < 1 ? t('pages.system.tasks.lessThanMinute') : formatDuration(minutes)
}

onMounted(() => {
	void loadScheduledTasks()
	void commandsStore.load()
})
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.system.tasks.title')" />
		<SubNav
			:label="t('pages.system.tasks.systemNav')"
			:items="navChildren('system')"
		/>

		<SSection :title="t('pages.system.tasks.scheduledTasks')">
			<SEmptyState
				v-if="loadError"
				:message="loadError"
				icon="lucide:alert-triangle"
			>
				<template #action>
					<SButton @click="loadScheduledTasks">
						{{ t('pages.system.tasks.retry') }}
					</SButton>
				</template>
			</SEmptyState>
			<SSkeleton
				v-else-if="loadingTasks"
				height="200px"
			/>
			<STable
				v-else
				:columns="taskColumns"
				:rows="scheduledTasks"
				:row-key="row => row.id ?? row.name ?? ''"
			>
				<template #cell-name="{ row }">
					{{ t(commandLabel(row.name ?? '')) }}
				</template>
				<template #cell-interval="{ row }">
					{{ formatDuration(row.intervalMinutes ?? 0) }}
				</template>
				<template #cell-lastRun="{ row }">
					{{ row.lastRun ? formatRelative(row.lastRun) : t('pages.system.tasks.never') }}
				</template>
				<template #cell-nextRun="{ row }">
					<span v-if="row.nextRun">{{ formatRelative(row.nextRun) }}</span>
					<span
						v-else
						class="s-cell-muted"
					>{{ t('pages.system.tasks.none') }}</span>
				</template>
				<template #cell-run="{ row }">
					<SButton
						size="sm"
						:loading="runningTaskNames[row.name ?? '']"
						@click="runNow(row)"
					>
						{{ t('pages.system.tasks.runNow') }}
					</SButton>
				</template>
			</STable>
		</SSection>

		<SSection :title="t('pages.system.tasks.runningAndQueued')">
			<SEmptyState
				v-if="commandsStore.active.length === 0"
				:message="t('pages.system.tasks.noActiveCommands')"
				icon="lucide:list-checks"
			/>
			<div v-else>
				<CommandProgress
					v-for="command in commandsStore.active"
					:key="command.id"
					:command="command"
					@cancel="cancelCommand(command.id!)"
				/>
			</div>
		</SSection>

		<SSection :title="t('pages.system.tasks.history')">
			<STable
				:columns="historyColumns"
				:rows="commandsStore.history"
				:row-key="row => row.id ?? ''"
			>
				<template #empty>
					<SEmptyState
						:message="t('pages.system.tasks.noCommandHistory')"
						icon="lucide:history"
					/>
				</template>
				<template #cell-name="{ row }">
					<div class="history-name-cell">
						<span>{{ t(commandLabel(row.name ?? '')) }}</span>
						<SButton
							v-if="row.exception"
							variant="ghost"
							size="sm"
							@click="toggleException(row.id!)"
						>
							{{ t(expandedException === row.id ? 'pages.system.tasks.hideError' : 'pages.system.tasks.showError') }}
						</SButton>
					</div>
					<pre
						v-if="row.exception && expandedException === row.id"
						class="history-exception"
					>{{ row.exception }}</pre>
				</template>
				<template #cell-trigger="{ row }">
					{{ t(commandTriggerLabel(row.trigger ?? '')) }}
				</template>
				<template #cell-status="{ row }">
					<SBadge :tone="commandStatusTone(row.status ?? '')">
						{{ t(commandStatusLabel(row.status ?? '')) }}
					</SBadge>
				</template>
				<template #cell-started="{ row }">
					<span v-if="row.startedAt">{{ formatDateTime(row.startedAt) }}</span>
					<span
						v-else
						class="s-cell-muted"
					>{{ t('pages.system.tasks.none') }}</span>
				</template>
				<template #cell-duration="{ row }">
					{{ durationText(row) }}
				</template>
			</STable>
		</SSection>
	</div>
</template>

<style scoped>
.history-name-cell {
	display: flex;
	align-items: center;
	gap: 8px;
}

.history-exception {
	margin-top: 8px;
	padding: 12px;
	border-radius: var(--r-control);
	background: var(--surface-2);
	font-size: 0.75rem;
	white-space: pre-wrap;
	overflow-wrap: anywhere;
}
</style>
