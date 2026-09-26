<script setup lang="ts">
/**
 * Library import: adopt folders that already exist under a root folder but
 * aren't mapped to a series or movie yet. Scans for unmapped folders,
 * proposes a metadata match per folder (re-searchable), then adds the
 * selected folders with the folder itself as the new version's path.
 */
import { useI18n } from 'vue-i18n'
import { toApiError, useApi } from '~/composables/useApi'
import { seriesTypeOptions } from '~/utils/library-labels'
import { useReferenceStore } from '~/stores/reference'
import type { components } from '~/types/api'

type LibraryImportFolderDto = components['schemas']['LibraryImportFolderDto']
type SearchResultResource = components['schemas']['SearchResultResource']
type SeriesType = components['schemas']['SeriesType']

interface FolderRow {
	qualityProfileId: number | null
	languageProfileId: number | null
	seriesType: SeriesType
	monitor: boolean
}

const { t } = useI18n()
useHead({ title: t('pages.libraryImport.title') })

const api = useApi()
const reference = useReferenceStore()
const { toast } = useToast()

const rootFolderId = ref('')
const folders = ref<LibraryImportFolderDto[]>([])
const rows = reactive<Record<string, FolderRow>>({})
const matches = reactive<Record<string, SearchResultResource | null>>({})
const selected = ref<string[]>([])
const scanning = ref(false)
const scanError = ref('')
const importing = ref(false)
const scanned = ref(false)

onMounted(async () => {
	await reference.load()
	const first = reference.rootFolders[0]
	if (first) {
		rootFolderId.value = String(first.id)
	}
})

const rootFolderOptions = computed(() => reference.rootFolders.map(folder => ({ value: String(folder.id), label: folder.path })))
const selectedRootFolder = computed(() => reference.rootFolders.find(folder => String(folder.id) === rootFolderId.value) ?? null)
const mediaKind = computed(() => selectedRootFolder.value?.mediaKind ?? 'SERIES')

const qualityOptions = computed(() => reference.qualityProfiles.map(profile => ({ value: String(profile.id), label: profile.name })))
const languageOptions = computed(() => reference.languageProfiles.map(profile => ({ value: String(profile.id), label: profile.name })))

const columns = computed(() => [
	{ key: 'select', label: '' },
	{ key: 'folder', label: t('pages.libraryImport.folder') },
	{ key: 'match', label: t('pages.libraryImport.match') },
	{ key: 'quality', label: t('pages.libraryImport.qualityProfile') },
	{ key: 'language', label: t('pages.libraryImport.languageProfile') },
	...(mediaKind.value === 'SERIES' ? [{ key: 'seriesType', label: t('pages.libraryImport.seriesType') }] : []),
	{ key: 'monitor', label: t('pages.libraryImport.monitor'), align: 'right' as const },
])

function initRow(): FolderRow {
	return {
		qualityProfileId: reference.qualityProfiles[0]?.id ?? null,
		languageProfileId: reference.languageProfiles[0]?.id ?? null,
		seriesType: 'STANDARD',
		monitor: true,
	}
}

async function scan() {
	if (!rootFolderId.value) {
		toast({ title: t('pages.libraryImport.chooseRootFolder'), tone: 'danger' })
		return
	}
	scanning.value = true
	scanError.value = ''
	try {
		const result = await api.POST('/api/v1/library-import/scan', { body: { rootFolderId: Number(rootFolderId.value) } })
		if (!result.data) {
			scanError.value = toApiError(result.error, result.response).message
			return
		}
		folders.value = result.data
		scanned.value = true
		for (const key of Object.keys(rows)) {
			Reflect.deleteProperty(rows, key)
		}
		for (const key of Object.keys(matches)) {
			Reflect.deleteProperty(matches, key)
		}
		for (const folder of result.data) {
			rows[folder.folder] = initRow()
			matches[folder.folder] = folder.proposals[0] ?? null
		}
		selected.value = result.data.map(folder => folder.folder)
	}
	finally {
		scanning.value = false
	}
}

const allSelected = computed(() => folders.value.length > 0 && folders.value.every(folder => selected.value.includes(folder.folder)))

function toggleAll(value: boolean) {
	selected.value = value ? folders.value.map(folder => folder.folder) : []
}

function toggleRow(folder: string, value: boolean) {
	selected.value = value ? [...selected.value, folder] : selected.value.filter(existing => existing !== folder)
}

async function runImport() {
	if (selected.value.length === 0) {
		toast({ title: t('pages.libraryImport.selectFolderToImport'), tone: 'danger' })
		return
	}
	const missing = selected.value.filter(folder => !matches[folder])
	if (missing.length > 0) {
		toast({ title: t('pages.libraryImport.selectedFoldersUnmatched', { count: missing.length }), tone: 'danger' })
		return
	}

	importing.value = true
	try {
		const items = selected.value.map((folder) => {
			const match = matches[folder]!
			const row = rows[folder]!
			return {
				rootFolderId: Number(rootFolderId.value),
				folder,
				tvdbId: match.tvdbId,
				tmdbId: match.tmdbId,
				qualityProfileId: row.qualityProfileId ?? reference.qualityProfiles[0]?.id ?? 1,
				languageProfileId: row.languageProfileId ?? reference.languageProfiles[0]?.id ?? 1,
				seriesType: mediaKind.value === 'SERIES' ? row.seriesType : null,
				monitor: row.monitor,
			}
		})
		const result = await api.POST('/api/v1/library-import', { body: { items } })
		if (!result.data) {
			toast({ title: t('pages.libraryImport.importFailed'), description: toApiError(result.error, result.response).message, tone: 'danger' })
			return
		}
		toast({ title: t('pages.libraryImport.foldersAdded', { count: result.data.added }), tone: 'ok' })
		const importedFolders = new Set(selected.value)
		folders.value = folders.value.filter(folder => !importedFolders.has(folder.folder))
		for (const folder of importedFolders) {
			Reflect.deleteProperty(rows, folder)
			Reflect.deleteProperty(matches, folder)
		}
		selected.value = []
	}
	finally {
		importing.value = false
	}
}
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.libraryImport.title')">
			<template #actions>
				<SButton
					variant="primary"
					:loading="importing"
					:disabled="selected.length === 0"
					@click="runImport"
				>
					{{ t('pages.libraryImport.importSelected', { count: selected.length }) }}
				</SButton>
			</template>
		</SPageHeader>

		<SSection>
			<div class="library-import-scan-bar">
				<SField
					:label="t('pages.libraryImport.rootFolder')"
					control-id="library-import-root"
					class="library-import-root-field"
				>
					<SSelect
						v-model="rootFolderId"
						control-id="library-import-root"
						:options="rootFolderOptions"
						:placeholder="t('pages.libraryImport.chooseRootFolder')"
					/>
				</SField>
				<SButton
					:loading="scanning"
					@click="scan"
				>
					{{ t('pages.libraryImport.scan') }}
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
			v-else-if="scanned && folders.length === 0"
			:message="t('pages.libraryImport.noUnmappedFolders')"
			icon="lucide:folder-search"
		/>
		<template v-else-if="folders.length > 0">
			<SCheckbox
				:model-value="allSelected"
				:label="t('pages.libraryImport.selectAll')"
				class="library-import-select-all"
				@update:model-value="toggleAll"
			/>
			<STable
				:columns="columns"
				:rows="folders"
				:row-key="(row) => row.folder"
			>
				<template #cell-select="{ row }">
					<SCheckbox
						:model-value="selected.includes(row.folder)"
						:aria-label="t('pages.libraryImport.selectFolder', { folder: row.folder })"
						@update:model-value="value => toggleRow(row.folder, value)"
					/>
				</template>
				<template #cell-folder="{ row }">
					<div class="library-import-folder-cell">
						<span class="library-import-folder-name">{{ row.folder }}</span>
						<span
							v-if="row.guessedYear"
							class="library-import-folder-guess"
						>{{ row.guessedTitle }} ({{ row.guessedYear }})</span>
						<span
							v-else
							class="library-import-folder-guess"
						>{{ row.guessedTitle }}</span>
					</div>
				</template>
				<template #cell-match="{ row }">
					<MetadataMatchPicker
						v-model="matches[row.folder]"
						:kind="mediaKind === 'SERIES' ? 'series' : 'movie'"
					/>
				</template>
				<template #cell-quality="{ row }">
					<SSelect
						v-if="rows[row.folder]"
						:model-value="rows[row.folder]!.qualityProfileId != null ? String(rows[row.folder]!.qualityProfileId) : undefined"
						:options="qualityOptions"
						:placeholder="t('pages.libraryImport.chooseProfile')"
						@update:model-value="value => { rows[row.folder]!.qualityProfileId = Number(value) }"
					/>
				</template>
				<template #cell-language="{ row }">
					<SSelect
						v-if="rows[row.folder]"
						:model-value="rows[row.folder]!.languageProfileId != null ? String(rows[row.folder]!.languageProfileId) : undefined"
						:options="languageOptions"
						:placeholder="t('pages.libraryImport.chooseProfile')"
						@update:model-value="value => { rows[row.folder]!.languageProfileId = Number(value) }"
					/>
				</template>
				<template #cell-seriesType="{ row }">
					<SSelect
						v-if="rows[row.folder]"
						v-model="rows[row.folder]!.seriesType"
						:options="seriesTypeOptions.map(option => ({ ...option, label: t(option.label, option.label) }))"
					/>
				</template>
				<template #cell-monitor="{ row }">
					<SCheckbox
						v-if="rows[row.folder]"
						v-model="rows[row.folder]!.monitor"
						:aria-label="t('pages.libraryImport.monitorFolder', { folder: row.folder })"
					/>
				</template>
			</STable>
		</template>
	</div>
</template>

<style scoped>
.library-import-scan-bar {
	display: flex;
	align-items: flex-end;
	gap: 12px;
}

.library-import-root-field {
	flex: 1;
	min-width: 0;
	max-width: 400px;
}

.library-import-select-all {
	margin-bottom: 12px;
}

.library-import-folder-cell {
	display: flex;
	flex-direction: column;
	gap: 2px;
	max-width: 240px;
}

.library-import-folder-name {
	font-weight: 500;
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.library-import-folder-guess {
	font-size: 0.75rem;
	color: var(--fg-muted);
}
</style>
