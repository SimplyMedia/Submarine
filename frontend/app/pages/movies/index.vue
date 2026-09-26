<script setup lang="ts">
import type { components } from '~/types/api'
import type { MediaLookupResult, MediaTableColumn, PosterCardBadge, PosterCardItem } from '~/types/ui'
import { formatBytes } from '~/composables/useFormat'
import {
	minimumAvailabilityOptions,
	movieStatusLabel,
	movieStatusOptions,
} from '~/utils/library-labels'

type MovieListItem = components['schemas']['MovieListItemDto']

useHead({ title: 'Movies' })

const api = useApi()
const reference = useReferenceStore()
const events = useEvents()
const toast = useToast()
const route = useRoute()

const view = ref<'grid' | 'table'>('grid')
const items = ref<MovieListItem[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = ref(40)
const loading = ref(true)
const loadError = ref('')

const term = ref('')
const monitoredFilter = ref('any')
const statusFilter = ref('any')
const hasFileFilter = ref('any')
const isAnimeFilter = ref('any')
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
const statusOptions = [{ value: 'any', label: 'Any status' }, ...movieStatusOptions]
const hasFileOptions = [
	{ value: 'any', label: 'Any file state' },
	{ value: 'true', label: 'Has file' },
	{ value: 'false', label: 'Missing file' },
]
const isAnimeOptions = [
	{ value: 'any', label: 'Any type' },
	{ value: 'true', label: 'Anime' },
	{ value: 'false', label: 'Not anime' },
]
const sortOptions = [
	{ value: 'SortTitle', label: 'Title' },
	{ value: 'Added', label: 'Added' },
	{ value: 'Year', label: 'Year' },
	{ value: 'Status', label: 'Status' },
]

const tagOptions = computed(() => [{ value: 'any', label: 'Any tag' }, ...reference.tags.map(tag => ({ value: String(tag.id), label: tag.label }))])
const rootFolderOptions = computed(() => [
	{ value: 'any', label: 'Any root folder' },
	...reference.rootFolders.filter(folder => folder.mediaKind === 'MOVIES').map(folder => ({ value: String(folder.id), label: folder.path })),
])
const addRootFolderOptions = computed(() =>
	reference.rootFolders.filter(folder => folder.mediaKind === 'MOVIES').map(folder => ({ value: String(folder.id), label: folder.path })),
)
const qualityOptions = computed(() => reference.qualityProfiles.map(profile => ({ value: String(profile.id), label: profile.name })))
const languageOptions = computed(() => reference.languageProfiles.map(profile => ({ value: String(profile.id), label: profile.name })))

const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize.value)))
// The backend only recognises the full words "ascending"/"descending" (Submarine.Api.Common.Paging.ApplySort).
const apiSortDirection = computed(() => (sortDirection.value === 'desc' ? 'descending' : 'ascending'))

const cards = computed<PosterCardItem[]>(() => items.value.map((movie) => {
	const badges: PosterCardBadge[] = [{ label: movieStatusLabel(movie.status), tone: movie.hasFile ? 'ok' : 'neutral' }]
	badges.push({ label: movie.hasFile ? formatBytes(movie.sizeOnDisk) : 'No file', tone: 'neutral' as const })
	if (!movie.monitored) {
		badges.push({ label: 'Unmonitored', tone: 'neutral' as const })
	}
	return {
		id: movie.id,
		to: `/movies/${movie.id}`,
		posterUrl: movie.posterUrl,
		title: movie.title,
		meta: movie.year ? String(movie.year) : undefined,
		metaSecondary: movie.studio ?? undefined,
		badges,
	}
}))

interface MovieRow {
	id: number
	to: string
	posterUrl: string | null
	title: string
	subtitle?: string
	movie: MovieListItem
}

const tableColumns: MediaTableColumn[] = [
	{ key: 'status', label: 'Status' },
	{ key: 'size', label: 'Size' },
	{ key: 'monitored', label: 'Monitored', align: 'right' },
]

const rows = computed<MovieRow[]>(() => items.value.map(movie => ({
	id: movie.id,
	to: `/movies/${movie.id}`,
	posterUrl: movie.posterUrl,
	title: movie.title,
	subtitle: movie.studio ?? undefined,
	movie,
})))

let searchTimer: ReturnType<typeof setTimeout>
const stopHandlers: Array<() => void> = []

onMounted(async () => {
	const stored = localStorage.getItem('submarine.movies.view')
	if (stored === 'grid' || stored === 'table') {
		view.value = stored
	}
	await reference.load()
	await load()
	stopHandlers.push(events.on('MovieAddedEvent', () => void load()))
	stopHandlers.push(events.on('MovieUpdatedEvent', () => void load()))
	stopHandlers.push(events.on('MovieDeletedEvent', () => void load()))
	if (route.query.add === '1') {
		addOpen.value = true
	}
})

onUnmounted(() => {
	stopHandlers.forEach(stop => stop())
})

watch(view, (value) => {
	localStorage.setItem('submarine.movies.view', value)
})

watch(term, () => {
	clearTimeout(searchTimer)
	searchTimer = setTimeout(() => {
		page.value = 1
		void load()
	}, 350)
})

watch([monitoredFilter, statusFilter, hasFileFilter, isAnimeFilter, tagFilter, rootFolderFilter, sortKey, sortDirection, pageSize], () => {
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
	const result = await api.GET('/api/v1/movies', {
		params: {
			query: {
				Page: page.value,
				PageSize: pageSize.value,
				SortKey: sortKey.value,
				SortDirection: apiSortDirection.value,
				monitored: monitoredFilter.value === 'any' ? undefined : monitoredFilter.value === 'true',
				status: (statusFilter.value === 'any' ? undefined : statusFilter.value) as MovieListItem['status'] | undefined,
				hasFile: hasFileFilter.value === 'any' ? undefined : hasFileFilter.value === 'true',
				isAnime: isAnimeFilter.value === 'any' ? undefined : isAnimeFilter.value === 'true',
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
		loadError.value = 'Could not load movies. Check your connection and try again.'
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

async function toggleMonitored(movie: MovieListItem, monitored: boolean) {
	movie.monitored = monitored
	const result = await api.PUT('/api/v1/movies/{id}', {
		params: { path: { id: movie.id } },
		body: { monitored, minimumAvailability: null, isAnime: null, tagIds: null, rootFolderId: null, moveFiles: null, versions: null },
	})
	if (!result.data) {
		movie.monitored = !monitored
		toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
	}
}

async function searchMovies(searchTerm: string): Promise<MediaLookupResult[]> {
	const result = await api.GET('/api/v1/movies/lookup', { params: { query: { term: searchTerm } } })
	if (!result.data) {
		return []
	}
	return result.data.map(hit => ({
		key: `${hit.provider}-${hit.tmdbId ?? hit.tvdbId}`,
		title: hit.title,
		year: hit.year,
		overview: hit.overview,
		posterUrl: hit.posterUrl,
		existingId: hit.existingMovieId,
		tvdbId: hit.tvdbId ?? null,
		tmdbId: hit.tmdbId,
		provider: hit.provider,
	}))
}

const addForm = reactive({
	rootFolderId: '',
	isAnime: false,
	minimumAvailability: 'RELEASED' as NonNullable<components['schemas']['MinimumAvailability']>,
	searchOnAdd: false,
	tagIds: [] as number[],
})
const addVersions = ref([{ name: 'Main', qualityProfileId: null as number | null, languageProfileId: null as number | null, rootFolderId: null as number | null }])
const adding = ref(false)
const addDialogRef = ref<{ back: () => void } | null>(null)

watch(() => reference.rootFolders, (folders) => {
	const first = folders.find(folder => folder.mediaKind === 'MOVIES')
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
	if (result.tmdbId == null) {
		toast.toast({ title: 'This title has no TMDB id and cannot be added', tone: 'danger' })
		return
	}
	adding.value = true
	try {
		const created = await api.POST('/api/v1/movies', {
			body: {
				tmdbId: result.tmdbId,
				title: result.title,
				rootFolderId: Number(addForm.rootFolderId),
				isAnime: addForm.isAnime,
				monitored: true,
				minimumAvailability: addForm.minimumAvailability,
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
			await navigateTo(`/movies/${created.data.movie.id}`)
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
	minimumAvailability: 'unchanged',
	qualityProfileId: 'unchanged',
	languageProfileId: 'unchanged',
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
		const result = await api.PUT('/api/v1/movies/editor', {
			body: {
				ids: selected.value,
				monitored: massEdit.monitored === 'unchanged' ? null : massEdit.monitored === 'true',
				minimumAvailability: (massEdit.minimumAvailability === 'unchanged' ? null : massEdit.minimumAvailability) as components['schemas']['MinimumAvailability'] | null,
				qualityProfileId: massEdit.qualityProfileId === 'unchanged' ? null : Number(massEdit.qualityProfileId),
				languageProfileId: massEdit.languageProfileId === 'unchanged' ? null : Number(massEdit.languageProfileId),
				rootFolderId: massEdit.rootFolderId === 'unchanged' ? null : Number(massEdit.rootFolderId),
				moveFiles: massEdit.rootFolderId === 'unchanged' ? null : massEdit.moveFiles,
				tags: massEdit.tagIds.length > 0 ? { mode: massEdit.tagMode, tagIds: massEdit.tagIds } : null,
			},
		})
		if (result.data) {
			toast.toast({ title: `${result.data.updated} movies updated`, tone: 'ok' })
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

async function confirmDelete() {
	deleting.value = true
	try {
		const result = await api.DELETE('/api/v1/movies/editor', {
			body: { ids: selected.value, deleteFiles: deleteFiles.value, addImportListExclusion: deleteExclusion.value },
		})
		if (result.response.ok) {
			toast.toast({ title: `${selected.value.length} movies removed`, tone: 'ok' })
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
		<SPageHeader title="Movies">
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
					Add movie
				</SButton>
			</template>
		</SPageHeader>

		<div class="library-toolbar">
			<SInput
				v-model="term"
				type="search"
				placeholder="Search movies"
			/>
			<SSelect
				v-model="monitoredFilter"
				:options="monitoredOptions"
			/>
			<SSelect
				v-model="statusFilter"
				:options="statusOptions"
			/>
			<SSelect
				v-model="hasFileFilter"
				:options="hasFileOptions"
			/>
			<SSelect
				v-model="isAnimeFilter"
				:options="isAnimeOptions"
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
			<SEmptyState message="No movies match yet. Add a movie to start building your library.">
				<template #action>
					<SButton
						variant="primary"
						@click="addOpen = true"
					>
						Add movie
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
					<SBadge :tone="row.movie.hasFile ? 'ok' : 'neutral'">
						{{ movieStatusLabel(row.movie.status) }}
					</SBadge>
				</template>
				<template #cell-size="{ row }">
					{{ row.movie.hasFile ? formatBytes(row.movie.sizeOnDisk) : 'No file' }}
				</template>
				<template #cell-monitored="{ row }">
					<MonitorToggle
						:model-value="row.movie.monitored"
						:label="`Toggle monitored for ${row.movie.title}`"
						@update:model-value="toggleMonitored(row.movie, $event)"
					/>
				</template>
			</MediaTable>

			<div class="library-pager">
				<span>{{ totalCount }} movies</span>
				<SSelect
					class="library-pager-size"
					:options="[{ value: '40', label: '40 per page' }, { value: '100', label: '100 per page' }]"
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
			title="Add movie"
			search-placeholder="Search by title"
			:search="searchMovies"
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
					<SField label="Minimum availability">
						<SSelect
							v-model="addForm.minimumAvailability"
							:options="minimumAvailabilityOptions"
						/>
					</SField>
					<SField label="Tags">
						<TagPicker v-model:tag-ids="addForm.tagIds" />
					</SField>
					<SCheckbox
						v-model="addForm.isAnime"
						label="This is an anime"
					/>
					<SCheckbox
						v-model="addForm.searchOnAdd"
						label="Search for a release on add"
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
					Add movie
				</SButton>
			</template>
		</AddMediaDialog>

		<SDialog
			v-model="massEditOpen"
			title="Edit movies"
		>
			<div class="add-form">
				<SField label="Monitored">
					<SSelect
						v-model="massEdit.monitored"
						:options="[{ value: 'unchanged', label: 'Leave unchanged' }, { value: 'true', label: 'Monitored' }, { value: 'false', label: 'Unmonitored' }]"
					/>
				</SField>
				<SField label="Minimum availability">
					<SSelect
						v-model="massEdit.minimumAvailability"
						:options="[{ value: 'unchanged', label: 'Leave unchanged' }, ...minimumAvailabilityOptions]"
					/>
				</SField>
				<SField label="Quality profile">
					<SSelect
						v-model="massEdit.qualityProfileId"
						:options="[{ value: 'unchanged', label: 'Leave unchanged' }, ...qualityOptions]"
					/>
				</SField>
				<SField label="Language profile">
					<SSelect
						v-model="massEdit.languageProfileId"
						:options="[{ value: 'unchanged', label: 'Leave unchanged' }, ...languageOptions]"
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
			title="Delete movies"
			:description="`This removes ${selected.length} movies from the library.`"
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
