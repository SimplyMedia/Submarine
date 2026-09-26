<script setup lang="ts">
import { commandLabel, commandStatusLabel, commandStatusTone, commandTriggerLabel } from '~/utils/system-labels'
import { formatDateTime, formatDuration, formatRelative } from '~/composables/useFormat'
import { useCommandsStore } from '~/stores/commands'
import { navChildren } from '~/navigation'
import type { components } from '~/types/api'

useHead({ title: 'Tasks' })

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
		loadError.value = 'Could not load scheduled tasks. Check your connection and try again.'
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
		toast({ title: `Could not run ${commandLabel(task.name)}`, tone: 'danger' })
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
		toast({ title: 'Could not cancel the command', tone: 'danger' })
	}
}

function toggleException(id: number) {
	expandedException.value = expandedException.value === id ? null : id
}

const taskColumns = [
	{ key: 'name', label: 'Task' },
	{ key: 'interval', label: 'Interval' },
	{ key: 'lastRun', label: 'Last run' },
	{ key: 'nextRun', label: 'Next run' },
	{ key: 'run', label: '' },
]

const historyColumns = [
	{ key: 'name', label: 'Task' },
	{ key: 'trigger', label: 'Trigger' },
	{ key: 'status', label: 'Status' },
	{ key: 'started', label: 'Started' },
	{ key: 'duration', label: 'Duration', align: 'right' as const },
]

function durationText(command: components['schemas']['Command']): string {
	if (!command.startedAt || !command.endedAt) {
		return 'None'
	}
	const minutes = (new Date(command.endedAt).getTime() - new Date(command.startedAt).getTime()) / 60_000
	return minutes < 1 ? '< 1m' : formatDuration(minutes)
}

onMounted(() => {
	void loadScheduledTasks()
	void commandsStore.load()
})
</script>

<template>
	<div>
		<SPageHeader title="Tasks" />
		<SubNav
			label="System"
			:items="navChildren('system')"
		/>

		<SSection title="Scheduled tasks">
			<SEmptyState
				v-if="loadError"
				:message="loadError"
				icon="lucide:alert-triangle"
			>
				<template #action>
					<SButton @click="loadScheduledTasks">
						Retry
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
					{{ commandLabel(row.name ?? '') }}
				</template>
				<template #cell-interval="{ row }">
					{{ formatDuration(row.intervalMinutes ?? 0) }}
				</template>
				<template #cell-lastRun="{ row }">
					{{ row.lastRun ? formatRelative(row.lastRun) : 'Never' }}
				</template>
				<template #cell-nextRun="{ row }">
					<span v-if="row.nextRun">{{ formatRelative(row.nextRun) }}</span>
					<span
						v-else
						class="s-cell-muted"
					>None</span>
				</template>
				<template #cell-run="{ row }">
					<SButton
						size="sm"
						:loading="runningTaskNames[row.name ?? '']"
						@click="runNow(row)"
					>
						Run now
					</SButton>
				</template>
			</STable>
		</SSection>

		<SSection title="Running and queued">
			<SEmptyState
				v-if="commandsStore.active.length === 0"
				message="No commands are running or queued right now."
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

		<SSection title="History">
			<STable
				:columns="historyColumns"
				:rows="commandsStore.history"
				:row-key="row => row.id ?? ''"
			>
				<template #empty>
					<SEmptyState
						message="Completed and failed commands will show up here."
						icon="lucide:history"
					/>
				</template>
				<template #cell-name="{ row }">
					<div class="history-name-cell">
						<span>{{ commandLabel(row.name ?? '') }}</span>
						<SButton
							v-if="row.exception"
							variant="ghost"
							size="sm"
							@click="toggleException(row.id!)"
						>
							{{ expandedException === row.id ? 'Hide error' : 'Show error' }}
						</SButton>
					</div>
					<pre
						v-if="row.exception && expandedException === row.id"
						class="history-exception"
					>{{ row.exception }}</pre>
				</template>
				<template #cell-trigger="{ row }">
					{{ commandTriggerLabel(row.trigger ?? '') }}
				</template>
				<template #cell-status="{ row }">
					<SBadge :tone="commandStatusTone(row.status ?? '')">
						{{ commandStatusLabel(row.status ?? '') }}
					</SBadge>
				</template>
				<template #cell-started="{ row }">
					<span v-if="row.startedAt">{{ formatDateTime(row.startedAt) }}</span>
					<span
						v-else
						class="s-cell-muted"
					>None</span>
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
