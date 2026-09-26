<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { formatDate } from '~/composables/useFormat'
import { protocolLabel } from '~/utils/settings-labels'
import { toApiError, useApi } from '~/composables/useApi'
import { navChildren } from '~/navigation'
import type { components } from '~/types/api'

type BlocklistItem = components['schemas']['BlocklistItemDto']

const { t } = useI18n()
useHead({ title: t('pages.activity.blocklist.title') })

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
		loadError.value = t('pages.activity.blocklist.loadFailed')
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
		toast({ title: t('pages.activity.blocklist.removeFailed'), description: (error as Error).message, tone: 'danger' })
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
		toast({ title: t('pages.activity.blocklist.removeSelectedFailed'), description: (error as Error).message, tone: 'danger' })
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
		toast({ title: t('pages.activity.blocklist.clearFailed'), description: (error as Error).message, tone: 'danger' })
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
	{ key: 'releaseTitle', label: t('pages.activity.blocklist.titleColumn') },
	{ key: 'media', label: t('pages.activity.blocklist.media') },
	{ key: 'protocol', label: t('pages.activity.blocklist.protocol') },
	{ key: 'indexerName', label: t('pages.activity.blocklist.indexer') },
	{ key: 'reason', label: t('pages.activity.blocklist.reason') },
	{ key: 'date', label: t('pages.activity.blocklist.date') },
	{ key: 'actions', label: '', align: 'right' as const },
]

onMounted(() => {
	void load()
})
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.activity.blocklist.title')">
			<template #actions>
				<SButton
					variant="danger"
					size="sm"
					:disabled="items.length === 0"
					@click="clearAllOpen = true"
				>
					{{ t('pages.activity.blocklist.clearAll') }}
				</SButton>
			</template>
		</SPageHeader>
		<SubNav
			:label="t('pages.activity.blocklist.activityNav')"
			:items="navChildren('activity')"
		/>

		<div class="blocklist-toolbar">
			<SCheckbox
				:model-value="allSelected"
				:label="t('pages.activity.blocklist.selectAll')"
				@update:model-value="toggleAll"
			/>
			<SButton
				variant="danger"
				size="sm"
				:disabled="selected.length === 0"
				:loading="removing"
				@click="removeSelected"
			>
				{{ t('pages.activity.blocklist.removeSelected', { count: selected.length }) }}
			</SButton>
		</div>

		<SEmptyState
			v-if="loadError"
			:message="loadError"
			icon="lucide:alert-triangle"
		>
			<template #action>
				<SButton @click="load">
					{{ t('pages.activity.blocklist.retry') }}
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
					:message="t('pages.activity.blocklist.emptyState')"
					icon="lucide:shield-check"
				/>
			</template>
			<template #cell-select="{ row }">
				<SCheckbox
					:model-value="selected.includes(row.id)"
					:aria-label="t('pages.activity.blocklist.selectItem', { title: row.releaseTitle })"
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
				>{{ t('pages.activity.blocklist.none') }}</span>
			</template>
			<template #cell-protocol="{ row }">
				{{ t(protocolLabel(row.protocol)) }}
			</template>
			<template #cell-indexerName="{ row }">
				<span v-if="row.indexerName">{{ row.indexerName }}</span>
				<span
					v-else
					class="s-cell-muted"
				>{{ t('pages.activity.blocklist.none') }}</span>
			</template>
			<template #cell-date="{ row }">
				{{ formatDate(row.date) }}
			</template>
			<template #cell-actions="{ row }">
				<SIconButton
					:label="t('pages.activity.blocklist.removeFromBlocklist')"
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
				{{ t('pages.activity.blocklist.previous') }}
			</SButton>
			<span>{{ t('pages.activity.blocklist.page', { page, totalPages, totalCount }) }}</span>
			<SButton
				variant="secondary"
				size="sm"
				:disabled="page >= totalPages"
				@click="setPage(page + 1)"
			>
				{{ t('pages.activity.blocklist.next') }}
			</SButton>
		</div>

		<SDialog
			v-model="clearAllOpen"
			:title="t('pages.activity.blocklist.clearConfirmTitle')"
		>
			<p>{{ t('pages.activity.blocklist.clearConfirmDescription') }}</p>
			<template #footer>
				<SButton
					variant="secondary"
					:disabled="clearing"
					@click="clearAllOpen = false"
				>
					{{ t('pages.activity.blocklist.cancel') }}
				</SButton>
				<SButton
					variant="danger"
					:loading="clearing"
					@click="clearAll"
				>
					{{ t('pages.activity.blocklist.clearAll') }}
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
