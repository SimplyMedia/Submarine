<script setup lang="ts">
import type { components } from '~/types/api'
import {
	addMonitorOptionOptions,
	historyEventTypeLabel,
	monitorNewItemsOptions,
	qualityLabel,
	seriesNumberingOptions,
	seriesStatusLabel,
	seriesTypeLabel,
	seriesTypeOptions,
} from '~/utils/library-labels'

type SeriesDetail = components['schemas']['SeriesDetailDto']
type Episode = components['schemas']['EpisodeDto']
type Version = components['schemas']['VersionDto']
type HistoryEvent = components['schemas']['HistoryEventDto']
type RenamePreview = components['schemas']['RenamePreviewDto']
type AddMonitorOption = NonNullable<components['schemas']['AddMonitorOption']>

const route = useRoute()
const seriesId = Number(route.params.id)

const api = useApi()
const reference = useReferenceStore()
const events = useEvents()
const toast = useToast()

const detail = ref<SeriesDetail | null>(null)
const episodes = ref<Episode[]>([])
const history = ref<HistoryEvent[]>([])
const loading = ref(true)
const loadError = ref('')

const series = computed(() => detail.value?.series ?? null)

useHead({ title: computed(() => series.value?.title ?? 'Series') })

const episodesBySeason = computed(() => {
	const map = new Map<number, Episode[]>()
	for (const episode of episodes.value) {
		const list = map.get(episode.seasonNumber) ?? []
		list.push(episode)
		map.set(episode.seasonNumber, list)
	}
	for (const list of map.values()) {
		list.sort((a, b) => a.episodeNumber - b.episodeNumber)
	}
	return map
})

const seasons = computed(() => [...(detail.value?.seasons ?? [])].sort((a, b) => a.seasonNumber - b.seasonNumber))

async function load() {
	loading.value = true
	loadError.value = ''
	const [detailResult, episodesResult, historyResult] = await Promise.all([
		api.GET('/api/v1/series/{id}', { params: { path: { id: seriesId } } }),
		api.GET('/api/v1/episodes', { params: { query: { seriesId, includeFiles: true } } }),
		api.GET('/api/v1/history/series', { params: { query: { seriesId } } }),
	])
	if (detailResult.data) {
		detail.value = detailResult.data
	}
	else {
		loadError.value = 'Could not load this series. Check your connection and try again.'
	}
	if (episodesResult.data) {
		episodes.value = episodesResult.data
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
	stopHandlers.push(events.on('SeriesUpdatedEvent', (payload) => {
		const id = (payload as { seriesId?: number }).seriesId
		if (id === seriesId) {
			void load()
		}
	}))
	stopHandlers.push(events.on('SeriesDeletedEvent', (payload) => {
		const id = (payload as { seriesId?: number }).seriesId
		if (id === seriesId) {
			toast.toast({ title: 'This series was deleted', tone: 'info' })
			void navigateTo('/series', { replace: true })
		}
	}))
	stopHandlers.push(events.on('CommandUpdated', (payload) => {
		const command = payload as { name?: string, status?: string }
		const trackedNames = ['RefreshSeries', 'RescanSeries', 'SeriesSearch', 'MoveSeries', 'RenameSeries', 'SeasonSearch', 'EpisodeSearch']
		if (command.status === 'COMPLETED' && command.name && trackedNames.includes(command.name)) {
			void load()
		}
	}))
})

onUnmounted(() => {
	stopHandlers.forEach(stop => stop())
})

const rootFolderOptions = computed(() =>
	reference.rootFolders.filter(folder => folder.mediaKind === 'SERIES').map(folder => ({ value: String(folder.id), label: folder.path })),
)
const qualityOptions = computed(() => reference.qualityProfiles.map(profile => ({ value: String(profile.id), label: profile.name })))
const languageOptions = computed(() => reference.languageProfiles.map(profile => ({ value: String(profile.id), label: profile.name })))

async function toggleMonitored(monitored: boolean) {
	if (!series.value) {
		return
	}
	const previous = series.value.monitored
	series.value.monitored = monitored
	const result = await api.PUT('/api/v1/series/{id}', {
		params: { path: { id: seriesId } },
		body: { monitored, seasonFolder: null, seriesType: null, numbering: null, monitorNewItems: null, tagIds: null, rootFolderId: null, moveFiles: null, versions: null },
	})
	if (!result.data) {
		if (series.value) {
			series.value.monitored = previous
		}
		toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
	}
}

const tagIds = ref<number[]>([])
watch(series, (value) => {
	if (value) {
		tagIds.value = [...value.tagIds]
	}
}, { immediate: true })

let tagsSaving = false
watch(tagIds, async (value, previous) => {
	if (tagsSaving || !series.value || JSON.stringify(value) === JSON.stringify(previous)) {
		return
	}
	tagsSaving = true
	try {
		const result = await api.PUT('/api/v1/series/{id}', {
			params: { path: { id: seriesId } },
			body: { tagIds: value, monitored: null, seasonFolder: null, seriesType: null, numbering: null, monitorNewItems: null, rootFolderId: null, moveFiles: null, versions: null },
		})
		if (!result.data) {
			toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
		}
	}
	finally {
		tagsSaving = false
	}
})

async function runCommand(name: string, body: Record<string, unknown> = {}) {
	const result = await api.POST('/api/v1/commands', { body: { name, ...body } })
	if (result.data) {
		const label = name.replace(/([A-Z])/g, ' $1').trim().toLowerCase()
		toast.toast({ title: `${label.charAt(0).toUpperCase()}${label.slice(1)} queued`, tone: 'ok' })
	}
	else {
		toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
	}
}

const actionItems = computed(() => [
	{ label: 'Refresh metadata', icon: 'lucide:refresh-cw', onSelect: () => void refresh() },
	{ label: 'Rescan files', icon: 'lucide:folder-search', onSelect: () => void rescan() },
	{ label: 'Search', icon: 'lucide:search', onSelect: () => void searchSeries() },
	{ label: 'Interactive search', icon: 'lucide:search-check', onSelect: () => { interactiveSearchOpen.value = true } },
	{ label: 'Preview renames', icon: 'lucide:file-text', onSelect: () => void openRenamePreview() },
	{ label: 'Edit', icon: 'lucide:pencil', onSelect: () => openEdit() },
	{ label: 'Delete', icon: 'lucide:trash-2', danger: true, onSelect: () => { deleteOpen.value = true } },
])

async function refresh() {
	const result = await api.POST('/api/v1/series/{id}/refresh', { params: { path: { id: seriesId } } })
	toast.toast({ title: result.data ? 'Refresh queued' : toApiError(result.error, result.response).message, tone: result.data ? 'ok' : 'danger' })
}

async function rescan() {
	const result = await api.POST('/api/v1/series/{id}/rescan', { params: { path: { id: seriesId } } })
	toast.toast({ title: result.data ? 'Rescan queued' : toApiError(result.error, result.response).message, tone: result.data ? 'ok' : 'danger' })
}

async function searchSeries() {
	const result = await api.POST('/api/v1/series/{id}/search', { params: { path: { id: seriesId } } })
	toast.toast({ title: result.data ? 'Search queued' : toApiError(result.error, result.response).message, tone: result.data ? 'ok' : 'danger' })
}

async function searchSeason(seasonNumber: number) {
	await runCommand('SeasonSearch', { seriesId, seasonNumber })
}

async function searchEpisodes(episodeIds: number[]) {
	await runCommand('EpisodeSearch', { episodeIds })
}

const renamePreviewOpen = ref(false)
const renamePreview = ref<RenamePreview[]>([])
const renaming = ref(false)

async function openRenamePreview() {
	const result = await api.GET('/api/v1/rename', { params: { query: { seriesId } } })
	renamePreview.value = result.data ?? []
	renamePreviewOpen.value = true
}

async function applyRename() {
	renaming.value = true
	try {
		const result = await api.POST('/api/v1/rename', { body: { seriesId, movieId: null, fileIds: null } })
		if (result.data) {
			toast.toast({ title: 'Rename queued', tone: 'ok' })
			renamePreviewOpen.value = false
		}
		else {
			toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
		}
	}
	finally {
		renaming.value = false
	}
}

const interactiveSearchOpen = ref(false)
const versionLabels = computed(() => Object.fromEntries((series.value?.versions ?? []).map(v => [v.id, v.name])))

const editOpen = ref(false)
const editForm = reactive({
	seriesType: 'STANDARD' as SeriesDetail['series']['seriesType'],
	numbering: 'AIRED' as SeriesDetail['series']['numbering'],
	seasonFolder: true,
	monitorNewItems: 'ALL' as SeriesDetail['series']['monitorNewItems'],
	rootFolderId: '',
	moveFiles: false,
})
const savingEdit = ref(false)

function openEdit() {
	if (!series.value) {
		return
	}
	editForm.seriesType = series.value.seriesType
	editForm.numbering = series.value.numbering
	editForm.seasonFolder = series.value.seasonFolder
	editForm.monitorNewItems = series.value.monitorNewItems
	editForm.rootFolderId = String(series.value.versions[0]?.rootFolderId ?? '')
	editForm.moveFiles = false
	editOpen.value = true
}

async function saveEdit() {
	savingEdit.value = true
	try {
		const currentRootFolderId = series.value?.versions[0]?.rootFolderId
		const movedRootFolderId = editForm.rootFolderId && Number(editForm.rootFolderId) !== currentRootFolderId ? Number(editForm.rootFolderId) : null
		const result = await api.PUT('/api/v1/series/{id}', {
			params: { path: { id: seriesId } },
			body: {
				monitored: null,
				seriesType: editForm.seriesType,
				numbering: editForm.numbering,
				seasonFolder: editForm.seasonFolder,
				monitorNewItems: editForm.monitorNewItems,
				tagIds: null,
				rootFolderId: movedRootFolderId,
				moveFiles: movedRootFolderId ? editForm.moveFiles : null,
				versions: null,
			},
		})
		if (result.data) {
			detail.value = result.data
			editOpen.value = false
			toast.toast({ title: 'Series updated', tone: 'ok' })
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
		const result = await api.DELETE('/api/v1/series/{id}', {
			params: { path: { id: seriesId }, query: { deleteFiles: deleteFiles.value, addImportListExclusion: deleteExclusion.value } },
		})
		if (result.response.ok) {
			toast.toast({ title: `${series.value?.title ?? 'Series'} deleted`, tone: 'ok' })
			await navigateTo('/series', { replace: true })
		}
		else {
			toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
		}
	}
	finally {
		deleting.value = false
	}
}

async function toggleSeasonMonitored(seasonNumber: number, monitored: boolean) {
	const season = detail.value?.seasons.find(entry => entry.seasonNumber === seasonNumber)
	if (season) {
		season.monitored = monitored
	}
	const result = await api.PUT('/api/v1/series/{seriesId}/seasons/{seasonNumber}/monitor', {
		params: { path: { seriesId, seasonNumber } },
		body: { monitored },
	})
	if (!result.response.ok && season) {
		season.monitored = !monitored
		toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
	}
}

async function toggleEpisodeMonitored(episode: Episode, monitored: boolean) {
	episode.monitored = monitored
	const result = await api.PUT('/api/v1/episodes/monitor', { body: { episodeIds: [episode.id], monitored } })
	if (!result.data) {
		episode.monitored = !monitored
		toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
	}
}

const seasonPassOpen = ref(false)
const seasonPassOption = ref<AddMonitorOption>('ALL')
const seasonPassSpecials = ref(false)
const seasonPassSaving = ref(false)

async function applySeasonPass() {
	seasonPassSaving.value = true
	try {
		const result = await api.PUT('/api/v1/series/{seriesId}/seasons/season-pass', {
			params: { path: { seriesId } },
			body: { seasons: null, monitoringOption: seasonPassOption.value, monitorSpecials: seasonPassSpecials.value },
		})
		if (result.response.ok) {
			toast.toast({ title: 'Season pass applied', tone: 'ok' })
			seasonPassOpen.value = false
			await load()
		}
		else {
			toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
		}
	}
	finally {
		seasonPassSaving.value = false
	}
}

const addVersionOpen = ref(false)
const addVersionForm = reactive({ name: '', qualityProfileId: '', languageProfileId: '', rootFolderId: '' })
const addingVersion = ref(false)

async function submitAddVersion() {
	addingVersion.value = true
	try {
		const result = await api.POST('/api/v1/series/{seriesId}/versions', {
			params: { path: { seriesId } },
			body: {
				name: addVersionForm.name || null,
				qualityProfileId: Number(addVersionForm.qualityProfileId || reference.qualityProfiles[0]?.id),
				languageProfileId: Number(addVersionForm.languageProfileId || reference.languageProfiles[0]?.id),
				rootFolderId: addVersionForm.rootFolderId ? Number(addVersionForm.rootFolderId) : null,
			},
		})
		if (result.data && detail.value) {
			detail.value.series.versions = [...detail.value.series.versions, result.data]
			addVersionOpen.value = false
			toast.toast({ title: 'Version added', tone: 'ok' })
		}
		else {
			toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
		}
	}
	finally {
		addingVersion.value = false
	}
}

async function updateVersion(version: Version, changes: { qualityProfileId?: number, languageProfileId?: number }) {
	const result = await api.PUT('/api/v1/media-versions/{id}', {
		params: { path: { id: version.id } },
		body: { name: null, path: null, monitored: null, rootFolderId: null, qualityProfileId: changes.qualityProfileId ?? null, languageProfileId: changes.languageProfileId ?? null },
	})
	if (result.data && detail.value) {
		Object.assign(version, result.data)
	}
	else {
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
		detail.value.series.versions = detail.value.series.versions.filter(version => version.id !== deleteVersionTarget.value?.id)
		toast.toast({ title: 'Version removed', tone: 'ok' })
	}
	else {
		toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
	}
	deleteVersionTarget.value = null
}
</script>

<template>
	<div>
		<SSpinner v-if="loading && !series" />
		<SEmptyState
			v-else-if="!series"
			:message="loadError || 'This series could not be found.'"
		>
			<template #action>
				<SButton @click="navigateTo('/series')">
					Back to series
				</SButton>
			</template>
		</SEmptyState>
		<template v-else>
			<div
				v-if="series.backdropUrl"
				class="series-backdrop"
				:style="{ backgroundImage: `url(${series.backdropUrl})` }"
			/>
			<div class="series-header">
				<SPosterImage
					:src="series.posterUrl"
					:alt="series.title"
					class="series-poster"
				/>
				<div class="series-facts">
					<div class="series-title-row">
						<h1>{{ series.title }}<span v-if="series.year"> ({{ series.year }})</span></h1>
						<MonitorToggle
							:model-value="series.monitored"
							:label="`Toggle monitored for ${series.title}`"
							@update:model-value="toggleMonitored"
						/>
					</div>
					<div class="series-badges">
						<SBadge :tone="series.status === 'CONTINUING' ? 'ok' : 'neutral'">
							{{ seriesStatusLabel(series.status) }}
						</SBadge>
						<SBadge tone="neutral">
							{{ seriesTypeLabel(series.seriesType) }}
						</SBadge>
						<SBadge
							v-if="series.certification"
							tone="neutral"
						>
							{{ series.certification }}
						</SBadge>
					</div>
					<p class="series-meta">
						<span v-if="series.network">{{ series.network }}</span>
						<span v-if="series.runtime"> · {{ series.runtime }} min</span>
						<span v-if="series.genres.length"> · {{ series.genres.join(', ') }}</span>
					</p>
					<p
						v-if="series.overview"
						class="series-overview"
					>
						{{ series.overview }}
					</p>
					<SField
						label="Tags"
						class="series-tags-field"
					>
						<TagPicker v-model:tag-ids="tagIds" />
					</SField>
				</div>
				<div class="series-header-actions">
					<SDropdownMenu :items="actionItems">
						<template #trigger>
							<SButton variant="secondary">
								Actions
							</SButton>
						</template>
					</SDropdownMenu>
				</div>
			</div>

			<SSection title="Versions">
				<template #default>
					<div class="version-list">
						<div
							v-for="version in series.versions"
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
								:label="`Toggle monitored for version ${version.name}`"
								disabled
							/>
							<SIconButton
								label="Remove version"
								:disabled="series.versions.length <= 1"
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
						Add version
					</SButton>
				</template>
			</SSection>

			<SSection title="Seasons">
				<template #default>
					<div class="season-pass-row">
						<SButton
							size="sm"
							@click="seasonPassOpen = true"
						>
							Season pass
						</SButton>
					</div>
					<details
						v-for="season in seasons"
						:key="season.seasonNumber"
						class="season-block"
						open
					>
						<summary class="season-summary">
							<span class="season-title">{{ season.seasonNumber === 0 ? 'Specials' : `Season ${season.seasonNumber}` }}</span>
							<SProgress
								class="season-progress"
								:value="season.statistics.episodeCount > 0 ? (season.statistics.episodeFileCount / season.statistics.episodeCount) * 100 : 0"
								:label="`Season ${season.seasonNumber} episodes on disk`"
							/>
							<span class="season-stats">{{ season.statistics.episodeFileCount }} / {{ season.statistics.episodeCount }}</span>
							<SButton
								size="sm"
								@click.prevent="searchSeason(season.seasonNumber)"
							>
								Search season
							</SButton>
							<MonitorToggle
								:model-value="season.monitored"
								:label="`Toggle monitored for season ${season.seasonNumber}`"
								@update:model-value="toggleSeasonMonitored(season.seasonNumber, $event)"
							/>
						</summary>
						<STable
							:columns="[
								{ key: 'episodeNumber', label: '#' },
								{ key: 'title', label: 'Title' },
								{ key: 'airDate', label: 'Air date' },
								{ key: 'file', label: 'Quality' },
								{ key: 'monitored', label: 'Monitored', align: 'right' },
								{ key: 'actions', label: '', align: 'right' },
							]"
							:rows="episodesBySeason.get(season.seasonNumber) ?? []"
							:row-key="(row) => row.id"
						>
							<template #cell-episodeNumber="{ row }">
								{{ row.episodeNumber }}
							</template>
							<template #cell-title="{ row }">
								{{ row.title || `Episode ${row.episodeNumber}` }}
							</template>
							<template #cell-airDate="{ row }">
								<span v-if="row.airDate">{{ formatDate(row.airDate) }}</span>
								<span
									v-else
									class="s-cell-muted"
								>None</span>
							</template>
							<template #cell-file="{ row }">
								<SBadge :tone="row.hasFile ? 'ok' : 'neutral'">
									{{ row.hasFile ? qualityLabel(row.files[0]?.quality) : 'No file' }}
								</SBadge>
							</template>
							<template #cell-monitored="{ row }">
								<MonitorToggle
									:model-value="row.monitored"
									:label="`Toggle monitored for episode ${row.episodeNumber}`"
									@update:model-value="toggleEpisodeMonitored(row, $event)"
								/>
							</template>
							<template #cell-actions="{ row }">
								<SIconButton
									label="Search episode"
									@click="searchEpisodes([row.id])"
								>
									<Icon
										name="lucide:search"
										aria-hidden="true"
									/>
								</SIconButton>
							</template>
						</STable>
					</details>
				</template>
			</SSection>

			<SSection title="History">
				<STable
					v-if="history.length > 0"
					:columns="[
						{ key: 'type', label: 'Event' },
						{ key: 'sourceTitle', label: 'Release' },
						{ key: 'date', label: 'Date' },
					]"
					:rows="history"
					:row-key="(row) => row.id"
				>
					<template #cell-type="{ row }">
						{{ historyEventTypeLabel(row.type as never) }}
					</template>
					<template #cell-date="{ row }">
						{{ formatDateTime(row.date) }}
					</template>
				</STable>
				<SEmptyState
					v-else
					message="No activity yet"
				/>
			</SSection>
		</template>

		<SDialog
			v-model="editOpen"
			title="Edit series"
		>
			<div class="add-form">
				<SField label="Series type">
					<SSelect
						v-model="editForm.seriesType"
						:options="seriesTypeOptions"
					/>
				</SField>
				<SField label="Episode numbering">
					<SSelect
						v-model="editForm.numbering"
						:options="seriesNumberingOptions"
					/>
				</SField>
				<SField label="New seasons">
					<SSelect
						v-model="editForm.monitorNewItems"
						:options="monitorNewItemsOptions"
					/>
				</SField>
				<SCheckbox
					v-model="editForm.seasonFolder"
					label="Use a season folder"
				/>
				<SField label="Root folder">
					<SSelect
						v-model="editForm.rootFolderId"
						:options="rootFolderOptions"
					/>
				</SField>
				<SCheckbox
					v-model="editForm.moveFiles"
					label="Move files on disk"
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
			v-model="deleteOpen"
			title="Delete series"
			:description="`This removes ${series?.title} from the library.`"
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
				<SButton @click="deleteOpen = false">
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

		<SDialog
			v-model="seasonPassOpen"
			title="Season pass"
			description="Set monitoring across every season at once."
		>
			<div class="add-form">
				<SField label="Monitor">
					<SSelect
						v-model="seasonPassOption"
						:options="addMonitorOptionOptions"
					/>
				</SField>
				<SCheckbox
					v-model="seasonPassSpecials"
					label="Include specials"
				/>
			</div>
			<template #footer>
				<SButton @click="seasonPassOpen = false">
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="seasonPassSaving"
					@click="applySeasonPass"
				>
					Apply
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="addVersionOpen"
			title="Add version"
		>
			<div class="add-form">
				<SField label="Name">
					<SInput
						v-model="addVersionForm.name"
						placeholder="Main"
					/>
				</SField>
				<SField label="Quality profile">
					<SSelect
						v-model="addVersionForm.qualityProfileId"
						:options="qualityOptions"
						placeholder="Choose a profile"
					/>
				</SField>
				<SField label="Language profile">
					<SSelect
						v-model="addVersionForm.languageProfileId"
						:options="languageOptions"
						placeholder="Choose a profile"
					/>
				</SField>
				<SField label="Root folder">
					<SSelect
						v-model="addVersionForm.rootFolderId"
						:options="rootFolderOptions"
						placeholder="Use the series root folder"
					/>
				</SField>
			</div>
			<template #footer>
				<SButton @click="addVersionOpen = false">
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="addingVersion"
					@click="submitAddVersion"
				>
					Add version
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteVersionOpen"
			title="Remove version"
			description="This removes the version and, if selected, its files."
		>
			<SCheckbox
				v-model="deleteVersionFiles"
				label="Delete files on disk"
			/>
			<template #footer>
				<SButton @click="deleteVersionTarget = null">
					Cancel
				</SButton>
				<SButton
					variant="danger"
					@click="confirmDeleteVersion"
				>
					Remove
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="renamePreviewOpen"
			title="Preview renames"
			wide
		>
			<STable
				:columns="[{ key: 'existingPath', label: 'Current' }, { key: 'newPath', label: 'New' }]"
				:rows="renamePreview"
				:row-key="(row) => row.fileId"
			>
				<template #empty>
					<SEmptyState message="Every file already matches the naming format." />
				</template>
			</STable>
			<template #footer>
				<SButton @click="renamePreviewOpen = false">
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:disabled="renamePreview.length === 0"
					:loading="renaming"
					@click="applyRename"
				>
					Rename {{ renamePreview.length }} file(s)
				</SButton>
			</template>
		</SDialog>

		<InteractiveSearchDialog
			v-model="interactiveSearchOpen"
			:series-id="seriesId"
			:version-labels="versionLabels"
		/>
	</div>
</template>

<style scoped>
.series-backdrop {
	height: 240px;
	margin: -24px -24px 24px;
	background-size: cover;
	background-position: center;
	position: relative;
}

.series-backdrop::after {
	content: '';
	position: absolute;
	inset: 0;
	background: color-mix(in srgb, var(--bg) 40%, transparent);
}

.series-header {
	display: flex;
	gap: 24px;
	margin-top: -96px;
	position: relative;
	margin-bottom: 32px;
}

.series-poster {
	width: 150px;
	flex: none;
	border-radius: var(--r-panel);
	box-shadow: var(--shadow-pop);
}

.series-facts {
	flex: 1;
	min-width: 0;
	padding-top: 96px;
}

.series-title-row {
	display: flex;
	align-items: center;
	gap: 12px;
}

.series-title-row h1 {
	font-size: var(--text-2xl);
	font-weight: 600;
}

.series-badges {
	display: flex;
	gap: 8px;
	margin-top: 8px;
}

.series-meta {
	color: var(--fg-muted);
	margin-top: 8px;
	font-size: var(--text-sm);
}

.series-overview {
	max-width: 72ch;
	margin-top: 12px;
}

.series-tags-field {
	max-width: 480px;
	margin-top: 16px;
}

.series-header-actions {
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

.season-pass-row {
	margin-bottom: 12px;
}

.season-block {
	border-top: 1px solid var(--line);
	padding: 12px 0;
}

.season-summary {
	display: flex;
	align-items: center;
	gap: 16px;
	cursor: pointer;
	list-style: none;
}

.season-summary::-webkit-details-marker {
	display: none;
}

.season-title {
	font-weight: 500;
	min-width: 110px;
}

.season-progress {
	flex: 1;
	max-width: 200px;
}

.season-stats {
	color: var(--fg-muted);
	font-size: var(--text-sm);
	min-width: 60px;
}

.add-form {
	display: flex;
	flex-direction: column;
	gap: 12px;
}

@media (max-width: 767px) {
	.series-header {
		flex-direction: column;
		margin-top: -48px;
	}

	.series-facts {
		padding-top: 0;
	}

	.series-header-actions {
		padding-top: 0;
	}

	.version-row {
		grid-template-columns: 1fr;
	}
}
</style>
