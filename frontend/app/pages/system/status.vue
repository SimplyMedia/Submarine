<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { healthTypeLabel, healthTypeTone } from '~/utils/system-labels'
import { formatBytes, formatDateTime } from '~/composables/useFormat'
import { navChildren } from '~/navigation'
import type { components } from '~/types/api'

const { t } = useI18n()
useHead({ title: t('pages.system.status.title') })

type DiskSpaceReport = components['schemas']['DiskSpaceReportDto']
type HealthIssue = components['schemas']['HealthIssueDto']

const { toast } = useToast()

const status = ref<components['schemas']['SystemStatusDto'] | null>(null)
const diskSpace = ref<DiskSpaceReport | null>(null)
const healthIssues = ref<HealthIssue[]>([])
const loading = ref(true)
const loadError = ref('')

const now = ref(Date.now())
let clock: ReturnType<typeof setInterval> | undefined

const restartOpen = ref(false)
const shutdownOpen = ref(false)
const restarting = ref(false)
const shuttingDown = ref(false)

async function loadAll() {
	loading.value = true
	loadError.value = ''
	const api = useApi()
	const [statusResult, diskResult, healthResult] = await Promise.all([
		api.GET('/api/v1/system/status'),
		api.GET('/api/v1/system/disk-space'),
		api.GET('/api/v1/health'),
	])
	if (statusResult.data) {
		status.value = statusResult.data
	}
	if (diskResult.data) {
		diskSpace.value = diskResult.data
	}
	if (healthResult.data) {
		healthIssues.value = healthResult.data
	}
	if (!statusResult.data || !diskResult.data || !healthResult.data) {
		loadError.value = t('pages.system.status.loadFailed')
	}
	loading.value = false
}

const uptimeText = computed(() => {
	if (!status.value) {
		return ''
	}
	const totalMinutes = Math.max(0, Math.floor((now.value - new Date(status.value.startTime).getTime()) / 60_000))
	const days = Math.floor(totalMinutes / 1440)
	const hours = Math.floor((totalMinutes % 1440) / 60)
	const minutes = totalMinutes % 60
	const parts: string[] = []
	if (days > 0) {
		parts.push(t('pages.system.status.daysShort', { count: days }))
	}
	if (days > 0 || hours > 0) {
		parts.push(t('pages.system.status.hoursShort', { count: hours }))
	}
	parts.push(t('pages.system.status.minutesShort', { count: minutes }))
	return parts.join(' ')
})

const diskRows = computed(() => {
	if (!diskSpace.value) {
		return []
	}
	const rows = [{ label: t('pages.system.status.applicationData'), ...diskSpace.value.appData }]
	for (const folder of diskSpace.value.rootFolders) {
		rows.push({ label: folder.path, ...folder })
	}
	return rows
})

function usagePercent(freeBytes: number | null, totalBytes: number | null): number {
	if (freeBytes === null || totalBytes === null || totalBytes === 0) {
		return 0
	}
	return Math.round(((totalBytes - freeBytes) / totalBytes) * 100)
}

const diskColumns = [
	{ key: 'label', label: t('pages.system.status.location') },
	{ key: 'free', label: t('pages.system.status.free'), align: 'right' as const },
	{ key: 'total', label: t('pages.system.status.total'), align: 'right' as const },
	{ key: 'usage', label: t('pages.system.status.usage') },
]

const healthColumns = [
	{ key: 'type', label: t('pages.system.status.type') },
	{ key: 'source', label: t('pages.system.status.source') },
	{ key: 'message', label: t('pages.system.status.message') },
	{ key: 'wiki', label: '' },
]

async function pollReady(): Promise<boolean> {
	const config = useRuntimeConfig()
	const base = (config.app.baseURL || '/').replace(/\/+$/, '')
	for (let attempt = 0; attempt < 60; attempt++) {
		await new Promise(resolve => setTimeout(resolve, 1000))
		try {
			const response = await fetch(`${base}/_status/ready`)
			if (response.ok) {
				return true
			}
		}
		catch {
			// Not up yet, keep polling.
		}
	}
	return false
}

async function confirmRestart() {
	restarting.value = true
	try {
		const api = useApi()
		await api.POST('/api/v1/system/restart')
		restartOpen.value = false
		const ready = await pollReady()
		if (ready) {
			window.location.reload()
		}
		else {
			toast({ title: t('pages.system.status.restartSlow'), tone: 'danger' })
			restarting.value = false
		}
	}
	catch {
		toast({ title: t('pages.system.status.restartFailed'), tone: 'danger' })
		restarting.value = false
	}
}

async function confirmShutdown() {
	shuttingDown.value = true
	try {
		const api = useApi()
		await api.POST('/api/v1/system/shutdown')
		shutdownOpen.value = false
		toast({ title: t('pages.system.status.shuttingDown'), description: t('pages.system.status.startAgain'), tone: 'info' })
	}
	catch {
		toast({ title: t('pages.system.status.shutdownFailed'), tone: 'danger' })
		shuttingDown.value = false
	}
}

onMounted(() => {
	void loadAll()
	clock = setInterval(() => {
		now.value = Date.now()
	}, 60_000)
})

onUnmounted(() => {
	clearInterval(clock)
})
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.system.status.title')">
			<template #actions>
				<SButton
					variant="secondary"
					@click="restartOpen = true"
				>
					{{ t('pages.system.status.restart') }}
				</SButton>
				<SButton
					variant="danger"
					@click="shutdownOpen = true"
				>
					{{ t('pages.system.status.shutDown') }}
				</SButton>
			</template>
		</SPageHeader>

		<SubNav
			:label="t('pages.system.status.systemNav')"
			:items="navChildren('system')"
		/>

		<div
			v-if="restarting"
			class="status-restarting"
		>
			<SSpinner :size="16" />
			<span>{{ t('pages.system.status.restarting') }}</span>
		</div>

		<template v-else-if="loadError">
			<SEmptyState
				:message="loadError"
				icon="lucide:alert-triangle"
			>
				<template #action>
					<SButton @click="loadAll">
						{{ t('pages.system.status.retry') }}
					</SButton>
				</template>
			</SEmptyState>
		</template>

		<template v-else-if="loading">
			<SSkeleton height="200px" />
		</template>
		<template v-else-if="status">
			<SSection :title="t('pages.system.status.overview')">
				<dl class="fact-grid">
					<div class="fact-row">
						<dt>{{ t('pages.system.status.version') }}</dt>
						<dd>{{ status.version }}</dd>
					</div>
					<div class="fact-row">
						<dt>{{ t('pages.system.status.uptime') }}</dt>
						<dd>{{ uptimeText }}</dd>
					</div>
					<div class="fact-row">
						<dt>{{ t('pages.system.status.started') }}</dt>
						<dd>{{ formatDateTime(status.startTime) }}</dd>
					</div>
					<div class="fact-row">
						<dt>{{ t('pages.system.status.operatingSystem') }}</dt>
						<dd>{{ status.os }}</dd>
					</div>
					<div class="fact-row">
						<dt>{{ t('pages.system.status.runtime') }}</dt>
						<dd>{{ status.runtime }}</dd>
					</div>
					<div class="fact-row">
						<dt>{{ t('pages.system.status.applicationData') }}</dt>
						<dd>{{ status.appData }}</dd>
					</div>
					<div class="fact-row">
						<dt>{{ t('pages.system.status.databaseProvider') }}</dt>
						<dd>{{ status.databaseProvider }}</dd>
					</div>
					<div class="fact-row">
						<dt>{{ t('pages.system.status.urlBase') }}</dt>
						<dd>{{ status.urlBase || '/' }}</dd>
					</div>
					<div class="fact-row">
						<dt>{{ t('pages.system.status.authentication') }}</dt>
						<dd>{{ status.authMethod === 'NONE' ? t('pages.system.status.apiKeyOnly') : t('pages.system.status.signInRequired') }}</dd>
					</div>
					<div class="fact-row">
						<dt>{{ t('pages.system.status.runningInDocker') }}</dt>
						<dd>{{ status.isDocker ? t('pages.system.status.yes') : t('pages.system.status.no') }}</dd>
					</div>
				</dl>
			</SSection>

			<SSection :title="t('pages.system.status.diskSpace')">
				<STable
					:columns="diskColumns"
					:rows="diskRows"
					:row-key="row => row.label"
				>
					<template #cell-free="{ row }">
						{{ row.freeBytes != null ? formatBytes(row.freeBytes) : t('pages.system.status.unknown') }}
					</template>
					<template #cell-total="{ row }">
						{{ row.totalBytes != null ? formatBytes(row.totalBytes) : t('pages.system.status.unknown') }}
					</template>
					<template #cell-usage="{ row }">
						<SProgress
							v-if="row.freeBytes != null && row.totalBytes != null"
							:value="usagePercent(row.freeBytes, row.totalBytes)"
							:label="t('pages.system.status.usageLabel', { label: row.label })"
						/>
						<span
							v-else
							class="s-cell-muted"
						>{{ t('pages.system.status.unknown') }}</span>
					</template>
				</STable>
			</SSection>

			<SSection :title="t('pages.system.status.healthIssues')">
				<STable
					:columns="healthColumns"
					:rows="healthIssues"
					:row-key="row => row.id"
				>
					<template #empty>
						<SEmptyState
							:message="t('pages.system.status.noHealthIssues')"
							icon="lucide:heart-pulse"
						/>
					</template>
					<template #cell-type="{ row }">
						<SBadge :tone="healthTypeTone(row.type)">
							{{ t(healthTypeLabel(row.type)) }}
						</SBadge>
					</template>
					<template #cell-wiki="{ row }">
						<a
							v-if="row.wikiUrl"
							:href="row.wikiUrl"
							target="_blank"
							rel="noopener"
							class="status-wiki-link"
						>
							{{ t('pages.system.status.learnMore') }}
						</a>
					</template>
				</STable>
			</SSection>
		</template>

		<ConfirmDialog
			v-model="restartOpen"
			:title="t('pages.system.status.restartConfirmTitle')"
			:description="t('pages.system.status.restartConfirmDescription')"
			:confirm-label="t('pages.system.status.restart')"
			:busy="restarting"
			@confirm="confirmRestart"
		/>
		<ConfirmDialog
			v-model="shutdownOpen"
			:title="t('pages.system.status.shutdownConfirmTitle')"
			:description="t('pages.system.status.shutdownConfirmDescription')"
			:confirm-label="t('pages.system.status.shutDown')"
			:busy="shuttingDown"
			@confirm="confirmShutdown"
		/>
	</div>
</template>

<style scoped>
.fact-grid {
	display: flex;
	flex-direction: column;
}

.fact-row {
	display: flex;
	justify-content: space-between;
	gap: 24px;
	padding: 10px 0;
	border-bottom: 1px solid var(--line);
}

.fact-row:last-child {
	border-bottom: 0;
}

.fact-row dt {
	color: var(--fg-muted);
}

.fact-row dd {
	font-weight: 500;
	text-align: right;
	overflow-wrap: anywhere;
}

.status-wiki-link {
	color: var(--accent);
}

.status-restarting {
	display: flex;
	align-items: center;
	gap: 12px;
	padding: 48px 0;
	justify-content: center;
	color: var(--fg-muted);
}
</style>
