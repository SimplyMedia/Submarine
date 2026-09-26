<script setup lang="ts">
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

type MediaManagementConfigResource = components['schemas']['MediaManagementConfigResource']
type NamingConfigResource = components['schemas']['NamingConfigResource']
type RootFolderDto = components['schemas']['RootFolderDto']
type NamingSample = components['schemas']['NamingSample']

definePageMeta({ layout: 'default' })
useHead({ title: 'Media management' })

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
		return 'Unknown'
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
		toast({ title: 'Root folder added', tone: 'ok' })
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
	toast({ title: 'Root folder removed', tone: 'ok' })
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
		toast({ title: 'Could not save', description: toApiError(result.error, result.response).message, tone: 'danger' })
		return
	}
	mediaDirty.markSaved(result.data)
	toast({ title: 'Saved', tone: 'ok' })
}

const rootFolderColumns = [
	{ key: 'path', label: 'Path' },
	{ key: 'mediaKind', label: 'Kind' },
	{ key: 'free', label: 'Free space', align: 'right' as const },
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
		<SPageHeader title="Media management" />

		<SSpinner v-if="loading" />

		<template v-else-if="mediaDraft">
			<SSection title="File management">
				<div class="field-grid">
					<SSwitch
						v-model="mediaDraft.useHardlinks"
						label="Use hardlinks instead of copying"
					/>
					<SSwitch
						v-model="mediaDraft.enableMediaInfo"
						label="Extract media info on import"
					/>
					<SSwitch
						v-model="mediaDraft.importExtraFiles"
						label="Import extra files (subtitles, NFO)"
					/>
				</div>
				<SField
					label="File date"
					hint="Sets the file's modified timestamp on import and rescan."
					control-id="file-date"
				>
					<SSelect
						v-model="mediaDraft.fileDate"
						control-id="file-date"
						:options="fileDateOptions"
					/>
				</SField>
				<p class="nfo-hint">
					NFO and image files are written by
					<NuxtLink to="/settings/metadata-consumers">
						metadata consumers
					</NuxtLink>.
				</p>
				<SField
					v-if="mediaDraft.importExtraFiles"
					label="Extra file extensions"
					hint="Comma separated, for example srt,nfo,jpg"
					control-id="extra-extensions"
				>
					<SInput
						id="extra-extensions"
						v-model="mediaDraft.extraFileExtensions"
					/>
				</SField>
			</SSection>

			<SSection title="Free space">
				<div class="field-grid">
					<SField
						label="Minimum free space (MB)"
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
						label="Skip the free space check"
					/>
				</div>
			</SSection>

			<SSection title="Folders">
				<div class="field-grid">
					<SSwitch
						v-model="mediaDraft.createEmptySeriesFolders"
						label="Create empty series folders on add"
					/>
					<SSwitch
						v-model="mediaDraft.createEmptyMovieFolders"
						label="Create empty movie folders on add"
					/>
					<SSwitch
						v-model="mediaDraft.deleteEmptyFolders"
						label="Delete empty folders after moves"
					/>
					<SSwitch
						v-model="mediaDraft.unmonitorDeletedFiles"
						label="Unmonitor deleted files found during scans"
					/>
				</div>
			</SSection>

			<SSection title="Recycle bin">
				<div class="field-grid">
					<SField
						label="Recycle bin path"
						hint="Empty deletes files immediately instead of moving them here"
						control-id="recycle-path"
					>
						<PathPicker
							id="recycle-path"
							v-model:path="mediaDraft.recycleBinPath"
						/>
					</SField>
					<SField
						label="Cleanup after (days)"
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

			<SSection title="Permissions">
				<div class="field-grid">
					<SField
						label="Folder mode"
						hint="Octal, for example 755. Empty to skip."
						control-id="chmod-folder"
					>
						<SInput
							id="chmod-folder"
							v-model="mediaDraft.chmodFolder"
							placeholder="755"
						/>
					</SField>
					<SField
						label="File mode"
						hint="Octal, for example 644. Empty to skip."
						control-id="chmod-file"
					>
						<SInput
							id="chmod-file"
							v-model="mediaDraft.chmodFile"
							placeholder="644"
						/>
					</SField>
					<SField
						label="Group owner"
						hint="Empty to skip."
						control-id="chown-group"
					>
						<SInput
							id="chown-group"
							v-model="mediaDraft.chownGroup"
						/>
					</SField>
				</div>
			</SSection>

			<SSection title="Propers and repacks">
				<SField
					label="On grab"
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

		<SSection title="Root folders">
			<STable
				:columns="rootFolderColumns"
				:rows="reference.rootFolders"
				:row-key="(row) => row.id"
			>
				<template #cell-mediaKind="{ row }">
					{{ row.mediaKind === 'SERIES' ? 'Series' : 'Movies' }}
				</template>
				<template #cell-free="{ row }">
					{{ row.accessible ? formatBytesGb(row.freeSpace) : 'Not accessible' }}
				</template>
				<template #cell-actions="{ row }">
					<SIconButton
						label="Remove root folder"
						@click="deleteFolderTarget = row"
					>
						<Icon
							name="lucide:trash-2"
							aria-hidden="true"
						/>
					</SIconButton>
				</template>
				<template #empty>
					<SEmptyState message="No root folders yet. Add one to tell Submarine where your library lives.">
						<template #action>
							<SButton
								variant="primary"
								@click="openAddRootFolder"
							>
								Add root folder
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
					Add root folder
				</SButton>
			</div>
		</SSection>

		<SSection
			v-if="namingDraft"
			title="Naming"
		>
			<p class="naming-status">
				Changes save automatically.
				<span v-if="namingStatus === 'saving'">Saving…</span>
				<span v-else-if="namingStatus === 'error'">Could not save the last change.</span>
			</p>

			<div class="field-grid">
				<SSwitch
					v-model="namingDraft.renameEpisodes"
					label="Rename episode files"
				/>
				<SSwitch
					v-model="namingDraft.renameMovies"
					label="Rename movie files"
				/>
				<SSwitch
					v-model="namingDraft.replaceIllegalCharacters"
					label="Replace illegal characters"
				/>
			</div>

			<div class="field-grid">
				<SField
					label="Colon replacement"
					control-id="colon-replacement"
				>
					<SSelect
						v-model="namingDraft.colonReplacement"
						control-id="colon-replacement"
						:options="colonReplacementOptions"
					/>
				</SField>
				<SField
					label="Multi-episode style"
					control-id="multi-episode-style"
				>
					<SSelect
						v-model="namingDraft.multiEpisodeStyle"
						control-id="multi-episode-style"
						:options="multiEpisodeStyleOptions"
					/>
				</SField>
			</div>

			<SSection title="Episodes">
				<div class="field-grid">
					<SField
						label="Standard episode format"
						control-id="standard-format"
					>
						<SInput
							id="standard-format"
							v-model="namingDraft.standardEpisodeFormat"
						/>
					</SField>
					<SField
						label="Daily episode format"
						control-id="daily-format"
					>
						<SInput
							id="daily-format"
							v-model="namingDraft.dailyEpisodeFormat"
						/>
					</SField>
					<SField
						label="Anime episode format"
						control-id="anime-format"
					>
						<SInput
							id="anime-format"
							v-model="namingDraft.animeEpisodeFormat"
						/>
					</SField>
				</div>
			</SSection>

			<SSection title="Series folders">
				<div class="field-grid">
					<SField
						label="Series folder format"
						control-id="series-folder-format"
					>
						<SInput
							id="series-folder-format"
							v-model="namingDraft.seriesFolderFormat"
						/>
					</SField>
					<SField
						label="Season folder format"
						control-id="season-folder-format"
					>
						<SInput
							id="season-folder-format"
							v-model="namingDraft.seasonFolderFormat"
						/>
					</SField>
					<SField
						label="Specials folder format"
						control-id="specials-folder-format"
					>
						<SInput
							id="specials-folder-format"
							v-model="namingDraft.specialsFolderFormat"
						/>
					</SField>
				</div>
			</SSection>

			<SSection title="Movies">
				<div class="field-grid">
					<SField
						label="Movie file format"
						control-id="movie-format"
					>
						<SInput
							id="movie-format"
							v-model="namingDraft.movieFormat"
						/>
					</SField>
					<SField
						label="Movie folder format"
						control-id="movie-folder-format"
					>
						<SInput
							id="movie-folder-format"
							v-model="namingDraft.movieFolderFormat"
						/>
					</SField>
				</div>
			</SSection>

			<SSection title="Examples">
				<ul class="examples-list">
					<li
						v-for="sample in examples"
						:key="sample.name"
						class="examples-row"
					>
						<span class="examples-label">{{ NAMING_SAMPLE_LABELS[sample.name] ?? sample.name }}</span>
						<span class="examples-preview">{{ sample.preview }}</span>
					</li>
				</ul>
			</SSection>
		</SSection>

		<SDialog
			v-model="rootFolderDialogOpen"
			title="Add root folder"
		>
			<SField
				label="Path"
				:error="rootFolderError"
				control-id="new-root-path"
			>
				<PathPicker
					id="new-root-path"
					v-model:path="rootFolderPath"
				/>
			</SField>
			<SField
				label="Contains"
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
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="rootFolderSaving"
					@click="saveRootFolder"
				>
					Add root folder
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteFolderTargetOpen"
			title="Remove root folder"
		>
			<p v-if="deleteFolderTarget">
				Remove "{{ deleteFolderTarget.path }}"? Files already imported from it are not deleted.
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
					Cancel
				</SButton>
				<SButton
					variant="danger"
					:loading="deletingFolder"
					@click="deleteRootFolder"
				>
					Remove
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
