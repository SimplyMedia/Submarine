<script setup lang="ts">
import { historyEventTypeLabel, qualityLabel } from '~/utils/library-labels'
import { historyEventTypeOptions } from '~/utils/activity-labels'
import { languageLabel } from '~/utils/settings-labels'
import { formatDateTime, formatRelative } from '~/composables/useFormat'
import { toApiError, useApi } from '~/composables/useApi'
import { navChildren } from '~/navigation'
import type { components } from '~/types/api'

type HistoryEvent = components['schemas']['HistoryEventDto']
type HistoryEventType = NonNullable<components['schemas']['HistoryEventType']>

useHead({ title: 'History' })

const { toast } = useToast()

const items = ref<HistoryEvent[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = 25
const loading = ref(true)
const loadError = ref('')
const eventType = ref<HistoryEventType | 'ALL'>('ALL')
const search = ref('')
const expandedId = ref<number | null>(null)
const failingId = ref<number | null>(null)

const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize)))

const eventTypeFilterOptions = [{ value: 'ALL', label: 'All events' }, ...historyEventTypeOptions]

async function load() {
	loading.value = true
	loadError.value = ''
	const api = useApi()
	const result = await api.GET('/api/v1/history', {
		params: {
			query: {
				Page: page.value,
				PageSize: pageSize,
				eventType: eventType.value === 'ALL' ? undefined : eventType.value,
				q: search.value || undefined,
			},
		},
	})
	if (!result.data) {
		loadError.value = 'Could not load history. Check your connection and try again.'
	}
	items.value = result.data?.items ?? []
	totalCount.value = result.data?.totalCount ?? 0
	loading.value = false
}

function setPage(next: number) {
	page.value = Math.min(Math.max(1, next), totalPages.value)
	void load()
}

watch([eventType, search], () => {
	page.value = 1
	void load()
})

function mediaTo(row: HistoryEvent): string | null {
	if (row.seriesId != null) {
		return `/series/${row.seriesId}`
	}
	if (row.movieId != null) {
		return `/movies/${row.movieId}`
	}
	return null
}

function mediaLabel(row: HistoryEvent): string {
	if (row.seriesTitle) {
		return row.episodeTitle ? `${row.seriesTitle}: ${row.episodeTitle}` : row.seriesTitle
	}
	return row.movieTitle ?? 'None'
}

function toggleExpanded(id: number) {
	expandedId.value = expandedId.value === id ? null : id
}

async function markFailed(row: HistoryEvent) {
	failingId.value = row.id
	try {
		const api = useApi()
		const result = await api.POST('/api/v1/history/failed/{id}', { params: { path: { id: row.id } } })
		if (!result.response.ok) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: 'Marked as failed', tone: 'ok' })
		await load()
	}
	catch (error) {
		toast({ title: 'Could not mark as failed', description: (error as Error).message, tone: 'danger' })
	}
	finally {
		failingId.value = null
	}
}

const columns = [
	{ key: 'type', label: 'Event' },
	{ key: 'media', label: 'Media' },
	{ key: 'sourceTitle', label: 'Source title' },
	{ key: 'quality', label: 'Quality' },
	{ key: 'date', label: 'Date' },
	{ key: 'actions', label: '', align: 'right' as const },
]

onMounted(() => {
	void load()
})
</script>

<template>
	<div>
		<SPageHeader title="History" />
		<SubNav
			label="Activity"
			:items="navChildren('activity')"
		/>

		<div class="history-toolbar">
			<SSelect
				:model-value="eventType"
				:options="eventTypeFilterOptions"
				@update:model-value="eventType = $event as HistoryEventType | 'ALL'"
			/>
			<SInput
				v-model="search"
				type="search"
				placeholder="Search source title"
			/>
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
		<SSpinner v-else-if="loading && items.length === 0" />
		<STable
			v-else
			:columns="columns"
			:rows="items"
			:row-key="(row) => row.id"
		>
			<template #empty>
				<SEmptyState
					message="No history yet."
					icon="lucide:history"
				/>
			</template>
			<template #cell-type="{ row }">
				<SBadge :tone="row.type === 'FAILED' ? 'danger' : row.type === 'GRABBED' ? 'info' : row.type === 'IMPORTED' || row.type === 'UPGRADED' ? 'ok' : 'neutral'">
					{{ historyEventTypeLabel(row.type as never) }}
				</SBadge>
			</template>
			<template #cell-media="{ row }">
				<NuxtLink
					v-if="mediaTo(row)"
					:to="mediaTo(row)!"
				>
					{{ mediaLabel(row) }}
				</NuxtLink>
				<span
					v-else
					class="s-cell-muted"
				>None</span>
			</template>
			<template #cell-sourceTitle="{ row }">
				<div class="history-source-cell">
					<span>{{ row.sourceTitle }}</span>
					<SButton
						v-if="row.data || row.downloadId"
						variant="ghost"
						size="sm"
						@click="toggleExpanded(row.id)"
					>
						{{ expandedId === row.id ? 'Hide details' : 'Show details' }}
					</SButton>
				</div>
				<div
					v-if="expandedId === row.id"
					class="history-details"
				>
					<span v-if="row.downloadId">Download id: {{ row.downloadId }}</span>
					<span v-if="row.languages && row.languages.length > 0">Languages: {{ row.languages.map(l => languageLabel(l)).join(', ') }}</span>
					<pre v-if="row.data">{{ row.data }}</pre>
				</div>
			</template>
			<template #cell-quality="{ row }">
				{{ qualityLabel(row.quality) }}
			</template>
			<template #cell-date="{ row }">
				<STooltip :text="formatDateTime(row.date)">
					<span>{{ formatRelative(row.date) }}</span>
				</STooltip>
			</template>
			<template #cell-actions="{ row }">
				<SButton
					v-if="row.type === 'GRABBED'"
					variant="secondary"
					size="sm"
					:loading="failingId === row.id"
					@click="markFailed(row)"
				>
					Mark as failed
				</SButton>
			</template>
		</STable>

		<div
			v-if="totalPages > 1"
			class="history-pagination"
		>
			<SButton
				variant="secondary"
				size="sm"
				:disabled="page <= 1"
				@click="setPage(page - 1)"
			>
				Previous
			</SButton>
			<span>Page {{ page }} of {{ totalPages }} ({{ totalCount }} total)</span>
			<SButton
				variant="secondary"
				size="sm"
				:disabled="page >= totalPages"
				@click="setPage(page + 1)"
			>
				Next
			</SButton>
		</div>
	</div>
</template>

<style scoped>
.history-toolbar {
	display: flex;
	gap: 12px;
	margin-bottom: 16px;
	max-width: 480px;
}

.history-source-cell {
	display: flex;
	align-items: center;
	gap: 8px;
}

.history-details {
	display: flex;
	flex-direction: column;
	gap: 4px;
	margin-top: 6px;
	font-size: 0.8125rem;
	color: var(--fg-muted);
}

.history-details pre {
	white-space: pre-wrap;
	word-break: break-word;
}

.history-pagination {
	display: flex;
	align-items: center;
	gap: 12px;
	margin-top: 16px;
	font-size: 0.875rem;
	color: var(--fg-muted);
}
</style>
