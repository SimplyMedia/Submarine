<script setup lang="ts">
import { healthTypeLabel, healthTypeTone } from '~/utils/system-labels'
import { formatBytes, formatDateTime } from '~/composables/useFormat'
import { navChildren } from '~/navigation'
import type { components } from '~/types/api'

useHead({ title: 'System' })

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
		loadError.value = 'Could not load system status. Check your connection and try again.'
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
		parts.push(`${days}d`)
	}
	if (days > 0 || hours > 0) {
		parts.push(`${hours}h`)
	}
	parts.push(`${minutes}m`)
	return parts.join(' ')
})

const diskRows = computed(() => {
	if (!diskSpace.value) {
		return []
	}
	const rows = [{ label: 'Application data', ...diskSpace.value.appData }]
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
	{ key: 'label', label: 'Location' },
	{ key: 'free', label: 'Free', align: 'right' as const },
	{ key: 'total', label: 'Total', align: 'right' as const },
	{ key: 'usage', label: 'Usage' },
]

const healthColumns = [
	{ key: 'type', label: 'Type' },
	{ key: 'source', label: 'Source' },
	{ key: 'message', label: 'Message' },
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
			toast({ title: 'Restart is taking longer than expected', tone: 'danger' })
			restarting.value = false
		}
	}
	catch {
		toast({ title: 'Could not restart the application', tone: 'danger' })
		restarting.value = false
	}
}

async function confirmShutdown() {
	shuttingDown.value = true
	try {
		const api = useApi()
		await api.POST('/api/v1/system/shutdown')
		shutdownOpen.value = false
		toast({ title: 'Shutting down', description: 'Start the application again to continue.', tone: 'info' })
	}
	catch {
		toast({ title: 'Could not shut down the application', tone: 'danger' })
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
		<SPageHeader title="System">
			<template #actions>
				<SButton
					variant="secondary"
					@click="restartOpen = true"
				>
					Restart
				</SButton>
				<SButton
					variant="danger"
					@click="shutdownOpen = true"
				>
					Shut down
				</SButton>
			</template>
		</SPageHeader>

		<SubNav
			label="System"
			:items="navChildren('system')"
		/>

		<div
			v-if="restarting"
			class="status-restarting"
		>
			<SSpinner :size="16" />
			<span>Restarting, waiting for the application to come back…</span>
		</div>

		<template v-else-if="loadError">
			<SEmptyState
				:message="loadError"
				icon="lucide:alert-triangle"
			>
				<template #action>
					<SButton @click="loadAll">
						Retry
					</SButton>
				</template>
			</SEmptyState>
		</template>

		<template v-else-if="loading">
			<SSkeleton height="200px" />
		</template>
		<template v-else-if="status">
			<SSection title="Overview">
				<dl class="fact-grid">
					<div class="fact-row">
						<dt>Version</dt>
						<dd>{{ status.version }}</dd>
					</div>
					<div class="fact-row">
						<dt>Uptime</dt>
						<dd>{{ uptimeText }}</dd>
					</div>
					<div class="fact-row">
						<dt>Started</dt>
						<dd>{{ formatDateTime(status.startTime) }}</dd>
					</div>
					<div class="fact-row">
						<dt>Operating system</dt>
						<dd>{{ status.os }}</dd>
					</div>
					<div class="fact-row">
						<dt>Runtime</dt>
						<dd>{{ status.runtime }}</dd>
					</div>
					<div class="fact-row">
						<dt>Application data</dt>
						<dd>{{ status.appData }}</dd>
					</div>
					<div class="fact-row">
						<dt>Database provider</dt>
						<dd>{{ status.databaseProvider }}</dd>
					</div>
					<div class="fact-row">
						<dt>URL base</dt>
						<dd>{{ status.urlBase || '/' }}</dd>
					</div>
					<div class="fact-row">
						<dt>Authentication</dt>
						<dd>{{ status.authMethod === 'NONE' ? 'API key only' : 'Sign-in required' }}</dd>
					</div>
					<div class="fact-row">
						<dt>Running in Docker</dt>
						<dd>{{ status.isDocker ? 'Yes' : 'No' }}</dd>
					</div>
				</dl>
			</SSection>

			<SSection title="Disk space">
				<STable
					:columns="diskColumns"
					:rows="diskRows"
					:row-key="row => row.label"
				>
					<template #cell-free="{ row }">
						{{ row.freeBytes != null ? formatBytes(row.freeBytes) : 'Unknown' }}
					</template>
					<template #cell-total="{ row }">
						{{ row.totalBytes != null ? formatBytes(row.totalBytes) : 'Unknown' }}
					</template>
					<template #cell-usage="{ row }">
						<SProgress
							v-if="row.freeBytes != null && row.totalBytes != null"
							:value="usagePercent(row.freeBytes, row.totalBytes)"
							:label="`${row.label} usage`"
						/>
						<span
							v-else
							class="s-cell-muted"
						>Unknown</span>
					</template>
				</STable>
			</SSection>

			<SSection title="Health issues">
				<STable
					:columns="healthColumns"
					:rows="healthIssues"
					:row-key="row => row.id"
				>
					<template #empty>
						<SEmptyState
							message="No health issues. Everything looks healthy."
							icon="lucide:heart-pulse"
						/>
					</template>
					<template #cell-type="{ row }">
						<SBadge :tone="healthTypeTone(row.type)">
							{{ healthTypeLabel(row.type) }}
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
							Learn more
						</a>
					</template>
				</STable>
			</SSection>
		</template>

		<ConfirmDialog
			v-model="restartOpen"
			title="Restart Submarine?"
			description="The application will stop and start again. This takes a few seconds."
			confirm-label="Restart"
			:busy="restarting"
			@confirm="confirmRestart"
		/>
		<ConfirmDialog
			v-model="shutdownOpen"
			title="Shut down Submarine?"
			description="The application will stop and will not restart on its own. You will need to start it again manually."
			confirm-label="Shut down"
			danger
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
