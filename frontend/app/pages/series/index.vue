<script setup lang="ts">
import type { components } from '~/types/api'
import type { MediaLookupResult, MediaTableColumn, PosterCardBadge, PosterCardItem } from '~/types/ui'
import {
	addMonitorOptionOptions,
	monitorNewItemsOptions,
	seriesNumberingOptions,
	seriesStatusLabel,
	seriesStatusOptions,
	seriesTypeOptions,
} from '~/utils/library-labels'

type SeriesListItem = components['schemas']['SeriesListItemDto']

useHead({ title: 'Series' })

const api = useApi()
const reference = useReferenceStore()
const events = useEvents()
const toast = useToast()

const view = ref<'grid' | 'table'>('grid')
const items = ref<SeriesListItem[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = ref(40)
const loading = ref(true)
const loadError = ref('')

const term = ref('')
const monitoredFilter = ref('any')
const typeFilter = ref('any')
const statusFilter = ref('any')
const tagFilter = ref('any')
const rootFolderFilter = ref('any')
const sortKey = ref('SortTitle')
const sortDirection = ref<'asc' | 'desc'>('asc')

const selectMode = ref(false)
const selected = ref<number[]>([])
const addOpen = ref(false)

const monitoredOptions = [
	{ value: 'any', label: 'Any monitor state' },
	{ value: 'true', label: 'Monitored' },
	{ value: 'false', label: 'Unmonitored' },
]
const typeOptions = [{ value: 'any', label: 'Any type' }, ...seriesTypeOptions]
const statusOptions = [{ value: 'any', label: 'Any status' }, ...seriesStatusOptions]
const sortOptions = [
	{ value: 'SortTitle', label: 'Title' },
	{ value: 'CreatedAt', label: 'Added' },
	{ value: 'Year', label: 'Year' },
	{ value: 'Status', label: 'Status' },
]

const tagOptions = computed(() => [{ value: 'any', label: 'Any tag' }, ...reference.tags.map(tag => ({ value: String(tag.id), label: tag.label }))])
const rootFolderOptions = computed(() => [
	{ value: 'any', label: 'Any root folder' },
	...reference.rootFolders.filter(folder => folder.mediaKind === 'SERIES').map(folder => ({ value: String(folder.id), label: folder.path })),
])
const addRootFolderOptions = computed(() =>
	reference.rootFolders.filter(folder => folder.mediaKind === 'SERIES').map(folder => ({ value: String(folder.id), label: folder.path })),
)

const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize.value)))

const cards = computed<PosterCardItem[]>(() => items.value.map((series) => {
	const badges: PosterCardBadge[] = [{ label: seriesStatusLabel(series.status), tone: series.status === 'CONTINUING' ? 'ok' : 'neutral' }]
	if (!series.monitored) {
		badges.push({ label: 'Unmonitored', tone: 'neutral' as const })
	}
	const total = series.statistics.episodeCount
	return {
		id: series.id,
		to: `/series/${series.id}`,
		posterUrl: series.posterUrl,
		title: series.title,
		meta: series.year ? String(series.year) : undefined,
		metaSecondary: series.network ?? undefined,
		badges,
		progress: total > 0 ? (series.statistics.episodeFileCount / total) * 100 : null,
		progressLabel: `${series.title} episodes on disk`,
	}
}))

interface SeriesRow {
	id: number
	to: string
	posterUrl: string | null
	title: string
	subtitle?: string
	series: SeriesListItem
}

const tableColumns: MediaTableColumn[] = [
	{ key: 'status', label: 'Status' },
	{ key: 'nextAiring', label: 'Next airing' },
	{ key: 'episodes', label: 'Episodes' },
	{ key: 'monitored', label: 'Monitored', align: 'right' },
]

const rows = computed<SeriesRow[]>(() => items.value.map(series => ({
	id: series.id,
	to: `/series/${series.id}`,
	posterUrl: series.posterUrl,
	title: series.title,
	subtitle: series.network ?? undefined,
	series,
})))

let searchTimer: ReturnType<typeof setTimeout>
const stopHandlers: Array<() => void> = []

onMounted(async () => {
	const stored = localStorage.getItem('submarine.series.view')
	if (stored === 'grid' || stored === 'table') {
		view.value = stored
	}
	await reference.load()
	await load()
	stopHandlers.push(events.on('SeriesAddedEvent', () => void load()))
	stopHandlers.push(events.on('SeriesUpdatedEvent', () => void load()))
	stopHandlers.push(events.on('SeriesDeletedEvent', () => void load()))
	if (useRoute().query.add === '1') {
		addOpen.value = true
	}
})

onUnmounted(() => {
	stopHandlers.forEach(stop => stop())
})

watch(view, (value) => {
	localStorage.setItem('submarine.series.view', value)
})

watch(term, () => {
	clearTimeout(searchTimer)
	searchTimer = setTimeout(() => {
		page.value = 1
		void load()
	}, 350)
})

watch([monitoredFilter, typeFilter, statusFilter, tagFilter, rootFolderFilter, sortKey, sortDirection, pageSize], () => {
	if (page.value === 1) {
		void load()
	}
	else {
		page.value = 1
	}
})

watch(page, () => void load())

let loadToken = 0

async function load() {
	const token = ++loadToken
	loading.value = true
	loadError.value = ''
	const result = await api.GET('/api/v1/series', {
		params: {
			query: {
				Page: page.value,
				PageSize: pageSize.value,
				SortKey: sortKey.value,
				SortDirection: sortDirection.value === 'desc' ? 'descending' : 'ascending',
				monitored: monitoredFilter.value === 'any' ? undefined : monitoredFilter.value === 'true',
				type: (typeFilter.value === 'any' ? undefined : typeFilter.value) as SeriesListItem['seriesType'] | undefined,
				status: (statusFilter.value === 'any' ? undefined : statusFilter.value) as SeriesListItem['status'] | undefined,
				tagId: tagFilter.value === 'any' ? undefined : Number(tagFilter.value),
				rootFolderId: rootFolderFilter.value === 'any' ? undefined : Number(rootFolderFilter.value),
				term: term.value.trim() || undefined,
			},
		},
	})
	if (token !== loadToken) {
		return
	}
	if (result.data) {
		items.value = result.data.items
		totalCount.value = result.data.totalCount
	}
	else {
		loadError.value = 'Could not load series. Check your connection and try again.'
	}
	loading.value = false
}

function toggleSort(key: string) {
	if (sortKey.value === key) {
		sortDirection.value = sortDirection.value === 'asc' ? 'desc' : 'asc'
	}
	else {
		sortKey.value = key
		sortDirection.value = 'asc'
	}
}

function toggleSelectMode() {
	selectMode.value = !selectMode.value
	selected.value = []
}

async function searchSeries(term: string): Promise<MediaLookupResult[]> {
	const result = await api.GET('/api/v1/series/lookup', { params: { query: { term } } })
	if (!result.data) {
		return []
	}
	return result.data.map(hit => ({
		key: `${hit.provider}-${hit.tvdbId ?? hit.tmdbId}`,
		title: hit.title,
		year: hit.year,
		overview: hit.overview,
		posterUrl: hit.posterUrl,
		existingId: hit.existingSeriesId,
		tvdbId: hit.tvdbId,
		tmdbId: hit.tmdbId,
		provider: hit.provider,
	}))
}

const addForm = reactive({
	rootFolderId: '',
	seriesType: 'STANDARD' as SeriesListItem['seriesType'],
	numbering: 'AIRED' as SeriesListItem['numbering'],
	seasonFolder: true,
	monitorOption: 'ALL' as NonNullable<components['schemas']['AddMonitorOption']>,
	monitorSpecials: false,
	monitorNewItems: 'ALL' as SeriesListItem['monitorNewItems'],
	searchOnAdd: false,
	tagIds: [] as number[],
})
const addVersions = ref([{ name: 'Main', qualityProfileId: null as number | null, languageProfileId: null as number | null, rootFolderId: null as number | null }])
const adding = ref(false)
const addDialogRef = ref<{ back: () => void } | null>(null)

watch(() => reference.rootFolders, (folders) => {
	const first = folders.find(folder => folder.mediaKind === 'SERIES')
	if (first && !addForm.rootFolderId) {
		addForm.rootFolderId = String(first.id)
	}
}, { immediate: true })

watch(() => reference.qualityProfiles, (profiles) => {
	if (profiles[0] && addVersions.value[0] && addVersions.value[0].qualityProfileId == null) {
		addVersions.value[0].qualityProfileId = profiles[0].id
	}
}, { immediate: true })

watch(() => reference.languageProfiles, (profiles) => {
	if (profiles[0] && addVersions.value[0] && addVersions.value[0].languageProfileId == null) {
		addVersions.value[0].languageProfileId = profiles[0].id
	}
}, { immediate: true })

async function submitAdd(result: MediaLookupResult) {
	if (!addForm.rootFolderId) {
		toast.toast({ title: 'Choose a root folder', tone: 'danger' })
		return
	}
	adding.value = true
	try {
		const created = await api.POST('/api/v1/series', {
			body: {
				tvdbId: result.tvdbId,
				tmdbId: result.tmdbId,
				metadataProvider: result.tvdbId != null ? 'TVDB' : 'TMDB',
				title: result.title,
				rootFolderId: Number(addForm.rootFolderId),
				seriesType: addForm.seriesType,
				numbering: addForm.numbering,
				seasonFolder: addForm.seasonFolder,
				monitored: true,
				monitorOption: addForm.monitorOption,
				monitorSpecials: addForm.monitorSpecials,
				monitorNewItems: addForm.monitorNewItems,
				tagIds: addForm.tagIds,
				versions: addVersions.value.map(version => ({
					name: version.name || null,
					qualityProfileId: version.qualityProfileId ?? reference.qualityProfiles[0]?.id ?? 1,
					languageProfileId: version.languageProfileId ?? reference.languageProfiles[0]?.id ?? 1,
					rootFolderId: version.rootFolderId,
				})),
				searchOnAdd: addForm.searchOnAdd,
			},
		})
		if (created.data) {
			addOpen.value = false
			toast.toast({ title: `${result.title} added`, tone: 'ok' })
			await navigateTo(`/series/${created.data.series.id}`)
		}
		else {
			toast.toast({ title: toApiError(created.error, created.response).message, tone: 'danger' })
		}
	}
	finally {
		adding.value = false
	}
}

const massEditOpen = ref(false)
const massEdit = reactive({
	monitored: 'unchanged',
	seriesType: 'unchanged',
	rootFolderId: 'unchanged',
	moveFiles: false,
	tagMode: 'add' as 'add' | 'remove' | 'replace',
	tagIds: [] as number[],
})
const massEditing = ref(false)

async function applyMassEdit() {
	if (selected.value.length === 0) {
		return
	}
	massEditing.value = true
	try {
		const result = await api.PUT('/api/v1/series/editor', {
			body: {
				ids: selected.value,
				monitored: massEdit.monitored === 'unchanged' ? null : massEdit.monitored === 'true',
				seriesType: (massEdit.seriesType === 'unchanged' ? null : massEdit.seriesType) as SeriesListItem['seriesType'] | null,
				seasonFolder: null,
				monitorNewItems: null,
				qualityProfileId: null,
				languageProfileId: null,
				rootFolderId: massEdit.rootFolderId === 'unchanged' ? null : Number(massEdit.rootFolderId),
				moveFiles: massEdit.rootFolderId === 'unchanged' ? null : massEdit.moveFiles,
				tags: massEdit.tagIds.length > 0 ? { mode: massEdit.tagMode, tagIds: massEdit.tagIds } : null,
			},
		})
		if (result.data) {
			toast.toast({ title: `${result.data.updated} series updated`, tone: 'ok' })
			massEditOpen.value = false
			selectMode.value = false
			selected.value = []
			await load()
		}
		else {
			toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
		}
	}
	finally {
		massEditing.value = false
	}
}

const deleteConfirmOpen = ref(false)
const deleteFiles = ref(false)
const deleteExclusion = ref(false)
const deleting = ref(false)

async function toggleMonitored(series: SeriesListItem, monitored: boolean) {
	series.monitored = monitored
	const result = await api.PUT('/api/v1/series/{id}', {
		params: { path: { id: series.id } },
		body: { monitored, seasonFolder: null, seriesType: null, numbering: null, monitorNewItems: null, tagIds: null, rootFolderId: null, moveFiles: null, versions: null },
	})
	if (!result.data) {
		series.monitored = !monitored
		toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
	}
}

async function confirmDelete() {
	deleting.value = true
	try {
		const result = await api.DELETE('/api/v1/series/editor', {
			body: { ids: selected.value, deleteFiles: deleteFiles.value, addImportListExclusion: deleteExclusion.value },
		})
		if (result.response.ok) {
			toast.toast({ title: `${selected.value.length} series removed`, tone: 'ok' })
			deleteConfirmOpen.value = false
			massEditOpen.value = false
			selectMode.value = false
			selected.value = []
			await load()
		}
		else {
			toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
		}
	}
	finally {
		deleting.value = false
	}
}
</script>

<template>
	<div>
		<SPageHeader title="Series">
			<template #actions>
				<SButton
					v-if="selectMode"
					variant="secondary"
					:disabled="selected.length === 0"
					@click="massEditOpen = true"
				>
					Edit {{ selected.length }} selected
				</SButton>
				<SButton
					variant="secondary"
					@click="toggleSelectMode"
				>
					{{ selectMode ? 'Cancel' : 'Mass edit' }}
				</SButton>
				<SButton
					variant="secondary"
					@click="navigateTo('/library-import')"
				>
					Import existing library
				</SButton>
				<SButton
					variant="primary"
					@click="addOpen = true"
				>
					Add series
				</SButton>
			</template>
		</SPageHeader>

		<div class="library-toolbar">
			<SInput
				v-model="term"
				type="search"
				placeholder="Search series"
			/>
			<SSelect
				v-model="monitoredFilter"
				:options="monitoredOptions"
			/>
			<SSelect
				v-model="typeFilter"
				:options="typeOptions"
			/>
			<SSelect
				v-model="statusFilter"
				:options="statusOptions"
			/>
			<SSelect
				v-model="tagFilter"
				:options="tagOptions"
			/>
			<SSelect
				v-model="rootFolderFilter"
				:options="rootFolderOptions"
			/>
			<SSelect
				v-model="sortKey"
				:options="sortOptions"
			/>
			<SIconButton
				:label="sortDirection === 'asc' ? 'Sort descending' : 'Sort ascending'"
				@click="sortDirection = sortDirection === 'asc' ? 'desc' : 'asc'"
			>
				<Icon
					:name="sortDirection === 'asc' ? 'lucide:arrow-up' : 'lucide:arrow-down'"
					aria-hidden="true"
				/>
			</SIconButton>
			<div class="library-view-toggle">
				<button
					type="button"
					class="library-view-btn"
					:class="{ 'library-view-btn-active': view === 'grid' }"
					aria-label="Poster grid view"
					@click="view = 'grid'"
				>
					<Icon
						name="lucide:layout-grid"
						aria-hidden="true"
					/>
				</button>
				<button
					type="button"
					class="library-view-btn"
					:class="{ 'library-view-btn-active': view === 'table' }"
					aria-label="Table view"
					@click="view = 'table'"
				>
					<Icon
						name="lucide:list"
						aria-hidden="true"
					/>
				</button>
			</div>
		</div>

		<SSpinner v-if="loading && items.length === 0" />
		<p
			v-else-if="loadError"
			class="s-field-error"
			role="alert"
		>
			{{ loadError }}
		</p>
		<template v-else-if="items.length === 0">
			<SEmptyState message="No series match yet. Add a series to start building your library.">
				<template #action>
					<SButton
						variant="primary"
						@click="addOpen = true"
					>
						Add series
					</SButton>
				</template>
			</SEmptyState>
		</template>
		<template v-else>
			<MediaPosterGrid
				v-if="view === 'grid'"
				:items="cards"
				:select-mode="selectMode"
				:selected="selected"
				@update:selected="selected = $event"
			/>
			<MediaTable
				v-else
				:columns="tableColumns"
				:rows="rows"
				:selectable="selectMode"
				:selected="selected"
				:sort-key="sortKey"
				:sort-direction="sortDirection"
				@update:selected="selected = $event"
				@sort="toggleSort"
			>
				<template #cell-status="{ row }">
					<SBadge :tone="row.series.status === 'CONTINUING' ? 'ok' : 'neutral'">
						{{ seriesStatusLabel(row.series.status) }}
					</SBadge>
				</template>
				<template #cell-nextAiring="{ row }">
					<span v-if="row.series.nextAiring">{{ formatDate(row.series.nextAiring) }}</span>
					<span
						v-else
						class="s-cell-muted"
					>None</span>
				</template>
				<template #cell-episodes="{ row }">
					{{ row.series.statistics.episodeFileCount }} / {{ row.series.statistics.episodeCount }}
				</template>
				<template #cell-monitored="{ row }">
					<MonitorToggle
						:model-value="row.series.monitored"
						:label="`Toggle monitored for ${row.series.title}`"
						@update:model-value="toggleMonitored(row.series, $event)"
					/>
				</template>
			</MediaTable>

			<div class="library-pager">
				<span>{{ totalCount }} series</span>
				<SSelect
					class="library-pager-size"
					:options="[{ value: '20', label: '20 per page' }, { value: '40', label: '40 per page' }, { value: '100', label: '100 per page' }]"
					:model-value="String(pageSize)"
					@update:model-value="pageSize = Number($event)"
				/>
				<SButton
					size="sm"
					:disabled="page <= 1"
					@click="page = page - 1"
				>
					Previous
				</SButton>
				<span>Page {{ page }} of {{ totalPages }}</span>
				<SButton
					size="sm"
					:disabled="page >= totalPages"
					@click="page = page + 1"
				>
					Next
				</SButton>
			</div>
		</template>

		<AddMediaDialog
			ref="addDialogRef"
			v-model:open="addOpen"
			title="Add series"
			search-placeholder="Search by title"
			:search="searchSeries"
		>
			<template #details>
				<div class="add-form">
					<SField label="Root folder">
						<SSelect
							v-model="addForm.rootFolderId"
							:options="addRootFolderOptions"
							placeholder="Choose a root folder"
						/>
					</SField>
					<SField label="Series type">
						<SSelect
							v-model="addForm.seriesType"
							:options="seriesTypeOptions"
						/>
					</SField>
					<SField label="Episode numbering">
						<SSelect
							v-model="addForm.numbering"
							:options="seriesNumberingOptions"
						/>
					</SField>
					<SField label="Monitor">
						<SSelect
							v-model="addForm.monitorOption"
							:options="addMonitorOptionOptions"
						/>
					</SField>
					<SField label="New seasons">
						<SSelect
							v-model="addForm.monitorNewItems"
							:options="monitorNewItemsOptions"
						/>
					</SField>
					<SField label="Tags">
						<TagPicker v-model:tag-ids="addForm.tagIds" />
					</SField>
					<SCheckbox
						v-model="addForm.seasonFolder"
						label="Use a season folder"
					/>
					<SCheckbox
						v-model="addForm.monitorSpecials"
						label="Include specials"
					/>
					<SCheckbox
						v-model="addForm.searchOnAdd"
						label="Search for episodes on add"
					/>
					<VersionEditor
						v-model="addVersions"
						:root-folder-options="addRootFolderOptions"
					/>
				</div>
			</template>
			<template #footer="{ result }">
				<SButton @click="addOpen = false">
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="adding"
					@click="submitAdd(result)"
				>
					Add series
				</SButton>
			</template>
		</AddMediaDialog>

		<SDialog
			v-model="massEditOpen"
			title="Edit series"
		>
			<div class="add-form">
				<SField label="Monitored">
					<SSelect
						v-model="massEdit.monitored"
						:options="[{ value: 'unchanged', label: 'Leave unchanged' }, { value: 'true', label: 'Monitored' }, { value: 'false', label: 'Unmonitored' }]"
					/>
				</SField>
				<SField label="Series type">
					<SSelect
						v-model="massEdit.seriesType"
						:options="[{ value: 'unchanged', label: 'Leave unchanged' }, ...seriesTypeOptions]"
					/>
				</SField>
				<SField label="Move to root folder">
					<SSelect
						v-model="massEdit.rootFolderId"
						:options="[{ value: 'unchanged', label: 'Leave unchanged' }, ...addRootFolderOptions]"
					/>
				</SField>
				<SCheckbox
					v-if="massEdit.rootFolderId !== 'unchanged'"
					v-model="massEdit.moveFiles"
					label="Move files on disk"
				/>
				<SField label="Tags">
					<TagPicker v-model:tag-ids="massEdit.tagIds" />
				</SField>
				<SField label="Tag action">
					<SSelect
						v-model="massEdit.tagMode"
						:options="[{ value: 'add', label: 'Add tags' }, { value: 'remove', label: 'Remove tags' }, { value: 'replace', label: 'Replace tags' }]"
					/>
				</SField>
			</div>
			<template #footer>
				<SButton
					variant="danger"
					@click="deleteConfirmOpen = true"
				>
					Delete selected
				</SButton>
				<SButton @click="massEditOpen = false">
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="massEditing"
					@click="applyMassEdit"
				>
					Save changes
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteConfirmOpen"
			title="Delete series"
			:description="`This removes ${selected.length} series from the library.`"
		>
			<SCheckbox
				v-model="deleteFiles"
				label="Delete files on disk"
			/>
			<SCheckbox
				v-model="deleteExclusion"
				label="Add an import list exclusion"
			/>
			<template #footer>
				<SButton @click="deleteConfirmOpen = false">
					Cancel
				</SButton>
				<SButton
					variant="danger"
					:loading="deleting"
					@click="confirmDelete"
				>
					Delete
				</SButton>
			</template>
		</SDialog>
	</div>
</template>

<style scoped>
.library-toolbar {
	display: flex;
	flex-wrap: wrap;
	gap: 8px;
	align-items: center;
	margin-bottom: 24px;
}

.library-toolbar > :deep(.s-input),
.library-toolbar > :deep(.s-select-trigger) {
	min-width: 140px;
}

.library-view-toggle {
	display: flex;
	margin-left: auto;
	border: 1px solid var(--line);
	border-radius: var(--r-control);
	overflow: hidden;
}

.library-view-btn {
	display: flex;
	align-items: center;
	justify-content: center;
	width: 32px;
	height: 32px;
	border: none;
	background: var(--surface);
	color: var(--fg-muted);
	cursor: pointer;
}

.library-view-btn-active {
	background: var(--accent-soft);
	color: var(--accent);
}

.library-pager {
	display: flex;
	align-items: center;
	gap: 12px;
	margin-top: 24px;
	color: var(--fg-muted);
	font-size: var(--text-sm);
}

.library-pager-size {
	width: 140px;
}

.add-form {
	display: flex;
	flex-direction: column;
	gap: 12px;
}
</style>
