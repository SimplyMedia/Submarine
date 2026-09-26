<script setup lang="ts">
/**
 * Library import: adopt folders that already exist under a root folder but
 * aren't mapped to a series or movie yet. Scans for unmapped folders,
 * proposes a metadata match per folder (re-searchable), then adds the
 * selected folders with the folder itself as the new version's path.
 */
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

useHead({ title: 'Import existing library' })

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
	{ key: 'folder', label: 'Folder' },
	{ key: 'match', label: 'Match' },
	{ key: 'quality', label: 'Quality profile' },
	{ key: 'language', label: 'Language profile' },
	...(mediaKind.value === 'SERIES' ? [{ key: 'seriesType', label: 'Series type' }] : []),
	{ key: 'monitor', label: 'Monitor', align: 'right' as const },
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
		toast({ title: 'Choose a root folder', tone: 'danger' })
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
		toast({ title: 'Select at least one folder to import', tone: 'danger' })
		return
	}
	const missing = selected.value.filter(folder => !matches[folder])
	if (missing.length > 0) {
		toast({ title: `${missing.length} selected folder(s) have no match. Search for one or deselect them.`, tone: 'danger' })
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
			toast({ title: 'Could not import selected folders', description: toApiError(result.error, result.response).message, tone: 'danger' })
			return
		}
		toast({ title: `${result.data.added} folder(s) added`, tone: 'ok' })
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
		<SPageHeader title="Import existing library">
			<template #actions>
				<SButton
					variant="primary"
					:loading="importing"
					:disabled="selected.length === 0"
					@click="runImport"
				>
					Import selected ({{ selected.length }})
				</SButton>
			</template>
		</SPageHeader>

		<SSection>
			<div class="library-import-scan-bar">
				<SField
					label="Root folder"
					control-id="library-import-root"
					class="library-import-root-field"
				>
					<SSelect
						v-model="rootFolderId"
						control-id="library-import-root"
						:options="rootFolderOptions"
						placeholder="Choose a root folder"
					/>
				</SField>
				<SButton
					:loading="scanning"
					@click="scan"
				>
					Scan
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
			message="No unmapped folders found under that root folder."
			icon="lucide:folder-search"
		/>
		<template v-else-if="folders.length > 0">
			<SCheckbox
				:model-value="allSelected"
				label="Select all"
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
						:aria-label="`Select ${row.folder}`"
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
						placeholder="Choose a profile"
						@update:model-value="value => { rows[row.folder]!.qualityProfileId = Number(value) }"
					/>
				</template>
				<template #cell-language="{ row }">
					<SSelect
						v-if="rows[row.folder]"
						:model-value="rows[row.folder]!.languageProfileId != null ? String(rows[row.folder]!.languageProfileId) : undefined"
						:options="languageOptions"
						placeholder="Choose a profile"
						@update:model-value="value => { rows[row.folder]!.languageProfileId = Number(value) }"
					/>
				</template>
				<template #cell-seriesType="{ row }">
					<SSelect
						v-if="rows[row.folder]"
						v-model="rows[row.folder]!.seriesType"
						:options="seriesTypeOptions"
					/>
				</template>
				<template #cell-monitor="{ row }">
					<SCheckbox
						v-if="rows[row.folder]"
						v-model="rows[row.folder]!.monitor"
						:aria-label="`Monitor ${row.folder}`"
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
