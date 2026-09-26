<script setup lang="ts">
import type { components } from '~/types/api'
import type { PosterCardBadge, PosterCardItem } from '~/types/ui'
import { minimumAvailabilityOptions, movieStatusLabel } from '~/utils/library-labels'
import { formatBytes } from '~/composables/useFormat'

type CollectionDto = components['schemas']['CollectionDto']
type MovieListItem = components['schemas']['MovieListItemDto']

const route = useRoute()
const tmdbCollectionId = Number(route.params.id)

const api = useApi()
const reference = useReferenceStore()
const toast = useToast()

const collection = ref<CollectionDto | null>(null)
const movies = ref<MovieListItem[]>([])
const loading = ref(true)
const loadError = ref('')

useHead({ title: computed(() => collection.value?.title ?? 'Collection') })

async function loadCollectionMovies(): Promise<MovieListItem[]> {
	// The movies endpoint has no collection filter, so page through every
	// movie instead of capping at one page (which silently dropped members
	// of large collections).
	const pageSize = 200
	const matches: MovieListItem[] = []
	let page = 1
	for (;;) {
		const result = await api.GET('/api/v1/movies', { params: { query: { Page: page, PageSize: pageSize } } })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		matches.push(...result.data.items.filter(movie => movie.tmdbCollectionId === tmdbCollectionId))
		if (page * pageSize >= result.data.totalCount) {
			return matches
		}
		page += 1
	}
}

async function load() {
	loading.value = true
	loadError.value = ''
	const collectionsResult = await api.GET('/api/v1/collections')
	if (collectionsResult.data) {
		collection.value = collectionsResult.data.find(entry => entry.tmdbCollectionId === tmdbCollectionId) ?? null
		if (!collection.value) {
			loadError.value = 'This collection could not be found.'
		}
	}
	else {
		loadError.value = 'Could not load this collection. Check your connection and try again.'
	}
	try {
		movies.value = await loadCollectionMovies()
	}
	catch {
		loadError.value = loadError.value || 'Could not load the movies in this collection.'
	}
	loading.value = false
}

onMounted(async () => {
	await reference.load()
	await load()
})

const cards = computed<PosterCardItem[]>(() => movies.value.map((movie) => {
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

const rootFolderOptions = computed(() =>
	reference.rootFolders.filter(folder => folder.mediaKind === 'MOVIES').map(folder => ({ value: String(folder.id), label: folder.path })),
)
const qualityOptions = computed(() => reference.qualityProfiles.map(profile => ({ value: String(profile.id), label: profile.name })))
const languageOptions = computed(() => reference.languageProfiles.map(profile => ({ value: String(profile.id), label: profile.name })))

async function toggleMonitored(monitored: boolean) {
	if (!collection.value?.id) {
		return
	}
	const previous = collection.value.monitored
	collection.value.monitored = monitored
	const result = await api.PUT('/api/v1/collections/{id}', {
		params: { path: { id: collection.value.id } },
		body: { monitored, rootFolderId: null, qualityProfileId: null, languageProfileId: null, minimumAvailability: null, searchOnAdd: null },
	})
	if (!result.data) {
		if (collection.value) {
			collection.value.monitored = previous
		}
		toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
	}
}

const editOpen = ref(false)
const editForm = reactive({
	rootFolderId: '',
	qualityProfileId: '',
	languageProfileId: '',
	minimumAvailability: 'RELEASED' as NonNullable<components['schemas']['MinimumAvailability']>,
	searchOnAdd: false,
})
const savingEdit = ref(false)

function openEdit() {
	if (!collection.value) {
		return
	}
	editForm.rootFolderId = collection.value.rootFolderId != null ? String(collection.value.rootFolderId) : ''
	editForm.qualityProfileId = collection.value.qualityProfileId != null ? String(collection.value.qualityProfileId) : ''
	editForm.languageProfileId = collection.value.languageProfileId != null ? String(collection.value.languageProfileId) : ''
	editForm.minimumAvailability = collection.value.minimumAvailability
	editForm.searchOnAdd = collection.value.searchOnAdd
	editOpen.value = true
}

async function saveEdit() {
	if (!collection.value?.id) {
		return
	}
	savingEdit.value = true
	try {
		const result = await api.PUT('/api/v1/collections/{id}', {
			params: { path: { id: collection.value.id } },
			body: {
				monitored: null,
				rootFolderId: editForm.rootFolderId ? Number(editForm.rootFolderId) : null,
				qualityProfileId: editForm.qualityProfileId ? Number(editForm.qualityProfileId) : null,
				languageProfileId: editForm.languageProfileId ? Number(editForm.languageProfileId) : null,
				minimumAvailability: editForm.minimumAvailability,
				searchOnAdd: editForm.searchOnAdd,
			},
		})
		if (result.data) {
			toast.toast({ title: 'Collection updated', tone: 'ok' })
			editOpen.value = false
			await load()
		}
		else {
			toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
		}
	}
	finally {
		savingEdit.value = false
	}
}

const addMissingOpen = ref(false)
const addingMissing = ref(false)

async function confirmAddMissing() {
	if (!collection.value?.id) {
		return
	}
	addingMissing.value = true
	try {
		const result = await api.POST('/api/v1/collections/{id}/add-missing', {
			params: { path: { id: collection.value.id } },
		})
		if (result.data) {
			toast.toast({ title: `${result.data.added} movie${result.data.added === 1 ? '' : 's'} added`, tone: 'ok' })
			addMissingOpen.value = false
			await load()
		}
		else {
			toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
		}
	}
	finally {
		addingMissing.value = false
	}
}
</script>

<template>
	<div>
		<SSpinner v-if="loading && !collection" />
		<SEmptyState
			v-else-if="!collection"
			:message="loadError || 'This collection could not be found.'"
		>
			<template #action>
				<SButton
					variant="primary"
					@click="navigateTo('/movies/collections')"
				>
					Back to collections
				</SButton>
			</template>
		</SEmptyState>
		<template v-else>
			<div class="collection-header">
				<SPosterImage
					:src="collection.posterUrl"
					:alt="collection.title"
					class="collection-poster"
				/>
				<div class="collection-facts">
					<div class="collection-title-row">
						<h1>{{ collection.title }}</h1>
						<STooltip :text="collection.id == null ? 'Configure this collection to enable monitoring' : undefined">
							<MonitorToggle
								:model-value="collection.monitored"
								:label="`Toggle monitored for ${collection.title}`"
								:disabled="collection.id == null"
								@update:model-value="toggleMonitored"
							/>
						</STooltip>
					</div>
					<p
						v-if="collection.overview"
						class="collection-overview"
					>
						{{ collection.overview }}
					</p>
					<p class="collection-meta">
						{{ collection.movieCount }} movie{{ collection.movieCount === 1 ? '' : 's' }} in the library
					</p>
				</div>
				<div class="collection-header-actions">
					<STooltip :text="collection.id == null ? 'This collection has no saved settings yet; add one of its movies first' : undefined">
						<SButton
							variant="secondary"
							:disabled="collection.id == null"
							@click="openEdit"
						>
							Edit defaults
						</SButton>
					</STooltip>
				</div>
			</div>

			<SSection title="In your library">
				<MediaPosterGrid :items="cards">
					<template #empty>
						<SEmptyState message="None of this collection's movies are in the library yet. Add one from the movies page." />
					</template>
				</MediaPosterGrid>
			</SSection>

			<SSection
				v-if="collection.missingCount > 0"
				title="Missing from this collection"
			>
				<p class="collection-missing-note">
					{{ collection.missingCount }} movie{{ collection.missingCount === 1 ? '' : 's' }} from this collection {{ collection.missingCount === 1 ? 'is' : 'are' }} not in the library yet.
				</p>
				<STooltip :text="collection.id == null ? 'Configure a root folder, quality profile and language profile first' : undefined">
					<SButton
						variant="primary"
						:disabled="collection.id == null"
						@click="addMissingOpen = true"
					>
						Add missing
					</SButton>
				</STooltip>
			</SSection>
		</template>

		<SDialog
			v-model="editOpen"
			title="Collection defaults"
			:description="collection ? `Applied when adding missing movies from ${collection.title}.` : undefined"
		>
			<div class="add-form">
				<SField label="Root folder">
					<SSelect
						v-model="editForm.rootFolderId"
						:options="rootFolderOptions"
						placeholder="Choose a root folder"
					/>
				</SField>
				<SField label="Quality profile">
					<SSelect
						v-model="editForm.qualityProfileId"
						:options="qualityOptions"
						placeholder="Choose a profile"
					/>
				</SField>
				<SField label="Language profile">
					<SSelect
						v-model="editForm.languageProfileId"
						:options="languageOptions"
						placeholder="Choose a profile"
					/>
				</SField>
				<SField label="Minimum availability">
					<SSelect
						v-model="editForm.minimumAvailability"
						:options="minimumAvailabilityOptions"
					/>
				</SField>
				<SCheckbox
					v-model="editForm.searchOnAdd"
					label="Search for a release when adding missing movies"
				/>
			</div>
			<template #footer>
				<SButton @click="editOpen = false">
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="savingEdit"
					@click="saveEdit"
				>
					Save changes
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="addMissingOpen"
			title="Add missing movies"
			:description="collection ? `This adds every movie from ${collection.title} that is not already in the library, using its saved root folder and profiles.` : undefined"
		>
			<template #footer>
				<SButton @click="addMissingOpen = false">
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="addingMissing"
					@click="confirmAddMissing"
				>
					Add missing movies
				</SButton>
			</template>
		</SDialog>
	</div>
</template>

<style scoped>
.collection-header {
	display: flex;
	gap: 24px;
	margin-bottom: 32px;
}

.collection-poster {
	width: 150px;
	flex: none;
	border-radius: var(--r-panel);
	box-shadow: var(--shadow-pop);
}

.collection-facts {
	flex: 1;
	min-width: 0;
}

.collection-title-row {
	display: flex;
	align-items: center;
	gap: 12px;
}

.collection-title-row h1 {
	font-size: var(--text-2xl);
	font-weight: 600;
}

.collection-overview {
	max-width: 72ch;
	margin-top: 12px;
}

.collection-meta {
	color: var(--fg-muted);
	margin-top: 8px;
	font-size: var(--text-sm);
}

.collection-missing-note {
	color: var(--fg-muted);
	margin-bottom: 12px;
}

.add-form {
	display: flex;
	flex-direction: column;
	gap: 12px;
}

@media (max-width: 767px) {
	.collection-header {
		flex-direction: column;
	}
}
</style>
