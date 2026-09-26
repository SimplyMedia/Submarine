<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { LOG_LEVELS, logLevelLabel } from '~/utils/system-labels'
import { formatBytes, formatDateTime } from '~/composables/useFormat'
import { useLogsStore } from '~/stores/logs'
import { navChildren } from '~/navigation'

const { t } = useI18n()
useHead({ title: t('pages.system.logs.title') })

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
	{ value: 'table', label: t('pages.system.logs.logs') },
	{ value: 'files', label: t('pages.system.logs.logFiles') },
]

const columns = [
	{ key: 'time', label: t('pages.system.logs.time') },
	{ key: 'level', label: t('pages.system.logs.level') },
	{ key: 'logger', label: t('pages.system.logs.logger') },
	{ key: 'message', label: t('pages.system.logs.message') },
]

const fileColumns = [
	{ key: 'name', label: t('pages.system.logs.file') },
	{ key: 'size', label: t('pages.system.logs.size'), align: 'right' as const },
	{ key: 'lastModified', label: t('pages.system.logs.lastModified') },
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
		toast({ title: t('pages.system.logs.cleared'), tone: 'ok' })
	}
	catch (error) {
		toast({ title: t('pages.system.logs.clearFailed'), description: error instanceof Error ? error.message : undefined, tone: 'danger' })
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
		<SPageHeader :title="t('pages.system.logs.title')">
			<template #actions>
				<SButton
					variant="danger"
					@click="clearConfirmOpen = true"
				>
					{{ t('pages.system.logs.clearLogs') }}
				</SButton>
			</template>
		</SPageHeader>

		<SubNav
			:label="t('pages.system.logs.systemNav')"
			:items="navChildren('system')"
		/>

		<STabs
			v-model="activeTab"
			:tabs="tabs"
			:label="t('pages.system.logs.logsView')"
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
							{{ t(logLevelLabel(lvl), lvl) }}
						</button>
					</div>
					<form
						class="logs-search"
						@submit.prevent="submitSearch"
					>
						<SInput
							v-model="searchInput"
							type="search"
							:placeholder="t('pages.system.logs.searchPlaceholder')"
						/>
						<SButton
							type="submit"
							size="sm"
						>
							{{ t('pages.system.logs.search') }}
						</SButton>
					</form>
					<SSwitch
						:model-value="autoRefresh"
						:label="t('pages.system.logs.refreshEvery10s')"
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
							:message="t('pages.system.logs.noMatchingEntries')"
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
								{{ t(expandedId === row.id ? 'pages.system.logs.hideException' : 'pages.system.logs.showException') }}
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
						{{ t('pages.system.logs.previous') }}
					</SButton>
					<span>{{ t('pages.system.logs.page', { current: logsStore.page, total: logsStore.totalPages }) }}</span>
					<SButton
						variant="secondary"
						size="sm"
						:disabled="logsStore.page >= logsStore.totalPages"
						@click="logsStore.setPage(logsStore.page + 1); logsStore.load()"
					>
						{{ t('pages.system.logs.next') }}
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
							:message="t('pages.system.logs.noLogFiles')"
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
							<SIconButton :label="t('pages.system.logs.downloadLogFile')">
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
			:title="t('pages.system.logs.clearHistoryTitle')"
			:description="t('pages.system.logs.clearHistoryDescription')"
			:confirm-label="t('pages.system.logs.clearLogs')"
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
