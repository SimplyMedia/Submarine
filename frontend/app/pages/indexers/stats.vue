<script setup lang="ts">
import { navChildren } from '~/navigation'
import { useApi } from '~/composables/useApi'
import type { components } from '~/types/api'

type IndexerStatEntry = components['schemas']['IndexerStatEntry']
type UserAgentStatEntry = components['schemas']['UserAgentStatEntry']

definePageMeta({ layout: 'default' })
useHead({ title: 'Stats' })

const api = useApi()

const rangeDays = ref(30)
const indexers = ref<IndexerStatEntry[]>([])
const userAgents = ref<UserAgentStatEntry[]>([])
const loading = ref(true)
const loadError = ref('')

const maxQueries = computed(() => Math.max(1, ...indexers.value.map(entry => entry.queryCount)))

async function load() {
	loading.value = true
	loadError.value = ''
	const end = new Date()
	const start = new Date(end.getTime() - rangeDays.value * 86_400_000)
	const result = await api.GET('/api/v1/indexer-stats', {
		params: { query: { start: start.toISOString(), end: end.toISOString() } },
	})
	if (!result.data) {
		loadError.value = 'Could not load stats. Check your connection and try again.'
	}
	indexers.value = result.data?.indexers ?? []
	userAgents.value = result.data?.userAgents ?? []
	loading.value = false
}

function setRange(days: number) {
	rangeDays.value = days
	void load()
}

const columns = [
	{ key: 'indexerName', label: 'Indexer' },
	{ key: 'queries', label: 'Queries' },
	{ key: 'grabCount', label: 'Grabs', align: 'right' as const },
	{ key: 'failureCount', label: 'Failures', align: 'right' as const },
	{ key: 'averageResponseMs', label: 'Avg response', align: 'right' as const },
]

const userAgentColumns = [
	{ key: 'userAgent', label: 'Caller' },
	{ key: 'queryCount', label: 'Queries', align: 'right' as const },
	{ key: 'grabCount', label: 'Grabs', align: 'right' as const },
]

onMounted(() => {
	void load()
})
</script>

<template>
	<div>
		<SPageHeader title="Stats" />
		<SubNav
			label="Indexers"
			:items="navChildren('indexers')"
		/>

		<div class="stats-range-row">
			<SButton
				v-for="preset in [7, 30, 90]"
				:key="preset"
				size="sm"
				:variant="rangeDays === preset ? 'primary' : 'secondary'"
				@click="setRange(preset)"
			>
				{{ preset }} days
			</SButton>
		</div>

		<SEmptyState
			v-if="loadError"
			:message="loadError"
			icon="lucide:alert-triangle"
		>
			<template #action>
				<SButton @click="load">
					Retry
				</SButton>
			</template>
		</SEmptyState>
		<SSpinner v-else-if="loading" />
		<template v-else>
			<SSection title="Indexers">
				<STable
					:columns="columns"
					:rows="indexers"
					:row-key="(row) => row.indexerId"
				>
					<template #empty>
						<SEmptyState message="No indexer activity in this range yet." />
					</template>
					<template #cell-queries="{ row }">
						<div class="stats-bar-cell">
							<div class="stats-bar-track">
								<div
									class="stats-bar-fill"
									:style="{ width: `${(row.queryCount / maxQueries) * 100}%` }"
								/>
							</div>
							<span>{{ row.queryCount }}</span>
						</div>
					</template>
					<template #cell-averageResponseMs="{ row }">
						{{ Math.round(row.averageResponseMs) }} ms
					</template>
				</STable>
			</SSection>

			<SSection title="Outbound Newznab API callers">
				<STable
					:columns="userAgentColumns"
					:rows="userAgents"
					:row-key="(row) => row.userAgent"
				>
					<template #empty>
						<SEmptyState message="No external app has queried this instance yet." />
					</template>
				</STable>
			</SSection>
		</template>
	</div>
</template>

<style scoped>
.stats-range-row {
	display: flex;
	gap: 8px;
	margin-bottom: 16px;
}

.stats-bar-cell {
	display: flex;
	align-items: center;
	gap: 8px;
}

.stats-bar-track {
	width: 120px;
	height: 6px;
	border-radius: 3px;
	background: var(--surface-2);
	overflow: hidden;
}

.stats-bar-fill {
	height: 100%;
	background: var(--accent);
}
</style>
