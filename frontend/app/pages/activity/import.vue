<script setup lang="ts">
import { useI18n } from 'vue-i18n'
/**
 * Manual import: scan a folder (or a queue item's output, via ?downloadId=)
 * for candidate media files, map each to a series/movie, episodes, version
 * and quality/language overrides, then import the selected ones.
 */
import { toApiError, useApi } from '~/composables/useApi'
import { formatBytes } from '~/composables/useFormat'
import { humanizeEnumValue } from '~/utils/settings-labels'
import { qualityLabel } from '~/utils/library-labels'
import { navChildren } from '~/navigation'
import type { LibraryMediaOption } from '~/components/import/LibraryMediaPicker.vue'
import type { components } from '~/types/api'

type ManualImportCandidateDto = components['schemas']['ManualImportCandidateDto']
type ManualImportFileResultDto = components['schemas']['ManualImportFileResultDto']
type QualityDefinitionResource = components['schemas']['QualityDefinitionResource']
type QualityModel = components['schemas']['QualityModel']
type QualitySource = components['schemas']['QualitySource']
type QualityResolution = components['schemas']['QualityResolution']
type Language = components['schemas']['Language']
type VersionDto = components['schemas']['VersionDto']

interface RowEdit {
	seriesId: number | null
	movieId: number | null
	episodeIds: number[]
	mediaVersionId: number | null
	qualityKey: string
	languages: Language[]
	releaseGroup: string
}

const { t } = useI18n()
useHead({ title: t('pages.activity.import.title') })

const api = useApi()
const { toast } = useToast()
const route = useRoute()

function queryString(value: unknown): string {
	return Array.isArray(value) ? (value[0] ?? '') : (typeof value === 'string' ? value : '')
}

const downloadId = ref(queryString(route.query.downloadId))
const seriesScope = ref(queryString(route.query.seriesId) ? Number(queryString(route.query.seriesId)) : null)
const movieScope = ref(queryString(route.query.movieId) ? Number(queryString(route.query.movieId)) : null)
const folder = ref(queryString(route.query.folder))

const candidates = ref<ManualImportCandidateDto[]>([])
const edits = reactive<Record<string, RowEdit>>({})
const importResults = reactive<Record<string, ManualImportFileResultDto>>({})
const versionsCache = reactive<Record<string, VersionDto[]>>({})
const seriesOptions = ref<LibraryMediaOption[]>([])
const movieOptions = ref<LibraryMediaOption[]>([])
const qualityDefinitions = ref<QualityDefinitionResource[]>([])
const selected = ref<string[]>([])
const importMode = ref<'move' | 'copy'>('move')
const scanning = ref(false)
const scanError = ref('')
const importing = ref(false)
const scanned = ref(false)

const importModeOptions = [
	{ value: 'move', label: t('pages.activity.import.move') },
	{ value: 'copy', label: t('pages.activity.import.copy') },
]

const qualityOptions = computed(() => [
	{ value: '', label: t('pages.activity.import.keepParsedQuality') },
	...qualityDefinitions.value.map(definition => ({ value: `${definition.source ?? ''}|${definition.resolution ?? ''}`, label: definition.title })),
])

onMounted(async () => {
	const [seriesResult, moviesResult, qualityResult] = await Promise.all([
		api.GET('/api/v1/series', { params: { query: { PageSize: 500, SortKey: 'SortTitle' } } }),
		api.GET('/api/v1/movies', { params: { query: { PageSize: 500, SortKey: 'SortTitle' } } }),
		api.GET('/api/v1/quality-definitions'),
	])
	seriesOptions.value = (seriesResult.data?.items ?? []).map(series => ({ id: series.id, title: series.title, year: series.year }))
	movieOptions.value = (moviesResult.data?.items ?? []).map(movie => ({ id: movie.id, title: movie.title, year: movie.year }))
	qualityDefinitions.value = qualityResult.data ?? []

	if (downloadId.value) {
		await scan()
	}
})

async function ensureVersions(kind: 'series' | 'movie', id: number): Promise<VersionDto[]> {
	const key = `${kind}-${id}`
	if (versionsCache[key]) {
		return versionsCache[key]
	}
	let versions: VersionDto[]
	if (kind === 'series') {
		const result = await api.GET('/api/v1/series/{id}', { params: { path: { id } } })
		versions = result.data?.series.versions ?? []
	}
	else {
		const result = await api.GET('/api/v1/movies/{id}', { params: { path: { id } } })
		versions = result.data?.movie.versions ?? []
	}
	versionsCache[key] = versions
	return versions
}

function versionOptionsFor(row: RowEdit) {
	const key = row.seriesId != null ? `series-${row.seriesId}` : row.movieId != null ? `movie-${row.movieId}` : null
	return key ? (versionsCache[key] ?? []).map(version => ({ value: String(version.id), label: version.name })) : []
}

function initRow(candidate: ManualImportCandidateDto): RowEdit {
	return {
		seriesId: candidate.seriesId,
		movieId: candidate.movieId,
		episodeIds: [...candidate.episodeIds],
		mediaVersionId: candidate.mediaVersionId,
		qualityKey: '',
		languages: [...candidate.languages],
		releaseGroup: candidate.releaseGroup ?? '',
	}
}

async function scan() {
	if (!folder.value.trim()) {
		toast({ title: t('pages.activity.import.chooseFolder'), tone: 'danger' })
		return
	}
	scanning.value = true
	scanError.value = ''
	try {
		const result = await api.GET('/api/v1/manual-import', {
			params: {
				query: {
					folder: folder.value.trim(),
					seriesId: seriesScope.value ?? undefined,
					movieId: movieScope.value ?? undefined,
					downloadId: downloadId.value || undefined,
				},
			},
		})
		if (!result.data) {
			scanError.value = toApiError(result.error, result.response).message
			return
		}
		candidates.value = result.data
		scanned.value = true
		for (const key of Object.keys(importResults)) {
			Reflect.deleteProperty(importResults, key)
		}
		for (const key of Object.keys(edits)) {
			Reflect.deleteProperty(edits, key)
		}
		for (const candidate of result.data) {
			edits[candidate.path] = initRow(candidate)
			if (candidate.seriesId != null) {
				const versions = await ensureVersions('series', candidate.seriesId)
				if (edits[candidate.path]!.mediaVersionId == null && versions.length === 1) {
					edits[candidate.path]!.mediaVersionId = versions[0]!.id
				}
			}
			else if (candidate.movieId != null) {
				const versions = await ensureVersions('movie', candidate.movieId)
				if (edits[candidate.path]!.mediaVersionId == null && versions.length === 1) {
					edits[candidate.path]!.mediaVersionId = versions[0]!.id
				}
			}
		}
		selected.value = result.data.filter(candidate => candidate.rejection == null).map(candidate => candidate.path)
	}
	finally {
		scanning.value = false
	}
}

async function onSeriesPick(path: string, seriesId: number | null) {
	const row = edits[path]
	if (!row) {
		return
	}
	row.seriesId = seriesId
	row.movieId = null
	row.episodeIds = []
	row.mediaVersionId = null
	if (seriesId != null) {
		const versions = await ensureVersions('series', seriesId)
		if (versions.length === 1) {
			row.mediaVersionId = versions[0]!.id
		}
	}
}

async function onMoviePick(path: string, movieId: number | null) {
	const row = edits[path]
	if (!row) {
		return
	}
	row.movieId = movieId
	row.seriesId = null
	row.mediaVersionId = null
	if (movieId != null) {
		const versions = await ensureVersions('movie', movieId)
		if (versions.length === 1) {
			row.mediaVersionId = versions[0]!.id
		}
	}
}

const allSelected = computed(() => candidates.value.length > 0 && candidates.value.every(candidate => selected.value.includes(candidate.path)))

function toggleAll(value: boolean) {
	selected.value = value ? candidates.value.map(candidate => candidate.path) : []
}

function toggleRow(path: string, value: boolean) {
	selected.value = value ? [...selected.value, path] : selected.value.filter(existing => existing !== path)
}

function fileName(path: string): string {
	return path.split(/[/\\]/).pop() ?? path
}

function buildQuality(key: string): QualityModel | null {
	if (!key) {
		return null
	}
	const [source, resolution] = key.split('|')
	return {
		resolution: {
			source: (source || null) as QualitySource,
			resolution: (resolution || null) as QualityResolution,
		},
		revision: { version: 1, isRepack: false, isProper: false, isReal: false },
	}
}

const columns = [
	{ key: 'select', label: '' },
	{ key: 'file', label: t('pages.activity.import.file') },
	{ key: 'match', label: t('pages.activity.import.match') },
	{ key: 'episodes', label: t('pages.activity.import.episodes') },
	{ key: 'version', label: t('pages.activity.import.version') },
	{ key: 'quality', label: t('pages.activity.import.quality') },
	{ key: 'languages', label: t('pages.activity.import.languages') },
	{ key: 'releaseGroup', label: t('pages.activity.import.releaseGroup') },

]

async function runImport() {
	const rows = selected.value
		.map(path => ({ path, candidate: candidates.value.find(candidate => candidate.path === path), edit: edits[path] }))
		.filter((row): row is { path: string, candidate: ManualImportCandidateDto, edit: RowEdit } => row.candidate != null && row.edit != null)

	if (rows.length === 0) {
		toast({ title: t('pages.activity.import.selectAtLeastOneFile'), tone: 'danger' })
		return
	}
	const invalid = rows.filter(({ edit }) => (edit.seriesId == null && edit.movieId == null) || edit.mediaVersionId == null)
	if (invalid.length > 0) {
		toast({ title: t('pages.activity.import.filesNeedMatchAndVersion', { count: invalid.length }), tone: 'danger' })
		return
	}

	importing.value = true
	try {
		const result = await api.POST('/api/v1/manual-import', {
			body: {
				items: rows.map(({ path, edit }) => ({
					path,
					seriesId: edit.seriesId,
					episodeIds: edit.seriesId != null ? edit.episodeIds : null,
					movieId: edit.movieId,
					mediaVersionId: edit.mediaVersionId!,
					quality: buildQuality(edit.qualityKey),
					languages: edit.languages.length > 0 ? edit.languages : null,
					releaseGroup: edit.releaseGroup.trim() || null,
					downloadId: downloadId.value || null,
				})),
				importMode: importMode.value,
			},
		})
		if (!result.data) {
			toast({ title: t('pages.activity.import.importFailed'), description: toApiError(result.error, result.response).message, tone: 'danger' })
			return
		}
		for (const file of result.data.files) {
			importResults[file.path] = file
		}
		const importedPaths = new Set(result.data.files.filter(file => file.imported).map(file => file.path))
		candidates.value = candidates.value.filter(candidate => !importedPaths.has(candidate.path))
		for (const path of importedPaths) {
			Reflect.deleteProperty(edits, path)
			Reflect.deleteProperty(importResults, path)
		}
		selected.value = selected.value.filter(path => !importedPaths.has(path))
		const failedCount = rows.length - importedPaths.size
		toast({
			title: importedPaths.size > 0 ? t('pages.activity.import.importedFiles', { count: importedPaths.size }) : t('pages.activity.import.noFilesImported'),
			description: failedCount > 0 ? t('pages.activity.import.filesFailed', { count: failedCount }) : undefined,
			tone: importedPaths.size > 0 ? 'ok' : 'danger',
		})
	}
	finally {
		importing.value = false
	}
}
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.activity.import.title')">
			<template #actions>
				<SSelect
					v-model="importMode"
					:options="importModeOptions"
				/>
				<SButton
					variant="primary"
					:loading="importing"
					:disabled="selected.length === 0"
					@click="runImport"
				>
					{{ t('pages.activity.import.importSelected', { count: selected.length }) }}
				</SButton>
			</template>
		</SPageHeader>

		<SubNav
			:label="t('pages.activity.import.activityNav')"
			:items="navChildren('activity')"
		/>

		<SSection>
			<div class="import-scan-bar">
				<SField
					:label="t('pages.activity.import.folder')"
					control-id="import-folder"
					class="import-scan-field"
				>
					<PathPicker
						v-model="folder"
						control-id="import-folder"
						:placeholder="t('pages.activity.import.folderPlaceholder')"
					/>
				</SField>
				<SButton
					:loading="scanning"
					@click="scan"
				>
					{{ t('pages.activity.import.scanFolder') }}
				</SButton>
			</div>
			<p
				v-if="scanError"
				class="s-field-error"
				role="alert"
			>
				{{ scanError }}
			</p>
		</SSection>

		<SSpinner v-if="scanning && !scanned" />
		<SEmptyState
			v-else-if="scanned && candidates.length === 0"
			:message="t('pages.activity.import.noCandidateFiles')"
			icon="lucide:folder-search"
		/>
		<template v-else-if="candidates.length > 0">
			<SCheckbox
				:model-value="allSelected"
				:label="t('pages.activity.import.selectAll')"
				class="import-select-all"
				@update:model-value="toggleAll"
			/>
			<STable
				:columns="columns"
				:rows="candidates"
				:row-key="(row) => row.path"
			>
				<template #cell-select="{ row }">
					<SCheckbox
						:model-value="selected.includes(row.path)"
						:aria-label="t('pages.activity.import.selectFile', { file: fileName(row.path) })"
						@update:model-value="value => toggleRow(row.path, value)"
					/>
				</template>
				<template #cell-file="{ row }">
					<div class="import-file-cell">
						<span class="import-file-name">{{ fileName(row.path) }}</span>
						<span class="import-file-size">{{ formatBytes(row.size) }}</span>
						<span
							v-if="row.rejection"
							class="import-file-rejection"
						>{{ t(humanizeEnumValue(row.rejection)) }}</span>
						<span
							v-if="importResults[row.path] && !importResults[row.path]!.imported"
							class="import-file-rejection"
						>{{ importResults[row.path]!.message ?? t('pages.activity.import.importFailedFallback') }}</span>
					</div>
				</template>
				<template #cell-match="{ row }">
					<div class="import-match-cell">
						<LibraryMediaPicker
							:options="seriesOptions"
							:placeholder="t('pages.activity.import.pickSeries')"
							:model-value="edits[row.path]?.seriesId ?? null"
							@update:model-value="value => onSeriesPick(row.path, value)"
						/>
						<LibraryMediaPicker
							:options="movieOptions"
							:placeholder="t('pages.activity.import.pickMovie')"
							:model-value="edits[row.path]?.movieId ?? null"
							@update:model-value="value => onMoviePick(row.path, value)"
						/>
					</div>
				</template>
				<template #cell-episodes="{ row }">
					<EpisodePicker
						v-if="edits[row.path]"
						v-model="edits[row.path]!.episodeIds"
						:series-id="edits[row.path]!.seriesId"
					/>
				</template>
				<template #cell-version="{ row }">
					<SSelect
						v-if="edits[row.path]"
						:model-value="edits[row.path]!.mediaVersionId != null ? String(edits[row.path]!.mediaVersionId) : undefined"
						:options="versionOptionsFor(edits[row.path]!)"
						:placeholder="t('pages.activity.import.chooseVersion')"
						@update:model-value="value => { edits[row.path]!.mediaVersionId = Number(value) }"
					/>
				</template>
				<template #cell-quality="{ row }">
					<SSelect
						v-if="edits[row.path]"
						v-model="edits[row.path]!.qualityKey"
						:options="qualityOptions"
					/>
					<span class="import-parsed-quality">{{ t(qualityLabel(row.quality)) }} {{ t('pages.activity.import.parsed') }}</span>
				</template>
				<template #cell-languages="{ row }">
					<LanguagePicker
						v-if="edits[row.path]"
						v-model="edits[row.path]!.languages"
					/>
				</template>
				<template #cell-releaseGroup="{ row }">
					<SInput
						v-if="edits[row.path]"
						v-model="edits[row.path]!.releaseGroup"
						:placeholder="t('pages.activity.import.releaseGroup')"
					/>
				</template>
			</STable>
		</template>
	</div>
</template>

<style scoped>
.import-scan-bar {
	display: flex;
	align-items: flex-end;
	gap: 12px;
}

.import-scan-field {
	flex: 1;
	min-width: 0;
	max-width: 480px;
}

.import-select-all {
	margin-bottom: 12px;
}

.import-file-cell {
	display: flex;
	flex-direction: column;
	gap: 2px;
	max-width: 260px;
}

.import-file-name {
	font-weight: 500;
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.import-file-size {
	font-size: 0.75rem;
	color: var(--fg-muted);
}

.import-file-rejection {
	font-size: 0.75rem;
	color: var(--warn);
}

.import-match-cell {
	display: flex;
	flex-direction: column;
	gap: 6px;
}

.import-parsed-quality {
	display: block;
	margin-top: 4px;
	font-size: 0.75rem;
	color: var(--fg-muted);
}
</style>
