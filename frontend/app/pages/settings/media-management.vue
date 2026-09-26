<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useDirtyForm } from '~/composables/useDirtyForm'
import { useReferenceStore } from '~/stores/reference'
import {
	colonReplacementOptions,
	downloadPropersAndRepacksOptions,
	fileDateOptions,
	multiEpisodeStyleOptions, mediaKindOptions,
} from '~/utils/settings-labels'
import type { components } from '~/types/api'

const { t } = useI18n()

type MediaManagementConfigResource = components['schemas']['MediaManagementConfigResource']
type NamingConfigResource = components['schemas']['NamingConfigResource']
type RootFolderDto = components['schemas']['RootFolderDto']
type NamingSample = components['schemas']['NamingSample']

definePageMeta({ layout: 'default' })
useHead({ title: t('pages.settings.mediaManagement.title', 'Media management') })

const api = useApi()
const reference = useReferenceStore()
const { toast } = useToast()

const loading = ref(true)

// --- Media management config (manual save) ---------------------------------
const mediaDraft = ref<MediaManagementConfigResource | null>(null)
const mediaDirty = useDirtyForm(mediaDraft)
const mediaSaving = ref(false)

// --- Naming config (autosaves, drives the live preview) --------------------
const namingDraft = ref<NamingConfigResource | null>(null)
const examples = ref<NamingSample[]>([])
const namingStatus = ref<'idle' | 'saving' | 'error'>('idle')

const NAMING_SAMPLE_LABELS: Record<string, string> = {
	StandardEpisodeFormat: 'pages.settings.mediaManagement.samples.standardEpisode',
	DailyEpisodeFormat: 'pages.settings.mediaManagement.samples.dailyEpisode',
	AnimeEpisodeFormat: 'pages.settings.mediaManagement.samples.animeEpisode',
	SeriesFolderFormat: 'pages.settings.mediaManagement.samples.seriesFolder',
	SeasonFolderFormat: 'pages.settings.mediaManagement.samples.seasonFolder',
	SpecialsFolderFormat: 'pages.settings.mediaManagement.samples.specialsFolder',
	MovieFormat: 'pages.settings.mediaManagement.samples.movie',
	MovieFolderFormat: 'pages.settings.mediaManagement.samples.movieFolder',
}
const NAMING_SAMPLE_FALLBACKS: Record<string, string> = {
	StandardEpisodeFormat: 'Standard episode',
	DailyEpisodeFormat: 'Daily episode',
	AnimeEpisodeFormat: 'Anime episode',
	SeriesFolderFormat: 'Series folder',
	SeasonFolderFormat: 'Season folder',
	SpecialsFolderFormat: 'Specials folder',
	MovieFormat: 'Movie',
	MovieFolderFormat: 'Movie folder',
}

async function refreshExamples() {
	const result = await api.GET('/api/v1/config/naming/examples')
	if (result.data) {
		examples.value = result.data
	}
}

const saveNaming = useDebounceFn(async () => {
	if (!namingDraft.value) {
		return
	}
	namingStatus.value = 'saving'
	const result = await api.PUT('/api/v1/config/naming', { body: namingDraft.value })
	if (!result.data) {
		namingStatus.value = 'error'
		return
	}
	namingDraft.value = result.data
	namingStatus.value = 'idle'
	await refreshExamples()
}, 600)

watch(namingDraft, () => {
	void saveNaming()
}, { deep: true })

// --- Root folders ------------------------------------------------------------
const rootFolderDialogOpen = ref(false)
const rootFolderPath = ref('')
const rootFolderKind = ref<'SERIES' | 'MOVIES'>('SERIES')
const rootFolderError = ref('')
const rootFolderSaving = ref(false)
const deleteFolderTarget = ref<RootFolderDto | null>(null)
const deletingFolder = ref(false)
const deleteFolderError = ref('')

const deleteFolderTargetOpen = computed({
	get: () => deleteFolderTarget.value !== null,
	set: (value: boolean) => {
		if (!value) {
			deleteFolderTarget.value = null
		}
	},
})

function formatBytesGb(bytes: number | null): string {
	if (bytes === null) {
		return t('pages.settings.mediaManagement.unknown', 'Unknown')
	}
	return `${(bytes / 1024 / 1024 / 1024).toFixed(1)} GB`
}

function openAddRootFolder() {
	rootFolderPath.value = ''
	rootFolderKind.value = 'SERIES'
	rootFolderError.value = ''
	rootFolderDialogOpen.value = true
}

async function saveRootFolder() {
	rootFolderError.value = ''
	rootFolderSaving.value = true
	try {
		const result = await api.POST('/api/v1/root-folders', {
			body: { path: rootFolderPath.value, mediaKind: rootFolderKind.value },
		})
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: t('pages.settings.mediaManagement.rootFolderAdded', 'Root folder added'), tone: 'ok' })
		rootFolderDialogOpen.value = false
		await reference.load(true)
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		rootFolderError.value = apiError.fieldErrors.path?.[0] ?? apiError.message
	}
	finally {
		rootFolderSaving.value = false
	}
}

async function deleteRootFolder() {
	if (!deleteFolderTarget.value) {
		return
	}
	deletingFolder.value = true
	deleteFolderError.value = ''
	const result = await api.DELETE('/api/v1/root-folders/{id}', { params: { path: { id: deleteFolderTarget.value.id } } })
	if (!result.response.ok) {
		deleteFolderError.value = toApiError(result.error, result.response).message
		deletingFolder.value = false
		return
	}
	toast({ title: t('pages.settings.mediaManagement.rootFolderRemoved', 'Root folder removed'), tone: 'ok' })
	deleteFolderTarget.value = null
	deletingFolder.value = false
	await reference.load(true)
}

async function saveMediaConfig() {
	if (!mediaDraft.value) {
		return
	}
	mediaSaving.value = true
	const result = await api.PUT('/api/v1/config/media-management', { body: mediaDraft.value })
	mediaSaving.value = false
	if (!result.data) {
		toast({ title: t('pages.settings.mediaManagement.saveError', 'Could not save'), description: toApiError(result.error, result.response).message, tone: 'danger' })
		return
	}
	mediaDirty.markSaved(result.data)
	toast({ title: t('pages.settings.mediaManagement.saved', 'Saved'), tone: 'ok' })
}

function namingSampleLabel(name: string): string {
	const key = NAMING_SAMPLE_LABELS[name]
	return key ? t(key, NAMING_SAMPLE_FALLBACKS[name] ?? name) : name
}

const rootFolderColumns = [
	{ key: 'path', label: t('pages.settings.mediaManagement.path', 'Path') },
	{ key: 'mediaKind', label: t('pages.settings.mediaManagement.kind', 'Kind') },
	{ key: 'free', label: t('pages.settings.mediaManagement.freeSpace', 'Free space'), align: 'right' as const },
	{ key: 'actions', label: '', align: 'right' as const },
]

onMounted(async () => {
	loading.value = true
	const [mediaResult, namingResult] = await Promise.all([
		api.GET('/api/v1/config/media-management'),
		api.GET('/api/v1/config/naming'),
		reference.load(),
		refreshExamples(),
	])
	if (mediaResult.data) {
		mediaDirty.markSaved(mediaResult.data)
	}
	if (namingResult.data) {
		namingDraft.value = namingResult.data
	}
	loading.value = false
})
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.settings.mediaManagement.title', 'Media management')" />

		<SSpinner v-if="loading" />

		<template v-else-if="mediaDraft">
			<SSection :title="t('pages.settings.mediaManagement.fileManagement', 'File management')">
				<div class="field-grid">
					<SSwitch
						v-model="mediaDraft.useHardlinks"
						:label="t('pages.settings.mediaManagement.useHardlinks', 'Use hardlinks instead of copying')"
					/>
					<SSwitch
						v-model="mediaDraft.enableMediaInfo"
						:label="t('pages.settings.mediaManagement.extractMediaInfo', 'Extract media info on import')"
					/>
					<SSwitch
						v-model="mediaDraft.importExtraFiles"
						:label="t('pages.settings.mediaManagement.importExtraFiles', 'Import extra files (subtitles, NFO)')"
					/>
				</div>
				<SField
					:label="t('pages.settings.mediaManagement.fileDate', 'File date')"
					:hint="t('pages.settings.mediaManagement.fileDateHint', 'Sets the file\'s modified timestamp on import and rescan.')"
					control-id="file-date"
				>
					<SSelect
						v-model="mediaDraft.fileDate"
						control-id="file-date"
						:options="fileDateOptions"
					/>
				</SField>
				<p class="nfo-hint">
					{{ t('pages.settings.mediaManagement.nfoHintPrefix', 'NFO and image files are written by') }}
					<NuxtLink to="/settings/metadata-consumers">
						{{ t('pages.settings.mediaManagement.metadataConsumersLink', 'metadata consumers') }}
					</NuxtLink>.
				</p>
				<SField
					v-if="mediaDraft.importExtraFiles"
					:label="t('pages.settings.mediaManagement.extraFileExtensions', 'Extra file extensions')"
					:hint="t('pages.settings.mediaManagement.extraFileExtensionsHint', 'Comma separated, for example srt,nfo,jpg')"
					control-id="extra-extensions"
				>
					<SInput
						id="extra-extensions"
						v-model="mediaDraft.extraFileExtensions"
					/>
				</SField>
			</SSection>

			<SSection :title="t('pages.settings.mediaManagement.freeSpaceSection', 'Free space')">
				<div class="field-grid">
					<SField
						:label="t('pages.settings.mediaManagement.minimumFreeSpace', 'Minimum free space (MB)')"
						control-id="min-free-space"
					>
						<SInput
							id="min-free-space"
							type="number"
							:model-value="String(mediaDraft.minimumFreeSpaceMb)"
							@update:model-value="mediaDraft!.minimumFreeSpaceMb = Number($event) || 0"
						/>
					</SField>
					<SSwitch
						v-model="mediaDraft.skipFreeSpaceCheck"
						:label="t('pages.settings.mediaManagement.skipFreeSpaceCheck', 'Skip the free space check')"
					/>
				</div>
			</SSection>

			<SSection :title="t('pages.settings.mediaManagement.folders', 'Folders')">
				<div class="field-grid">
					<SSwitch
						v-model="mediaDraft.createEmptySeriesFolders"
						:label="t('pages.settings.mediaManagement.createEmptySeriesFolders', 'Create empty series folders on add')"
					/>
					<SSwitch
						v-model="mediaDraft.createEmptyMovieFolders"
						:label="t('pages.settings.mediaManagement.createEmptyMovieFolders', 'Create empty movie folders on add')"
					/>
					<SSwitch
						v-model="mediaDraft.deleteEmptyFolders"
						:label="t('pages.settings.mediaManagement.deleteEmptyFolders', 'Delete empty folders after moves')"
					/>
					<SSwitch
						v-model="mediaDraft.unmonitorDeletedFiles"
						:label="t('pages.settings.mediaManagement.unmonitorDeletedFiles', 'Unmonitor deleted files found during scans')"
					/>
				</div>
			</SSection>

			<SSection :title="t('pages.settings.mediaManagement.recycleBin', 'Recycle bin')">
				<div class="field-grid">
					<SField
						:label="t('pages.settings.mediaManagement.recycleBinPath', 'Recycle bin path')"
						:hint="t('pages.settings.mediaManagement.recycleBinPathHint', 'Empty deletes files immediately instead of moving them here')"
						control-id="recycle-path"
					>
						<PathPicker
							id="recycle-path"
							v-model:path="mediaDraft.recycleBinPath"
						/>
					</SField>
					<SField
						:label="t('pages.settings.mediaManagement.cleanupAfterDays', 'Cleanup after (days)')"
						control-id="recycle-days"
					>
						<SInput
							id="recycle-days"
							type="number"
							:model-value="String(mediaDraft.recycleBinCleanupDays)"
							@update:model-value="mediaDraft!.recycleBinCleanupDays = Number($event) || 0"
						/>
					</SField>
				</div>
			</SSection>

			<SSection :title="t('pages.settings.mediaManagement.permissions', 'Permissions')">
				<div class="field-grid">
					<SField
						:label="t('pages.settings.mediaManagement.folderMode', 'Folder mode')"
						:hint="t('pages.settings.mediaManagement.folderModeHint', 'Octal, for example 755. Empty to skip.')"
						control-id="chmod-folder"
					>
						<SInput
							id="chmod-folder"
							v-model="mediaDraft.chmodFolder"
							placeholder="755"
						/>
					</SField>
					<SField
						:label="t('pages.settings.mediaManagement.fileMode', 'File mode')"
						:hint="t('pages.settings.mediaManagement.fileModeHint', 'Octal, for example 644. Empty to skip.')"
						control-id="chmod-file"
					>
						<SInput
							id="chmod-file"
							v-model="mediaDraft.chmodFile"
							placeholder="644"
						/>
					</SField>
					<SField
						:label="t('pages.settings.mediaManagement.groupOwner', 'Group owner')"
						:hint="t('pages.settings.mediaManagement.groupOwnerHint', 'Empty to skip.')"
						control-id="chown-group"
					>
						<SInput
							id="chown-group"
							v-model="mediaDraft.chownGroup"
						/>
					</SField>
				</div>
			</SSection>

			<SSection :title="t('pages.settings.mediaManagement.propersAndRepacks', 'Propers and repacks')">
				<SField
					:label="t('pages.settings.mediaManagement.onGrab', 'On grab')"
					control-id="propers"
				>
					<SSelect
						v-model="mediaDraft.downloadPropersAndRepacks"
						control-id="propers"
						:options="downloadPropersAndRepacksOptions"
					/>
				</SField>
			</SSection>

			<SettingsSaveBar
				:dirty="mediaDirty.isDirty.value"
				:saving="mediaSaving"
				@save="saveMediaConfig"
				@discard="mediaDirty.revert()"
			/>
		</template>

		<SSection :title="t('pages.settings.mediaManagement.rootFolders', 'Root folders')">
			<STable
				:columns="rootFolderColumns"
				:rows="reference.rootFolders"
				:row-key="(row) => row.id"
			>
				<template #cell-mediaKind="{ row }">
					{{ row.mediaKind === 'SERIES' ? t('pages.settings.mediaManagement.series', 'Series') : t('pages.settings.mediaManagement.movies', 'Movies') }}
				</template>
				<template #cell-free="{ row }">
					{{ row.accessible ? formatBytesGb(row.freeSpace) : t('pages.settings.mediaManagement.notAccessible', 'Not accessible') }}
				</template>
				<template #cell-actions="{ row }">
					<SIconButton
						:label="t('pages.settings.mediaManagement.removeRootFolder', 'Remove root folder')"
						@click="deleteFolderTarget = row"
					>
						<Icon
							name="lucide:trash-2"
							aria-hidden="true"
						/>
					</SIconButton>
				</template>
				<template #empty>
					<SEmptyState :message="t('pages.settings.mediaManagement.emptyRootFolders', 'No root folders yet. Add one to tell Submarine where your library lives.')">
						<template #action>
							<SButton
								variant="primary"
								@click="openAddRootFolder"
							>
								{{ t('pages.settings.mediaManagement.addRootFolder', 'Add root folder') }}
							</SButton>
						</template>
					</SEmptyState>
				</template>
			</STable>
			<div
				v-if="reference.rootFolders.length > 0"
				class="section-actions"
			>
				<SButton
					variant="secondary"
					@click="openAddRootFolder"
				>
					{{ t('pages.settings.mediaManagement.addRootFolder', 'Add root folder') }}
				</SButton>
			</div>
		</SSection>

		<SSection
			v-if="namingDraft"
			:title="t('pages.settings.mediaManagement.naming', 'Naming')"
		>
			<p class="naming-status">
				{{ t('pages.settings.mediaManagement.autosaveHint', 'Changes save automatically.') }}
				<span v-if="namingStatus === 'saving'">{{ t('pages.settings.mediaManagement.saving', 'Saving…') }}</span>
				<span v-else-if="namingStatus === 'error'">{{ t('pages.settings.mediaManagement.lastChangeError', 'Could not save the last change.') }}</span>
			</p>

			<div class="field-grid">
				<SSwitch
					v-model="namingDraft.renameEpisodes"
					:label="t('pages.settings.mediaManagement.renameEpisodes', 'Rename episode files')"
				/>
				<SSwitch
					v-model="namingDraft.renameMovies"
					:label="t('pages.settings.mediaManagement.renameMovies', 'Rename movie files')"
				/>
				<SSwitch
					v-model="namingDraft.replaceIllegalCharacters"
					:label="t('pages.settings.mediaManagement.replaceIllegalCharacters', 'Replace illegal characters')"
				/>
			</div>

			<div class="field-grid">
				<SField
					:label="t('pages.settings.mediaManagement.colonReplacement', 'Colon replacement')"
					control-id="colon-replacement"
				>
					<SSelect
						v-model="namingDraft.colonReplacement"
						control-id="colon-replacement"
						:options="colonReplacementOptions"
					/>
				</SField>
				<SField
					:label="t('pages.settings.mediaManagement.multiEpisodeStyle', 'Multi-episode style')"
					control-id="multi-episode-style"
				>
					<SSelect
						v-model="namingDraft.multiEpisodeStyle"
						control-id="multi-episode-style"
						:options="multiEpisodeStyleOptions"
					/>
				</SField>
			</div>

			<SSection :title="t('pages.settings.mediaManagement.episodes', 'Episodes')">
				<div class="field-grid">
					<SField
						:label="t('pages.settings.mediaManagement.standardEpisodeFormat', 'Standard episode format')"
						control-id="standard-format"
					>
						<SInput
							id="standard-format"
							v-model="namingDraft.standardEpisodeFormat"
						/>
					</SField>
					<SField
						:label="t('pages.settings.mediaManagement.dailyEpisodeFormat', 'Daily episode format')"
						control-id="daily-format"
					>
						<SInput
							id="daily-format"
							v-model="namingDraft.dailyEpisodeFormat"
						/>
					</SField>
					<SField
						:label="t('pages.settings.mediaManagement.animeEpisodeFormat', 'Anime episode format')"
						control-id="anime-format"
					>
						<SInput
							id="anime-format"
							v-model="namingDraft.animeEpisodeFormat"
						/>
					</SField>
				</div>
			</SSection>

			<SSection :title="t('pages.settings.mediaManagement.seriesFolders', 'Series folders')">
				<div class="field-grid">
					<SField
						:label="t('pages.settings.mediaManagement.seriesFolderFormat', 'Series folder format')"
						control-id="series-folder-format"
					>
						<SInput
							id="series-folder-format"
							v-model="namingDraft.seriesFolderFormat"
						/>
					</SField>
					<SField
						:label="t('pages.settings.mediaManagement.seasonFolderFormat', 'Season folder format')"
						control-id="season-folder-format"
					>
						<SInput
							id="season-folder-format"
							v-model="namingDraft.seasonFolderFormat"
						/>
					</SField>
					<SField
						:label="t('pages.settings.mediaManagement.specialsFolderFormat', 'Specials folder format')"
						control-id="specials-folder-format"
					>
						<SInput
							id="specials-folder-format"
							v-model="namingDraft.specialsFolderFormat"
						/>
					</SField>
				</div>
			</SSection>

			<SSection :title="t('pages.settings.mediaManagement.movies', 'Movies')">
				<div class="field-grid">
					<SField
						:label="t('pages.settings.mediaManagement.movieFileFormat', 'Movie file format')"
						control-id="movie-format"
					>
						<SInput
							id="movie-format"
							v-model="namingDraft.movieFormat"
						/>
					</SField>
					<SField
						:label="t('pages.settings.mediaManagement.movieFolderFormat', 'Movie folder format')"
						control-id="movie-folder-format"
					>
						<SInput
							id="movie-folder-format"
							v-model="namingDraft.movieFolderFormat"
						/>
					</SField>
				</div>
			</SSection>

			<SSection :title="t('pages.settings.mediaManagement.examples', 'Examples')">
				<ul class="examples-list">
					<li
						v-for="sample in examples"
						:key="sample.name"
						class="examples-row"
					>
						<span class="examples-label">{{ namingSampleLabel(sample.name) }}</span>
						<span class="examples-preview">{{ sample.preview }}</span>
					</li>
				</ul>
			</SSection>
		</SSection>

		<SDialog
			v-model="rootFolderDialogOpen"
			:title="t('pages.settings.mediaManagement.addRootFolderDialog', 'Add root folder')"
		>
			<SField
				:label="t('pages.settings.mediaManagement.pathLabel', 'Path')"
				:error="rootFolderError"
				control-id="new-root-path"
			>
				<PathPicker
					id="new-root-path"
					v-model:path="rootFolderPath"
				/>
			</SField>
			<SField
				:label="t('pages.settings.mediaManagement.contains', 'Contains')"
				control-id="new-root-kind"
			>
				<SSelect
					v-model="rootFolderKind"
					control-id="new-root-kind"
					:options="mediaKindOptions"
				/>
			</SField>
			<template #footer>
				<SButton
					variant="secondary"
					@click="rootFolderDialogOpen = false"
				>
					{{ t('pages.settings.mediaManagement.cancel', 'Cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="rootFolderSaving"
					@click="saveRootFolder"
				>
					{{ t('pages.settings.mediaManagement.addRootFolder', 'Add root folder') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteFolderTargetOpen"
			:title="t('pages.settings.mediaManagement.removeRootFolderDialog', 'Remove root folder')"
		>
			<p v-if="deleteFolderTarget">
				{{ t('pages.settings.mediaManagement.confirmRemoveRootFolder', { path: deleteFolderTarget.path }, 'Remove "{path}"? Files already imported from it are not deleted.') }}
			</p>
			<p
				v-if="deleteFolderError"
				class="s-field-error"
				role="alert"
			>
				{{ deleteFolderError }}
			</p>
			<template #footer>
				<SButton
					variant="secondary"
					:disabled="deletingFolder"
					@click="deleteFolderTarget = null"
				>
					{{ t('pages.settings.mediaManagement.cancel', 'Cancel') }}
				</SButton>
				<SButton
					variant="danger"
					:loading="deletingFolder"
					@click="deleteRootFolder"
				>
					{{ t('pages.settings.mediaManagement.remove', 'Remove') }}
				</SButton>
			</template>
		</SDialog>
	</div>
</template>

<style scoped>
.field-grid {
	display: grid;
	grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
	gap: 16px;
	margin-bottom: 16px;
}

.field-grid:last-child {
	margin-bottom: 0;
}

.nfo-hint {
	font-size: 0.8125rem;
	color: var(--fg-muted);
	margin: 8px 0 0;
}

.nfo-hint a {
	color: var(--accent);
}

.section-actions {
	margin-top: 12px;
}

.naming-status {
	font-size: 0.8125rem;
	color: var(--fg-muted);
	margin-bottom: 16px;
	display: flex;
	gap: 8px;
}

.examples-list {
	display: flex;
	flex-direction: column;
	gap: 8px;
}

.examples-row {
	display: flex;
	justify-content: space-between;
	gap: 16px;
	padding: 6px 0;
	border-bottom: 1px solid var(--line);
}

.examples-row:last-child {
	border-bottom: none;
}

.examples-label {
	color: var(--fg-muted);
	flex: none;
}

.examples-preview {
	text-align: right;
	overflow-wrap: anywhere;
}
</style>
