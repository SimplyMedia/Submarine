<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { components } from '~/types/api'
import { formatBytes, formatDate, formatDateTime, formatDuration } from '~/composables/useFormat'
import {
	historyEventTypeLabel,
	minimumAvailabilityOptions,
	movieStatusLabel,
	qualityLabel,
} from '~/utils/library-labels'

const { t } = useI18n()

type MovieDetail = components['schemas']['MovieDetailDto']
type MovieFile = components['schemas']['MovieFileDto']
type Version = components['schemas']['VersionDto']
type HistoryEvent = components['schemas']['HistoryEventDto']

const route = useRoute()
const movieId = Number(route.params.id)

const api = useApi()
const reference = useReferenceStore()
const events = useEvents()
const toast = useToast()

const detail = ref<MovieDetail | null>(null)
const history = ref<HistoryEvent[]>([])
const loading = ref(true)
const loadError = ref('')

const movie = computed(() => detail.value?.movie ?? null)
const files = computed<MovieFile[]>(() => detail.value?.files ?? [])

useHead({ title: computed(() => movie.value?.title ?? t('pages.movies.movie')) })

function languageLabel(language: string): string {
	return language.charAt(0) + language.slice(1).toLowerCase()
}

async function load() {
	loading.value = true
	loadError.value = ''
	const [detailResult, historyResult] = await Promise.all([
		api.GET('/api/v1/movies/{id}', { params: { path: { id: movieId } } }),
		api.GET('/api/v1/history/movie', { params: { query: { movieId } } }),
	])
	if (detailResult.data) {
		detail.value = detailResult.data
	}
	else {
		loadError.value = t('pages.movies.detailLoadError')
	}
	if (historyResult.data) {
		history.value = historyResult.data
	}
	loading.value = false
}

const stopHandlers: Array<() => void> = []

onMounted(async () => {
	await reference.load()
	await load()
	stopHandlers.push(events.on('MovieUpdatedEvent', (payload) => {
		const id = (payload as { movieId?: number }).movieId
		if (id === movieId) {
			void load()
		}
	}))
	stopHandlers.push(events.on('MovieDeletedEvent', (payload) => {
		const id = (payload as { movieId?: number }).movieId
		if (id === movieId) {
			toast.toast({ title: t('pages.movies.deleted'), tone: 'info' })
			void navigateTo('/movies', { replace: true })
		}
	}))
	stopHandlers.push(events.on('CommandUpdated', (payload) => {
		const command = payload as { name?: string, status?: string }
		const trackedNames = ['RefreshMovie', 'RescanMovie', 'MovieSearch']
		if (command.status === 'COMPLETED' && command.name && trackedNames.includes(command.name)) {
			void load()
		}
	}))
})

onUnmounted(() => {
	stopHandlers.forEach(stop => stop())
})

const rootFolderOptions = computed(() =>
	reference.rootFolders.filter(folder => folder.mediaKind === 'MOVIES').map(folder => ({ value: String(folder.id), label: folder.path })),
)
const qualityOptions = computed(() => reference.qualityProfiles.map(profile => ({ value: String(profile.id), label: profile.name })))
const languageOptions = computed(() => reference.languageProfiles.map(profile => ({ value: String(profile.id), label: profile.name })))

async function toggleMonitored(monitored: boolean) {
	if (!movie.value) {
		return
	}
	const previous = movie.value.monitored
	movie.value.monitored = monitored
	const result = await api.PUT('/api/v1/movies/{id}', {
		params: { path: { id: movieId } },
		body: { monitored, minimumAvailability: null, isAnime: null, tagIds: null, rootFolderId: null, moveFiles: null, versions: null },
	})
	if (!result.data) {
		if (movie.value) {
			movie.value.monitored = previous
		}
		toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
	}
}

const tagIds = ref<number[]>([])
watch(movie, (value) => {
	if (value) {
		tagIds.value = [...value.tagIds]
	}
}, { immediate: true })

let tagsSaving = false
watch(tagIds, async (value, previous) => {
	if (tagsSaving || !movie.value || JSON.stringify(value) === JSON.stringify(previous)) {
		return
	}
	tagsSaving = true
	try {
		const result = await api.PUT('/api/v1/movies/{id}', {
			params: { path: { id: movieId } },
			body: { tagIds: value, monitored: null, minimumAvailability: null, isAnime: null, rootFolderId: null, moveFiles: null, versions: null },
		})
		if (!result.data) {
			toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
		}
	}
	finally {
		tagsSaving = false
	}
})

const actionItems = computed(() => [
	{ label: t('pages.movies.actions.refreshMetadata'), icon: 'lucide:refresh-cw', onSelect: () => void refresh() },
	{ label: t('pages.movies.actions.rescanFiles'), icon: 'lucide:folder-search', onSelect: () => void rescan() },
	{ label: t('pages.movies.actions.search'), icon: 'lucide:search', onSelect: () => void searchMovie() },
	{ label: t('pages.movies.actions.interactiveSearch'), icon: 'lucide:search-check', onSelect: () => { interactiveSearchOpen.value = true } },
	{ label: t('pages.movies.actions.edit'), icon: 'lucide:pencil', onSelect: () => openEdit() },
	{ label: t('pages.movies.actions.delete'), icon: 'lucide:trash-2', danger: true, onSelect: () => { deleteOpen.value = true } },
])

async function refresh() {
	const result = await api.POST('/api/v1/movies/{id}/refresh', { params: { path: { id: movieId } } })
	toast.toast({ title: result.data ? t('pages.movies.refreshQueued') : toApiError(result.error, result.response).message, tone: result.data ? 'ok' : 'danger' })
}

async function rescan() {
	const result = await api.POST('/api/v1/movies/{id}/rescan', { params: { path: { id: movieId } } })
	toast.toast({ title: result.data ? t('pages.movies.rescanQueued') : toApiError(result.error, result.response).message, tone: result.data ? 'ok' : 'danger' })
}

async function searchMovie() {
	const result = await api.POST('/api/v1/movies/{id}/search', { params: { path: { id: movieId } } })
	toast.toast({ title: result.data ? t('pages.movies.searchQueued') : toApiError(result.error, result.response).message, tone: result.data ? 'ok' : 'danger' })
}

const interactiveSearchOpen = ref(false)
const versionLabels = computed(() => Object.fromEntries((movie.value?.versions ?? []).map(v => [v.id, v.name])))

const editOpen = ref(false)
const editForm = reactive({
	isAnime: false,
	minimumAvailability: 'RELEASED' as NonNullable<components['schemas']['MinimumAvailability']>,
	rootFolderId: '',
	moveFiles: false,
})
const savingEdit = ref(false)

function openEdit() {
	if (!movie.value) {
		return
	}
	editForm.isAnime = movie.value.isAnime
	editForm.minimumAvailability = movie.value.minimumAvailability
	editForm.rootFolderId = String(movie.value.versions[0]?.rootFolderId ?? '')
	editForm.moveFiles = false
	editOpen.value = true
}

async function saveEdit() {
	savingEdit.value = true
	try {
		const currentRootFolderId = movie.value?.versions[0]?.rootFolderId
		const movedRootFolderId = editForm.rootFolderId && Number(editForm.rootFolderId) !== currentRootFolderId ? Number(editForm.rootFolderId) : null
		const result = await api.PUT('/api/v1/movies/{id}', {
			params: { path: { id: movieId } },
			body: {
				monitored: null,
				minimumAvailability: editForm.minimumAvailability,
				isAnime: editForm.isAnime,
				tagIds: null,
				rootFolderId: movedRootFolderId,
				moveFiles: movedRootFolderId ? editForm.moveFiles : null,
				versions: null,
			},
		})
		if (result.data) {
			detail.value = result.data
			editOpen.value = false
			toast.toast({ title: t('pages.movies.movieUpdated'), tone: 'ok' })
		}
		else {
			toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
		}
	}
	finally {
		savingEdit.value = false
	}
}

const deleteOpen = ref(false)
const deleteFiles = ref(false)
const deleteExclusion = ref(false)
const deleting = ref(false)

async function confirmDelete() {
	deleting.value = true
	try {
		const result = await api.DELETE('/api/v1/movies/{id}', {
			params: { path: { id: movieId }, query: { deleteFiles: deleteFiles.value, addImportListExclusion: deleteExclusion.value } },
		})
		if (result.response.ok) {
			toast.toast({ title: t('pages.movies.movieDeleted', { title: movie.value?.title ?? t('pages.movies.movie') }), tone: 'ok' })
			await navigateTo('/movies', { replace: true })
		}
		else {
			toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
		}
	}
	finally {
		deleting.value = false
	}
}

const addVersionOpen = ref(false)
const addVersionForm = reactive({ name: '', qualityProfileId: '', languageProfileId: '', rootFolderId: '' })
const addingVersion = ref(false)

async function submitAddVersion() {
	addingVersion.value = true
	try {
		const result = await api.POST('/api/v1/movies/{movieId}/versions', {
			params: { path: { movieId } },
			body: {
				name: addVersionForm.name || null,
				qualityProfileId: Number(addVersionForm.qualityProfileId || reference.qualityProfiles[0]?.id),
				languageProfileId: Number(addVersionForm.languageProfileId || reference.languageProfiles[0]?.id),
				rootFolderId: addVersionForm.rootFolderId ? Number(addVersionForm.rootFolderId) : null,
			},
		})
		if (result.data && detail.value) {
			detail.value.movie.versions = [...detail.value.movie.versions, result.data]
			addVersionOpen.value = false
			toast.toast({ title: t('pages.movies.versionAdded'), tone: 'ok' })
		}
		else {
			toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
		}
	}
	finally {
		addingVersion.value = false
	}
}

async function updateVersion(version: Version, changes: { qualityProfileId?: number, languageProfileId?: number, monitored?: boolean }) {
	const previousMonitored = version.monitored
	if (changes.monitored !== undefined) {
		version.monitored = changes.monitored
	}
	const result = await api.PUT('/api/v1/media-versions/{id}', {
		params: { path: { id: version.id } },
		body: {
			name: null,
			path: null,
			rootFolderId: null,
			qualityProfileId: changes.qualityProfileId ?? null,
			languageProfileId: changes.languageProfileId ?? null,
			monitored: changes.monitored ?? null,
		},
	})
	if (result.data) {
		Object.assign(version, result.data)
	}
	else {
		if (changes.monitored !== undefined) {
			version.monitored = previousMonitored
		}
		toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
	}
}

const deleteVersionTarget = ref<Version | null>(null)
const deleteVersionOpen = computed({
	get: () => deleteVersionTarget.value != null,
	set: (value: boolean) => { if (!value) deleteVersionTarget.value = null },
})
const deleteVersionFiles = ref(false)

async function confirmDeleteVersion() {
	if (!deleteVersionTarget.value || !detail.value) {
		return
	}
	const result = await api.DELETE('/api/v1/media-versions/{id}', {
		params: { path: { id: deleteVersionTarget.value.id }, query: { deleteFiles: deleteVersionFiles.value } },
	})
	if (result.response.ok) {
		detail.value.movie.versions = detail.value.movie.versions.filter(version => version.id !== deleteVersionTarget.value?.id)
		toast.toast({ title: t('pages.movies.versionRemoved'), tone: 'ok' })
	}
	else {
		toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
	}
	deleteVersionTarget.value = null
}
</script>

<template>
	<div>
		<SSpinner v-if="loading && !movie" />
		<SEmptyState
			v-else-if="!movie"
			:message="loadError || t('pages.movies.movieNotFound')"
		>
			<template #action>
				<SButton
					variant="primary"
					@click="navigateTo('/movies')"
				>
					{{ t('pages.movies.backToMovies') }}
				</SButton>
			</template>
		</SEmptyState>
		<template v-else>
			<div
				v-if="movie.backdropUrl"
				class="movie-backdrop"
				:style="{ backgroundImage: `url(${movie.backdropUrl})` }"
			/>
			<div class="movie-header">
				<SPosterImage
					:src="movie.posterUrl"
					:alt="movie.title"
					class="movie-poster"
				/>
				<div class="movie-facts">
					<div class="movie-title-row">
						<h1>{{ movie.title }}<span v-if="movie.year"> ({{ movie.year }})</span></h1>
						<MonitorToggle
							:model-value="movie.monitored"
							:label="t('pages.movies.toggleMonitored', { title: movie.title })"
							@update:model-value="toggleMonitored"
						/>
					</div>
					<div class="movie-badges">
						<SBadge :tone="movie.hasFile ? 'ok' : 'neutral'">
							{{ t(movieStatusLabel(movie.status)) }}
						</SBadge>
						<SBadge
							v-if="movie.isAnime"
							tone="neutral"
						>
							{{ t('pages.movies.anime') }}
						</SBadge>
						<SBadge
							v-if="movie.certification"
							tone="neutral"
						>
							{{ movie.certification }}
						</SBadge>
					</div>
					<p class="movie-meta">
						<span v-if="movie.studio">{{ movie.studio }}</span>
						<span v-if="movie.runtime"> · {{ formatDuration(movie.runtime) }}</span>
						<span v-if="movie.genres.length"> · {{ movie.genres.join(', ') }}</span>
					</p>
					<p
						v-if="movie.inCinemasDate || movie.digitalReleaseDate || movie.physicalReleaseDate"
						class="movie-meta"
					>
						<span v-if="movie.inCinemasDate">{{ t('pages.movies.inCinemas', { date: formatDate(movie.inCinemasDate) }) }}</span>
						<span v-if="movie.digitalReleaseDate"> · {{ t('pages.movies.digital', { date: formatDate(movie.digitalReleaseDate) }) }}</span>
						<span v-if="movie.physicalReleaseDate"> · {{ t('pages.movies.physical', { date: formatDate(movie.physicalReleaseDate) }) }}</span>
					</p>
					<p
						v-if="movie.overview"
						class="movie-overview"
					>
						{{ movie.overview }}
					</p>
					<NuxtLink
						v-if="movie.tmdbCollectionId"
						:to="`/movies/collections/${movie.tmdbCollectionId}`"
						class="movie-collection-link"
					>
						{{ t('pages.movies.partOfCollection', { title: movie.collectionTitle ?? t('pages.movies.aCollection') }) }}
					</NuxtLink>
					<SField
						:label="t('pages.movies.tags')"
						class="movie-tags-field"
					>
						<TagPicker v-model:tag-ids="tagIds" />
					</SField>
				</div>
				<div class="movie-header-actions">
					<SDropdownMenu :items="actionItems">
						<template #trigger>
							<SButton variant="secondary">
								{{ t('pages.movies.actionsLabel') }}
							</SButton>
						</template>
					</SDropdownMenu>
				</div>
			</div>

			<SSection :title="t('pages.movies.versions')">
				<template #default>
					<div class="version-list">
						<div
							v-for="version in movie.versions"
							:key="version.id"
							class="version-row"
						>
							<span class="version-name">{{ version.name }}</span>
							<SSelect
								:model-value="String(version.qualityProfileId)"
								:options="qualityOptions"
								@update:model-value="updateVersion(version, { qualityProfileId: Number($event) })"
							/>
							<SSelect
								:model-value="String(version.languageProfileId)"
								:options="languageOptions"
								@update:model-value="updateVersion(version, { languageProfileId: Number($event) })"
							/>
							<span class="version-path">{{ version.rootFolderPath }}/{{ version.path }}</span>
							<MonitorToggle
								:model-value="version.monitored"
								:label="t('pages.movies.toggleVersionMonitored', { name: version.name })"
								@update:model-value="updateVersion(version, { monitored: $event })"
							/>
							<SIconButton
								:label="t('pages.movies.removeVersion')"
								:disabled="movie.versions.length <= 1"
								@click="deleteVersionTarget = version"
							>
								<Icon
									name="lucide:trash-2"
									aria-hidden="true"
								/>
							</SIconButton>
						</div>
					</div>
					<SButton
						size="sm"
						@click="addVersionOpen = true"
					>
						{{ t('pages.movies.addVersion') }}
					</SButton>
				</template>
			</SSection>

			<SSection :title="t('pages.movies.files')">
				<STable
					v-if="files.length > 0"
					:columns="[
						{ key: 'quality', label: t('pages.movies.quality') },
						{ key: 'size', label: t('pages.movies.size'), align: 'right' },
						{ key: 'releaseGroup', label: t('pages.movies.releaseGroup') },
						{ key: 'languages', label: t('pages.movies.languages') },
					]"
					:rows="files"
					:row-key="(row) => row.id"
				>
					<template #cell-quality="{ row }">
						{{ t(qualityLabel(row.quality)) }}
					</template>
					<template #cell-size="{ row }">
						{{ formatBytes(row.size) }}
					</template>
					<template #cell-releaseGroup="{ row }">
						<span v-if="row.releaseGroup">{{ row.releaseGroup }}</span>
						<span
							v-else
							class="s-cell-muted"
						>{{ t('pages.movies.none') }}</span>
					</template>
					<template #cell-languages="{ row }">
						<span v-if="row.languages.length > 0">{{ row.languages.map(languageLabel).join(', ') }}</span>
						<span
							v-else
							class="s-cell-muted"
						>{{ t('pages.movies.none') }}</span>
					</template>
				</STable>
				<SEmptyState
					v-else
					:message="t('pages.movies.noFiles')"
				>
					<template #action>
						<SButton
							variant="primary"
							@click="searchMovie"
						>
							{{ t('pages.movies.search') }}
						</SButton>
					</template>
				</SEmptyState>
			</SSection>

			<SSection :title="t('pages.movies.history')">
				<STable
					v-if="history.length > 0"
					:columns="[
						{ key: 'type', label: t('pages.movies.event') },
						{ key: 'sourceTitle', label: t('pages.movies.release') },
						{ key: 'date', label: t('pages.movies.date') },
					]"
					:rows="history"
					:row-key="(row) => row.id"
				>
					<template #cell-type="{ row }">
						{{ t(historyEventTypeLabel(row.type as never)) }}
					</template>
					<template #cell-date="{ row }">
						{{ formatDateTime(row.date) }}
					</template>
				</STable>
				<SEmptyState
					v-else
					:message="t('pages.movies.noActivity')"
				/>
			</SSection>
		</template>

		<SDialog
			v-model="editOpen"
			:title="t('pages.movies.editTitle')"
		>
			<div class="add-form">
				<SField :label="t('pages.movies.minimumAvailability')">
					<SSelect
						v-model="editForm.minimumAvailability"
						:options="minimumAvailabilityOptions.map(option => ({ ...option, label: t(option.label) }))"
					/>
				</SField>
				<SCheckbox
					v-model="editForm.isAnime"
					:label="t('pages.movies.thisIsAnime')"
				/>
				<SField :label="t('pages.movies.rootFolder')">
					<SSelect
						v-model="editForm.rootFolderId"
						:options="rootFolderOptions"
					/>
				</SField>
				<SCheckbox
					v-model="editForm.moveFiles"
					:label="t('pages.movies.moveFilesOnDisk')"
				/>
			</div>
			<template #footer>
				<SButton @click="editOpen = false">
					{{ t('pages.movies.cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="savingEdit"
					@click="saveEdit"
				>
					{{ t('pages.movies.saveChanges') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteOpen"
			:title="t('pages.movies.deleteTitle')"
			:description="t('pages.movies.deleteMovieDescription', { title: movie?.title })"
		>
			<SCheckbox
				v-model="deleteFiles"
				:label="t('pages.movies.deleteFiles')"
			/>
			<SCheckbox
				v-model="deleteExclusion"
				:label="t('pages.movies.importListExclusion')"
			/>
			<template #footer>
				<SButton @click="deleteOpen = false">
					{{ t('pages.movies.cancel') }}
				</SButton>
				<SButton
					variant="danger"
					:loading="deleting"
					@click="confirmDelete"
				>
					{{ t('pages.movies.delete') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="addVersionOpen"
			:title="t('pages.movies.addVersion')"
		>
			<div class="add-form">
				<SField :label="t('pages.movies.name')">
					<SInput
						v-model="addVersionForm.name"
						:placeholder="t('pages.movies.mainPlaceholder')"
					/>
				</SField>
				<SField :label="t('pages.movies.qualityProfile')">
					<SSelect
						v-model="addVersionForm.qualityProfileId"
						:options="qualityOptions"
						:placeholder="t('pages.movies.chooseProfile')"
					/>
				</SField>
				<SField :label="t('pages.movies.languageProfile')">
					<SSelect
						v-model="addVersionForm.languageProfileId"
						:options="languageOptions"
						:placeholder="t('pages.movies.chooseProfile')"
					/>
				</SField>
				<SField :label="t('pages.movies.rootFolder')">
					<SSelect
						v-model="addVersionForm.rootFolderId"
						:options="rootFolderOptions"
						:placeholder="t('pages.movies.movieRootFolder')"
					/>
				</SField>
			</div>
			<template #footer>
				<SButton @click="addVersionOpen = false">
					{{ t('pages.movies.cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="addingVersion"
					@click="submitAddVersion"
				>
					{{ t('pages.movies.addVersion') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteVersionOpen"
			:title="t('pages.movies.removeVersion')"
			:description="t('pages.movies.removeVersionDescription')"
		>
			<SCheckbox
				v-model="deleteVersionFiles"
				:label="t('pages.movies.deleteFiles')"
			/>
			<template #footer>
				<SButton @click="deleteVersionTarget = null">
					{{ t('pages.movies.cancel') }}
				</SButton>
				<SButton
					variant="danger"
					@click="confirmDeleteVersion"
				>
					{{ t('pages.movies.remove') }}
				</SButton>
			</template>
		</SDialog>

		<InteractiveSearchDialog
			v-model="interactiveSearchOpen"
			:movie-id="movieId"
			:version-labels="versionLabels"
		/>
	</div>
</template>

<style scoped>
.movie-backdrop {
	height: 240px;
	margin: -24px -24px 24px;
	background-size: cover;
	background-position: center;
	position: relative;
}

.movie-backdrop::after {
	content: '';
	position: absolute;
	inset: 0;
	background: color-mix(in srgb, var(--bg) 40%, transparent);
}

.movie-header {
	display: flex;
	gap: 24px;
	margin-top: -96px;
	position: relative;
	margin-bottom: 32px;
}

.movie-poster {
	width: 150px;
	flex: none;
	border-radius: var(--r-panel);
	box-shadow: var(--shadow-pop);
}

.movie-facts {
	flex: 1;
	min-width: 0;
	padding-top: 96px;
}

.movie-title-row {
	display: flex;
	align-items: center;
	gap: 12px;
}

.movie-title-row h1 {
	font-size: var(--text-2xl);
	font-weight: 600;
}

.movie-badges {
	display: flex;
	gap: 8px;
	margin-top: 8px;
}

.movie-meta {
	color: var(--fg-muted);
	margin-top: 8px;
	font-size: var(--text-sm);
}

.movie-overview {
	max-width: 72ch;
	margin-top: 12px;
}

.movie-collection-link {
	display: inline-block;
	margin-top: 8px;
	color: var(--accent);
	font-size: var(--text-sm);
}

.movie-collection-link:hover {
	text-decoration: underline;
}

.movie-tags-field {
	max-width: 480px;
	margin-top: 16px;
}

.movie-header-actions {
	padding-top: 96px;
}

.version-list {
	display: flex;
	flex-direction: column;
	gap: 8px;
	margin-bottom: 12px;
}

.version-row {
	display: grid;
	grid-template-columns: 100px 1fr 1fr 1fr auto auto;
	gap: 12px;
	align-items: center;
}

.version-path {
	color: var(--fg-muted);
	font-size: var(--text-sm);
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.add-form {
	display: flex;
	flex-direction: column;
	gap: 12px;
}

@media (max-width: 767px) {
	.movie-header {
		flex-direction: column;
		margin-top: -48px;
	}

	.movie-facts {
		padding-top: 0;
	}

	.movie-header-actions {
		padding-top: 0;
	}

	.version-row {
		grid-template-columns: 1fr;
	}
}
</style>
