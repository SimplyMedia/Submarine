<script setup lang="ts">
import { formatDate } from '~/composables/useFormat'
import { protocolLabel } from '~/utils/settings-labels'
import { toApiError, useApi } from '~/composables/useApi'
import { navChildren } from '~/navigation'
import type { components } from '~/types/api'

type BlocklistItem = components['schemas']['BlocklistItemDto']

useHead({ title: 'Blocklist' })

const { toast } = useToast()

const items = ref<BlocklistItem[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = 25
const loading = ref(true)
const loadError = ref('')
const selected = ref<number[]>([])
const clearAllOpen = ref(false)
const clearing = ref(false)
const removing = ref(false)

const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize)))
const allSelected = computed(() => items.value.length > 0 && items.value.every(row => selected.value.includes(row.id)))

async function load() {
	loading.value = true
	loadError.value = ''
	const api = useApi()
	const result = await api.GET('/api/v1/blocklist', { params: { query: { Page: page.value, PageSize: pageSize } } })
	if (!result.data) {
		loadError.value = 'Could not load the blocklist. Check your connection and try again.'
	}
	items.value = result.data?.items ?? []
	totalCount.value = result.data?.totalCount ?? 0
	loading.value = false
}

function setPage(next: number) {
	page.value = Math.min(Math.max(1, next), totalPages.value)
	void load()
}

function toggleRow(id: number, value: boolean) {
	selected.value = value ? [...selected.value, id] : selected.value.filter(existing => existing !== id)
}

function toggleAll(value: boolean) {
	selected.value = value ? items.value.map(row => row.id) : []
}

async function removeOne(id: number) {
	removing.value = true
	try {
		const api = useApi()
		const result = await api.DELETE('/api/v1/blocklist/{id}', { params: { path: { id } } })
		if (!result.response.ok) {
			throw toApiError(result.error, result.response)
		}
		await load()
	}
	catch (error) {
		toast({ title: 'Could not remove blocklist entry', description: (error as Error).message, tone: 'danger' })
	}
	finally {
		removing.value = false
	}
}

async function removeSelected() {
	if (selected.value.length === 0) {
		return
	}
	removing.value = true
	try {
		const api = useApi()
		const result = await api.DELETE('/api/v1/blocklist/bulk', { body: { ids: selected.value } })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		selected.value = []
		await load()
	}
	catch (error) {
		toast({ title: 'Could not remove selected entries', description: (error as Error).message, tone: 'danger' })
	}
	finally {
		removing.value = false
	}
}

async function clearAll() {
	clearing.value = true
	try {
		const api = useApi()
		const result = await api.DELETE('/api/v1/blocklist/all')
		if (!result.response.ok) {
			throw toApiError(result.error, result.response)
		}
		selected.value = []
		clearAllOpen.value = false
		page.value = 1
		await load()
	}
	catch (error) {
		toast({ title: 'Could not clear the blocklist', description: (error as Error).message, tone: 'danger' })
	}
	finally {
		clearing.value = false
	}
}

function mediaTo(row: BlocklistItem): string | null {
	if (row.seriesId != null) {
		return `/series/${row.seriesId}`
	}
	if (row.movieId != null) {
		return `/movies/${row.movieId}`
	}
	return null
}

const columns = [
	{ key: 'select', label: '' },
	{ key: 'releaseTitle', label: 'Title' },
	{ key: 'media', label: 'Media' },
	{ key: 'protocol', label: 'Protocol' },
	{ key: 'indexerName', label: 'Indexer' },
	{ key: 'reason', label: 'Reason' },
	{ key: 'date', label: 'Date' },
	{ key: 'actions', label: '', align: 'right' as const },
]

onMounted(() => {
	void load()
})
</script>

<template>
	<div>
		<SPageHeader title="Blocklist">
			<template #actions>
				<SButton
					variant="danger"
					size="sm"
					:disabled="items.length === 0"
					@click="clearAllOpen = true"
				>
					Clear all
				</SButton>
			</template>
		</SPageHeader>
		<SubNav
			label="Activity"
			:items="navChildren('activity')"
		/>

		<div class="blocklist-toolbar">
			<SCheckbox
				:model-value="allSelected"
				label="Select all on this page"
				@update:model-value="toggleAll"
			/>
			<SButton
				variant="danger"
				size="sm"
				:disabled="selected.length === 0"
				:loading="removing"
				@click="removeSelected"
			>
				Remove selected ({{ selected.length }})
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
		<SSpinner v-else-if="loading && items.length === 0" />
		<STable
			v-else
			:columns="columns"
			:rows="items"
			:row-key="(row) => row.id"
		>
			<template #empty>
				<SEmptyState
					message="Nothing is blocklisted."
					icon="lucide:shield-check"
				/>
			</template>
			<template #cell-select="{ row }">
				<SCheckbox
					:model-value="selected.includes(row.id)"
					:aria-label="`Select ${row.releaseTitle}`"
					@update:model-value="value => toggleRow(row.id, value)"
				/>
			</template>
			<template #cell-media="{ row }">
				<NuxtLink
					v-if="mediaTo(row)"
					:to="mediaTo(row)!"
				>
					{{ row.seriesTitle ?? row.movieTitle }}
				</NuxtLink>
				<span
					v-else
					class="s-cell-muted"
				>None</span>
			</template>
			<template #cell-protocol="{ row }">
				{{ protocolLabel(row.protocol) }}
			</template>
			<template #cell-indexerName="{ row }">
				<span v-if="row.indexerName">{{ row.indexerName }}</span>
				<span
					v-else
					class="s-cell-muted"
				>None</span>
			</template>
			<template #cell-date="{ row }">
				{{ formatDate(row.date) }}
			</template>
			<template #cell-actions="{ row }">
				<SIconButton
					label="Remove from blocklist"
					@click="removeOne(row.id)"
				>
					<Icon
						name="lucide:x"
						aria-hidden="true"
					/>
				</SIconButton>
			</template>
		</STable>

		<div
			v-if="totalPages > 1"
			class="blocklist-pagination"
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

		<SDialog
			v-model="clearAllOpen"
			title="Clear the blocklist"
		>
			<p>Remove every blocklisted release? This cannot be undone.</p>
			<template #footer>
				<SButton
					variant="secondary"
					:disabled="clearing"
					@click="clearAllOpen = false"
				>
					Cancel
				</SButton>
				<SButton
					variant="danger"
					:loading="clearing"
					@click="clearAll"
				>
					Clear all
				</SButton>
			</template>
		</SDialog>
	</div>
</template>

<style scoped>
.blocklist-toolbar {
	display: flex;
	align-items: center;
	justify-content: space-between;
	margin-bottom: 16px;
}

.blocklist-pagination {
	display: flex;
	align-items: center;
	gap: 12px;
	margin-top: 16px;
	font-size: 0.875rem;
	color: var(--fg-muted);
}
</style>
