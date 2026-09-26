<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { components } from '~/types/api'
import type { PosterCardBadge, PosterCardItem } from '~/types/ui'
import { minimumAvailabilityOptions, movieStatusLabel } from '~/utils/library-labels'
import { formatBytes } from '~/composables/useFormat'

const { t } = useI18n()

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

useHead({ title: computed(() => collection.value?.title ?? t('pages.movies.collections.collectionTitle')) })

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
			loadError.value = t('pages.movies.collections.notFound')
		}
	}
	else {
		loadError.value = t('pages.movies.collections.detailLoadError')
	}
	try {
		movies.value = await loadCollectionMovies()
	}
	catch {
		loadError.value = loadError.value || t('pages.movies.collections.moviesLoadError')
	}
	loading.value = false
}

onMounted(async () => {
	await reference.load()
	await load()
})

const cards = computed<PosterCardItem[]>(() => movies.value.map((movie) => {
	const badges: PosterCardBadge[] = [{ label: t(movieStatusLabel(movie.status)), tone: movie.hasFile ? 'ok' : 'neutral' }]
	badges.push({ label: movie.hasFile ? formatBytes(movie.sizeOnDisk) : t('pages.movies.noFile'), tone: 'neutral' as const })
	if (!movie.monitored) {
		badges.push({ label: t('pages.movies.unmonitored'), tone: 'neutral' as const })
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
			toast.toast({ title: t('pages.movies.collections.updated'), tone: 'ok' })
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
			toast.toast({ title: t(result.data.added === 1 ? 'pages.movies.collections.addedMissingSingle' : 'pages.movies.collections.addedMissingPlural', { count: result.data.added }), tone: 'ok' })
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
			:message="loadError || t('pages.movies.collections.notFound')"
		>
			<template #action>
				<SButton
					variant="primary"
					@click="navigateTo('/movies/collections')"
				>
					{{ t('pages.movies.collections.backToCollections') }}
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
						<STooltip :text="collection.id == null ? t('pages.movies.collections.configureMonitoring') : undefined">
							<MonitorToggle
								:model-value="collection.monitored"
								:label="t('pages.movies.collections.toggleMonitored', { title: collection.title })"
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
						{{ t(collection.movieCount === 1 ? 'pages.movies.collections.movieCountLibrarySingle' : 'pages.movies.collections.movieCountLibraryPlural', { count: collection.movieCount }) }}
					</p>
				</div>
				<div class="collection-header-actions">
					<STooltip :text="collection.id == null ? t('pages.movies.collections.noSavedSettingsShort') : undefined">
						<SButton
							variant="secondary"
							:disabled="collection.id == null"
							@click="openEdit"
						>
							{{ t('pages.movies.collections.editDefaults') }}
						</SButton>
					</STooltip>
				</div>
			</div>

			<SSection :title="t('pages.movies.collections.inYourLibrary')">
				<MediaPosterGrid :items="cards">
					<template #empty>
						<SEmptyState :message="t('pages.movies.collections.noMoviesInLibrary')" />
					</template>
				</MediaPosterGrid>
			</SSection>

			<SSection
				v-if="collection.missingCount > 0"
				:title="t('pages.movies.collections.missingFromCollection')"
			>
				<p class="collection-missing-note">
					{{ t(collection.missingCount === 1 ? 'pages.movies.collections.missingCountNoteSingle' : 'pages.movies.collections.missingCountNotePlural', { count: collection.missingCount }) }}
				</p>
				<STooltip :text="collection.id == null ? t('pages.movies.collections.configureBeforeAdd') : undefined">
					<SButton
						variant="primary"
						:disabled="collection.id == null"
						@click="addMissingOpen = true"
					>
						{{ t('pages.movies.collections.addMissing') }}
					</SButton>
				</STooltip>
			</SSection>
		</template>

		<SDialog
			v-model="editOpen"
			:title="t('pages.movies.collections.defaultsTitle')"
			:description="collection ? t('pages.movies.collections.defaultsDescription', { title: collection.title }) : undefined"
		>
			<div class="add-form">
				<SField :label="t('pages.movies.collections.rootFolder')">
					<SSelect
						v-model="editForm.rootFolderId"
						:options="rootFolderOptions"
						:placeholder="t('pages.movies.collections.chooseRootFolder')"
					/>
				</SField>
				<SField :label="t('pages.movies.collections.qualityProfile')">
					<SSelect
						v-model="editForm.qualityProfileId"
						:options="qualityOptions"
						:placeholder="t('pages.movies.collections.chooseProfile')"
					/>
				</SField>
				<SField :label="t('pages.movies.collections.languageProfile')">
					<SSelect
						v-model="editForm.languageProfileId"
						:options="languageOptions"
						:placeholder="t('pages.movies.collections.chooseProfile')"
					/>
				</SField>
				<SField :label="t('pages.movies.collections.minimumAvailability')">
					<SSelect
						v-model="editForm.minimumAvailability"
						:options="minimumAvailabilityOptions.map(option => ({ ...option, label: t(option.label) }))"
					/>
				</SField>
				<SCheckbox
					v-model="editForm.searchOnAdd"
					:label="t('pages.movies.collections.searchMissingOnAdd')"
				/>
			</div>
			<template #footer>
				<SButton @click="editOpen = false">
					{{ t('pages.movies.collections.cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="savingEdit"
					@click="saveEdit"
				>
					{{ t('pages.movies.collections.saveChanges') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="addMissingOpen"
			:title="t('pages.movies.collections.addMissingTitle')"
			:description="collection ? t('pages.movies.collections.addMissingDescription', { title: collection.title }) : undefined"
		>
			<template #footer>
				<SButton @click="addMissingOpen = false">
					{{ t('pages.movies.collections.cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="addingMissing"
					@click="confirmAddMissing"
				>
					{{ t('pages.movies.collections.addMissingMovies') }}
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
