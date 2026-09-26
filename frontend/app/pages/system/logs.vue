<script setup lang="ts">
import { LOG_LEVELS } from '~/utils/system-labels'
import { formatBytes, formatDateTime } from '~/composables/useFormat'
import { useLogsStore } from '~/stores/logs'
import { navChildren } from '~/navigation'

useHead({ title: 'Logs' })

const { toast } = useToast()
const logsStore = useLogsStore()

const activeTab = ref('table')
const searchInput = ref('')
const autoRefresh = ref(false)
const clearConfirmOpen = ref(false)
const clearing = ref(false)
const expandedId = ref<number | null>(null)

let refreshTimer: ReturnType<typeof setInterval> | undefined

const tabs = [
	{ value: 'table', label: 'Logs' },
	{ value: 'files', label: 'Log files' },
]

const columns = [
	{ key: 'time', label: 'Time' },
	{ key: 'level', label: 'Level' },
	{ key: 'logger', label: 'Logger' },
	{ key: 'message', label: 'Message' },
]

const fileColumns = [
	{ key: 'name', label: 'File' },
	{ key: 'size', label: 'Size', align: 'right' as const },
	{ key: 'lastModified', label: 'Last modified' },
	{ key: 'download', label: '' },
]

function selectLevel(level: string) {
	logsStore.setFilter(logsStore.level === level ? '' : level, logsStore.query)
	void logsStore.load()
}

function submitSearch() {
	logsStore.setFilter(logsStore.level, searchInput.value)
	void logsStore.load()
}

function toggleException(id: number) {
	expandedId.value = expandedId.value === id ? null : id
}

async function confirmClear() {
	clearing.value = true
	try {
		await logsStore.clear()
		toast({ title: 'Log history cleared', tone: 'ok' })
	}
	catch (error) {
		toast({ title: 'Could not clear the logs', description: error instanceof Error ? error.message : undefined, tone: 'danger' })
	}
	finally {
		clearing.value = false
		clearConfirmOpen.value = false
	}
}

function setAutoRefresh(value: boolean) {
	autoRefresh.value = value
	clearInterval(refreshTimer)
	if (value) {
		refreshTimer = setInterval(() => {
			void logsStore.load()
		}, 10_000)
	}
}

onMounted(() => {
	void logsStore.load()
	void logsStore.loadFiles()
})

onUnmounted(() => {
	clearInterval(refreshTimer)
})
</script>

<template>
	<div>
		<SPageHeader title="Logs">
			<template #actions>
				<SButton
					variant="danger"
					@click="clearConfirmOpen = true"
				>
					Clear logs
				</SButton>
			</template>
		</SPageHeader>

		<SubNav
			label="System"
			:items="navChildren('system')"
		/>

		<STabs
			v-model="activeTab"
			:tabs="tabs"
			label="Logs view"
		>
			<template #panel-table>
				<div class="logs-toolbar">
					<div class="logs-chips">
						<button
							v-for="lvl in LOG_LEVELS"
							:key="lvl"
							type="button"
							class="s-badge logs-chip"
							:class="logsStore.level === lvl ? 's-badge-info' : 's-badge-neutral'"
							@click="selectLevel(lvl)"
						>
							{{ lvl }}
						</button>
					</div>
					<form
						class="logs-search"
						@submit.prevent="submitSearch"
					>
						<SInput
							v-model="searchInput"
							type="search"
							placeholder="Search message or logger"
						/>
						<SButton
							type="submit"
							size="sm"
						>
							Search
						</SButton>
					</form>
					<SSwitch
						:model-value="autoRefresh"
						label="Refresh every 10s"
						@update:model-value="setAutoRefresh"
					/>
				</div>

				<STable
					:columns="columns"
					:rows="logsStore.entries"
					:row-key="row => row.id"
				>
					<template #empty>
						<SEmptyState
							message="No log entries match your filters."
							icon="lucide:scroll-text"
						/>
					</template>
					<template #cell-time="{ row }">
						{{ formatDateTime(row.time) }}
					</template>
					<template #cell-level="{ row }">
						<LogLevelBadge :level="row.level" />
					</template>
					<template #cell-message="{ row }">
						<div class="logs-message-cell">
							<span>{{ row.message }}</span>
							<SButton
								v-if="row.exception"
								variant="ghost"
								size="sm"
								@click="toggleException(row.id)"
							>
								{{ expandedId === row.id ? 'Hide exception' : 'Show exception' }}
							</SButton>
						</div>
						<pre
							v-if="row.exception && expandedId === row.id"
							class="logs-exception"
						>{{ row.exception }}</pre>
					</template>
				</STable>

				<div
					v-if="logsStore.totalPages > 1"
					class="logs-pagination"
				>
					<SButton
						variant="secondary"
						size="sm"
						:disabled="logsStore.page <= 1"
						@click="logsStore.setPage(logsStore.page - 1); logsStore.load()"
					>
						Previous
					</SButton>
					<span>Page {{ logsStore.page }} of {{ logsStore.totalPages }}</span>
					<SButton
						variant="secondary"
						size="sm"
						:disabled="logsStore.page >= logsStore.totalPages"
						@click="logsStore.setPage(logsStore.page + 1); logsStore.load()"
					>
						Next
					</SButton>
				</div>
			</template>

			<template #panel-files>
				<STable
					:columns="fileColumns"
					:rows="logsStore.files"
					:row-key="row => row.name"
				>
					<template #empty>
						<SEmptyState
							message="No rolling log files on disk yet."
							icon="lucide:folder"
						/>
					</template>
					<template #cell-size="{ row }">
						{{ formatBytes(row.size) }}
					</template>
					<template #cell-lastModified="{ row }">
						{{ formatDateTime(row.lastModified) }}
					</template>
					<template #cell-download="{ row }">
						<a
							:href="`${baseUrl()}/api/v1/logs/files/${row.name}`"
							download
						>
							<SIconButton label="Download log file">
								<Icon
									name="lucide:download"
									aria-hidden="true"
								/>
							</SIconButton>
						</a>
					</template>
				</STable>
			</template>
		</STabs>

		<ConfirmDialog
			v-model="clearConfirmOpen"
			title="Clear log history?"
			description="This deletes every stored log entry. Rolling log files on disk are not affected."
			confirm-label="Clear logs"
			danger
			:busy="clearing"
			@confirm="confirmClear"
		/>
	</div>
</template>

<style scoped>
.logs-toolbar {
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	gap: 16px;
	margin-bottom: 16px;
}

.logs-chips {
	display: flex;
	flex-wrap: wrap;
	gap: 6px;
}

.logs-chip {
	border: 0;
	cursor: pointer;
	font: inherit;
}

.logs-search {
	display: flex;
	gap: 8px;
	flex: 1;
	min-width: 220px;
}

.logs-message-cell {
	display: flex;
	align-items: center;
	gap: 8px;
	flex-wrap: wrap;
}

.logs-exception {
	margin-top: 8px;
	padding: 12px;
	border-radius: var(--r-control);
	background: var(--surface-2);
	font-size: 0.75rem;
	white-space: pre-wrap;
	overflow-wrap: anywhere;
}

.logs-pagination {
	display: flex;
	align-items: center;
	justify-content: center;
	gap: 16px;
	padding-top: 16px;
	color: var(--fg-muted);
}
</style>
