<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useDirtyForm } from '~/composables/useDirtyForm'
import { useReferenceStore } from '~/stores/reference'
import { useSettingsStore } from '~/stores/settings'
import { minimumAvailabilityOptions, monitorNewItemsOptions, seriesTypeOptions } from '~/utils/library-labels'
import { cleanLibraryLevelOptions, importListTypeIcon, importListTypeLabel, importListTypeOptions, mediaKindLabel, mediaKindOptions } from '~/utils/settings-labels'
import type { SchemaField } from '~/types/schema-form'
import type { components } from '~/types/api'

type ImportListDto = components['schemas']['ImportListDto']
type ImportListType = components['schemas']['ImportListType']
type MediaKind = components['schemas']['MediaKind']
type MonitorNewItems = components['schemas']['MonitorNewItems']
type MinimumAvailability = components['schemas']['MinimumAvailability']
type SeriesType = components['schemas']['SeriesType']
type SaveImportListRequest = components['schemas']['SaveImportListRequest']
type ImportListExclusionDto = components['schemas']['ImportListExclusionDto']
type ImportListPreviewItemDto = components['schemas']['ImportListPreviewItemDto']
type ImportListConfigResource = components['schemas']['ImportListConfigResource']

definePageMeta({ layout: 'default' })
const { t } = useI18n()
useHead({ title: t('pages.settings.importLists.title') })

const api = useApi()
const reference = useReferenceStore()
const settings = useSettingsStore()
const { toast } = useToast()

const configLoadError = ref('')
const configSaving = ref(false)
const configDraft = ref<ImportListConfigResource | null>(null)
const configDirty = useDirtyForm(configDraft)

async function loadConfig() {
	configLoadError.value = ''
	const result = await api.GET('/api/v1/config/import-list')
	if (result.data) {
		configDirty.markSaved(result.data)
	}
	else {
		configLoadError.value = t('pages.settings.importLists.configLoadFailed')
	}
}

async function saveConfig() {
	if (!configDraft.value) {
		return
	}
	configSaving.value = true
	const result = await api.PUT('/api/v1/config/import-list', { body: configDraft.value })
	configSaving.value = false
	if (!result.data) {
		toast({ title: t('pages.settings.importLists.saveFailed'), description: toApiError(result.error, result.response).message, tone: 'danger' })
		return
	}
	configDirty.markSaved(result.data)
	toast({ title: t('pages.settings.importLists.saved'), tone: 'ok' })
}

const lists = ref<ImportListDto[]>([])
const loading = ref(true)
const loadError = ref('')
const exclusions = ref<ImportListExclusionDto[]>([])
const exclusionsLoading = ref(true)
const exclusionsLoadError = ref('')

async function loadLists() {
	const result = await api.GET('/api/v1/import-lists')
	if (!result.data) {
		loadError.value = t('pages.settings.importLists.loadFailed')
	}
	lists.value = result.data ?? []
}

async function loadExclusions() {
	const result = await api.GET('/api/v1/import-list-exclusions')
	if (!result.data) {
		exclusionsLoadError.value = t('pages.settings.importLists.exclusionsLoadFailed')
	}
	exclusions.value = result.data ?? []
}

async function load() {
	loading.value = true
	loadError.value = ''
	exclusionsLoading.value = true
	exclusionsLoadError.value = ''
	await Promise.all([
		reference.load(),
		settings.ensureImportListSchemas(),
		loadLists(),
		loadExclusions(),
		loadConfig(),
	])
	loading.value = false
	exclusionsLoading.value = false
}

void load()

const listColumns = [
	{ key: 'name', label: t('pages.settings.importLists.name') },
	{ key: 'type', label: t('pages.settings.importLists.type') },
	{ key: 'mediaKind', label: t('pages.settings.importLists.media') },
	{ key: 'enable', label: t('pages.settings.importLists.enabled') },
	{ key: 'actions', label: '', align: 'right' as const },
]

const qualityProfileOptions = computed(() => reference.qualityProfiles.map(p => ({ value: String(p.id), label: p.name })))
const languageProfileOptions = computed(() => reference.languageProfiles.map(p => ({ value: String(p.id), label: p.name })))
const rootFolderOptions = computed(() => reference.rootFolders.map(f => ({ value: String(f.id), label: f.path })))

// --- Add/edit dialog ---

const dialogOpen = ref(false)
const editingId = ref<number | null>(null)
const selectedType = ref<ImportListType | null>(null)
const name = ref('')
const enable = ref(true)
const enableAutomaticAdd = ref(true)
const searchOnAdd = ref(false)
const mediaKind = ref<MediaKind>('MOVIES')
const qualityProfileId = ref('')
const languageProfileId = ref('')
const rootFolderId = ref('')
const monitor = ref<MonitorNewItems>('ALL')
const minimumAvailability = ref<MinimumAvailability>('RELEASED')
const seriesType = ref<SeriesType>('STANDARD')
const seasonFolder = ref(true)
const tagIds = ref<number[]>([])
const settingsDraft = ref<Record<string, unknown>>({})
const nameError = ref('')
const fieldErrors = ref<Record<string, string[]>>({})
const formError = ref('')
const saving = ref(false)
const testing = ref(false)

const normalizedFields = computed<SchemaField[]>(() => {
	const schema = settings.importListSchemas.find(s => s.type === selectedType.value)
	if (!schema) {
		return []
	}
	return schema.fields.map(field => ({ ...field, default: field.defaultValue, type: field.type as SchemaField['type'] }))
})

function resetForm() {
	name.value = ''
	enable.value = true
	enableAutomaticAdd.value = true
	searchOnAdd.value = false
	mediaKind.value = 'MOVIES'
	qualityProfileId.value = reference.qualityProfiles[0] ? String(reference.qualityProfiles[0].id) : ''
	languageProfileId.value = reference.languageProfiles[0] ? String(reference.languageProfiles[0].id) : ''
	rootFolderId.value = reference.rootFolders[0] ? String(reference.rootFolders[0].id) : ''
	monitor.value = 'ALL'
	minimumAvailability.value = 'RELEASED'
	seriesType.value = 'STANDARD'
	seasonFolder.value = true
	tagIds.value = []
	settingsDraft.value = {}
	nameError.value = ''
	fieldErrors.value = {}
	formError.value = ''
}

function openCreate() {
	editingId.value = null
	selectedType.value = null
	resetForm()
	dialogOpen.value = true
}

function selectType(type: ImportListType) {
	selectedType.value = type
	settingsDraft.value = {}
}

function openEdit(list: ImportListDto) {
	editingId.value = list.id
	selectedType.value = list.type
	name.value = list.name
	enable.value = list.enable
	enableAutomaticAdd.value = list.enableAutomaticAdd
	searchOnAdd.value = list.searchOnAdd
	mediaKind.value = list.mediaKind
	qualityProfileId.value = list.qualityProfileId != null ? String(list.qualityProfileId) : ''
	languageProfileId.value = list.languageProfileId != null ? String(list.languageProfileId) : ''
	rootFolderId.value = list.rootFolderId != null ? String(list.rootFolderId) : ''
	monitor.value = list.monitor
	minimumAvailability.value = list.minimumAvailability ?? 'RELEASED'
	seriesType.value = list.seriesType ?? 'STANDARD'
	seasonFolder.value = list.seasonFolder
	tagIds.value = [...list.tagIds]
	settingsDraft.value = (list.settings as Record<string, unknown> | null) ?? {}
	nameError.value = ''
	fieldErrors.value = {}
	formError.value = ''
	dialogOpen.value = true
}

async function save() {
	if (!selectedType.value) {
		return
	}
	nameError.value = ''
	fieldErrors.value = {}
	formError.value = ''
	saving.value = true
	try {
		const body: SaveImportListRequest = {
			name: name.value,
			type: selectedType.value,
			enable: enable.value,
			enableAutomaticAdd: enableAutomaticAdd.value,
			searchOnAdd: searchOnAdd.value,
			settings: settingsDraft.value,
			mediaKind: mediaKind.value,
			qualityProfileId: qualityProfileId.value ? Number(qualityProfileId.value) : null,
			languageProfileId: languageProfileId.value ? Number(languageProfileId.value) : null,
			rootFolderId: rootFolderId.value ? Number(rootFolderId.value) : null,
			monitor: monitor.value,
			minimumAvailability: mediaKind.value === 'MOVIES' ? minimumAvailability.value : null,
			seriesType: mediaKind.value === 'SERIES' ? seriesType.value : null,
			seasonFolder: mediaKind.value === 'SERIES' ? seasonFolder.value : null,
			tagIds: tagIds.value,
		}
		const result = editingId.value === null
			? await api.POST('/api/v1/import-lists', { body })
			: await api.PUT('/api/v1/import-lists/{id}', { params: { path: { id: editingId.value } }, body })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: t(editingId.value === null ? 'pages.settings.importLists.added' : 'pages.settings.importLists.savedList'), tone: 'ok' })
		dialogOpen.value = false
		await loadLists()
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		fieldErrors.value = apiError.fieldErrors
		nameError.value = apiError.fieldErrors.name?.[0] ?? ''
		formError.value = apiError.message
	}
	finally {
		saving.value = false
	}
}

async function testList(id: number) {
	testing.value = true
	const result = await api.POST('/api/v1/import-lists/{id}/test', { params: { path: { id } } })
	testing.value = false
	if (!result.data) {
		toast({ title: t('pages.settings.importLists.testFailed'), tone: 'danger', description: toApiError(result.error, result.response).message })
		return
	}
	if (result.data.success) {
		toast({ title: t('pages.settings.importLists.testSucceeded'), tone: 'ok', description: t(result.data.itemCount === 1 ? 'pages.settings.importLists.foundOneItem' : 'pages.settings.importLists.foundManyItems', { count: result.data.itemCount }) })
	}
	else {
		toast({ title: t('pages.settings.importLists.testFailed'), tone: 'danger', description: result.data.message ?? t('pages.settings.importLists.fetchFailed') })
	}
}

const deleteTarget = ref<ImportListDto | null>(null)
const deleting = ref(false)

const deleteTargetOpen = computed({
	get: () => deleteTarget.value !== null,
	set: (value: boolean) => {
		if (!value) {
			deleteTarget.value = null
		}
	},
})

function confirmDelete(list: ImportListDto) {
	deleteTarget.value = list
}

async function doDelete() {
	if (!deleteTarget.value) {
		return
	}
	deleting.value = true
	const result = await api.DELETE('/api/v1/import-lists/{id}', { params: { path: { id: deleteTarget.value.id } } })
	deleting.value = false
	if (!result.response.ok) {
		toast({ title: t('pages.settings.importLists.removeFailed'), tone: 'danger', description: toApiError(result.error, result.response).message })
		return
	}
	toast({ title: t('pages.settings.importLists.deleted'), tone: 'ok' })
	deleteTarget.value = null
	await loadLists()
}

// --- Preview dialog ---

const previewOpen = ref(false)
const previewLoading = ref(false)
const previewError = ref('')
const previewItems = ref<ImportListPreviewItemDto[]>([])
const previewColumns = [
	{ key: 'title', label: t('pages.settings.importLists.titleColumn') },
	{ key: 'year', label: t('pages.settings.importLists.year'), align: 'right' as const },
	{ key: 'status', label: t('pages.settings.importLists.status') },
]

async function openPreview(list: ImportListDto) {
	previewOpen.value = true
	previewLoading.value = true
	previewError.value = ''
	previewItems.value = []
	const result = await api.GET('/api/v1/import-lists/{id}/preview', { params: { path: { id: list.id } } })
	previewLoading.value = false
	if (!result.data) {
		previewError.value = toApiError(result.error, result.response).message
		return
	}
	previewItems.value = result.data
}

function previewStatusTone(status: string): 'info' | 'neutral' | 'warn' {
	const normalized = status.toLowerCase()
	if (normalized === 'new') {
		return 'info'
	}
	if (normalized === 'excluded') {
		return 'warn'
	}
	return 'neutral'
}

function previewRowKey(row: ImportListPreviewItemDto): string | number {
	return row.tmdbId ?? row.tvdbId ?? row.title
}

function rowActions(list: ImportListDto) {
	return [
		{ label: t('pages.settings.importLists.edit'), icon: 'lucide:pencil', onSelect: () => openEdit(list) },
		{ label: t('pages.settings.importLists.preview'), icon: 'lucide:eye', onSelect: () => openPreview(list) },
		{ label: t('pages.settings.importLists.test'), icon: 'lucide:play', onSelect: () => testList(list.id) },
		{ label: t('pages.settings.importLists.delete'), icon: 'lucide:trash-2', danger: true, onSelect: () => confirmDelete(list) },
	]
}

// --- Exclusions ---

const exclusionColumns = [
	{ key: 'title', label: t('pages.settings.importLists.titleColumn') },
	{ key: 'year', label: t('pages.settings.importLists.year'), align: 'right' as const },
	{ key: 'actions', label: '', align: 'right' as const },
]

const exclusionDialogOpen = ref(false)
const exclusionTitle = ref('')
const exclusionYear = ref('')
const exclusionTvdbId = ref('')
const exclusionTmdbId = ref('')
const exclusionError = ref('')
const exclusionSaving = ref(false)

function openAddExclusion() {
	exclusionTitle.value = ''
	exclusionYear.value = ''
	exclusionTvdbId.value = ''
	exclusionTmdbId.value = ''
	exclusionError.value = ''
	exclusionDialogOpen.value = true
}

async function saveExclusion() {
	exclusionError.value = ''
	exclusionSaving.value = true
	try {
		const result = await api.POST('/api/v1/import-list-exclusions', {
			body: {
				title: exclusionTitle.value,
				year: exclusionYear.value ? Number(exclusionYear.value) : null,
				tvdbId: exclusionTvdbId.value ? Number(exclusionTvdbId.value) : null,
				tmdbId: exclusionTmdbId.value ? Number(exclusionTmdbId.value) : null,
			},
		})
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: t('pages.settings.importLists.exclusionAdded'), tone: 'ok' })
		exclusionDialogOpen.value = false
		await loadExclusions()
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		exclusionError.value = apiError.message
	}
	finally {
		exclusionSaving.value = false
	}
}

async function removeExclusion(exclusion: ImportListExclusionDto) {
	const result = await api.DELETE('/api/v1/import-list-exclusions/{id}', { params: { path: { id: exclusion.id } } })
	if (!result.response.ok) {
		toast({ title: t('pages.settings.importLists.exclusionRemoveFailed'), tone: 'danger', description: toApiError(result.error, result.response).message })
		return
	}
	await loadExclusions()
}
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.settings.importLists.title')">
			<template #actions>
				<SButton
					variant="primary"
					@click="openCreate"
				>
					{{ t('pages.settings.importLists.addImportList') }}
				</SButton>
			</template>
		</SPageHeader>

		<SSection :title="t('pages.settings.importLists.librarySync')">
			<SEmptyState
				v-if="configLoadError"
				:message="configLoadError"
				icon="lucide:alert-triangle"
			>
				<template #action>
					<SButton @click="loadConfig">
						{{ t('pages.settings.importLists.retry') }}
					</SButton>
				</template>
			</SEmptyState>
			<template v-else-if="configDraft">
				<SField
					:label="t('pages.settings.importLists.cleanLibraryLevel')"
					:hint="t('pages.settings.importLists.cleanLibraryLevelHint')"
					control-id="clean-library-level"
				>
					<SSelect
						v-model="configDraft.cleanLibraryLevel"
						control-id="clean-library-level"
						:options="cleanLibraryLevelOptions.map(option => ({ ...option, label: t(option.label, option.label) }))"
					/>
				</SField>
				<SettingsSaveBar
					:dirty="configDirty.isDirty.value"
					:saving="configSaving"
					@save="saveConfig"
					@discard="configDirty.revert()"
				/>
			</template>
		</SSection>

		<SSection>
			<SEmptyState
				v-if="loadError"
				:message="loadError"
				icon="lucide:alert-triangle"
			>
				<template #action>
					<SButton @click="loadLists">
						{{ t('pages.settings.importLists.retry') }}
					</SButton>
				</template>
			</SEmptyState>
			<SSpinner v-else-if="loading" />
			<STable
				v-else
				:columns="listColumns"
				:rows="lists"
				:row-key="(row) => row.id"
			>
				<template #cell-type="{ row }">
					{{ t(importListTypeLabel(row.type)) }}
				</template>
				<template #cell-mediaKind="{ row }">
					{{ t(mediaKindLabel(row.mediaKind)) }}
				</template>
				<template #cell-enable="{ row }">
					<SBadge :tone="row.enable ? 'ok' : 'neutral'">
						{{ row.enable ? t('pages.settings.importLists.enabled') : t('pages.settings.importLists.disabled') }}
					</SBadge>
				</template>
				<template #cell-actions="{ row }">
					<SDropdownMenu :items="rowActions(row)">
						<template #trigger>
							<SIconButton :label="t('pages.settings.importLists.actions')">
								<Icon
									name="lucide:more-horizontal"
									aria-hidden="true"
								/>
							</SIconButton>
						</template>
					</SDropdownMenu>
				</template>
				<template #empty>
					<SEmptyState :message="t('pages.settings.importLists.emptyState')">
						<template #action>
							<SButton
								variant="primary"
								@click="openCreate"
							>
								{{ t('pages.settings.importLists.addImportList') }}
							</SButton>
						</template>
					</SEmptyState>
				</template>
			</STable>
		</SSection>

		<SSection :title="t('pages.settings.importLists.excludedTitle')">
			<SEmptyState
				v-if="exclusionsLoadError"
				:message="exclusionsLoadError"
				icon="lucide:alert-triangle"
			>
				<template #action>
					<SButton @click="loadExclusions">
						{{ t('pages.settings.importLists.retry') }}
					</SButton>
				</template>
			</SEmptyState>
			<SSpinner v-else-if="exclusionsLoading" />
			<STable
				v-else
				:columns="exclusionColumns"
				:rows="exclusions"
				:row-key="(row) => row.id"
			>
				<template #cell-actions="{ row }">
					<SButton
						variant="ghost"
						@click="removeExclusion(row)"
					>
						{{ t('pages.settings.importLists.remove') }}
					</SButton>
				</template>
				<template #empty>
					<SEmptyState :message="t('pages.settings.importLists.exclusionsEmpty')">
						<template #action>
							<SButton
								variant="secondary"
								@click="openAddExclusion"
							>
								{{ t('pages.settings.importLists.addExclusion') }}
							</SButton>
						</template>
					</SEmptyState>
				</template>
			</STable>
			<div
				v-if="!exclusionsLoading && exclusions.length > 0"
				class="exclusion-actions"
			>
				<SButton
					variant="secondary"
					@click="openAddExclusion"
				>
					{{ t('pages.settings.importLists.addExclusion') }}
				</SButton>
			</div>
		</SSection>

		<SDialog
			v-model="dialogOpen"
			:title="editingId === null ? t('pages.settings.importLists.addImportList') : t('pages.settings.importLists.editImportList')"
			wide
		>
			<div
				v-if="selectedType === null"
				class="type-grid"
			>
				<ProviderCard
					v-for="option in importListTypeOptions"
					:key="option.value"
					:icon="importListTypeIcon(option.value)"
					:label="t(option.label, option.label)"
					@click="selectType(option.value as ImportListType)"
				/>
			</div>

			<div
				v-else
				class="list-form"
			>
				<button
					v-if="editingId === null"
					type="button"
					class="change-type"
					@click="selectedType = null"
				>
					<Icon
						name="lucide:arrow-left"
						aria-hidden="true"
					/>
					{{ t('pages.settings.importLists.changeType', { type: t(importListTypeLabel(selectedType)) }) }}
				</button>

				<p
					v-if="formError"
					class="s-field-error"
					role="alert"
				>
					{{ formError }}
				</p>

				<SField
					:label="t('pages.settings.importLists.name')"
					:error="nameError"
					control-id="list-name"
				>
					<SInput
						id="list-name"
						v-model="name"
						:invalid="!!nameError"
					/>
				</SField>

				<div class="switch-row">
					<SSwitch
						v-model="enable"
						:label="t('pages.settings.importLists.enable')"
					/>
					<SSwitch
						v-model="enableAutomaticAdd"
						:label="t('pages.settings.importLists.enableAutomaticAdd')"
					/>
					<SSwitch
						v-model="searchOnAdd"
						:label="t('pages.settings.importLists.searchOnAdd')"
					/>
				</div>

				<div class="field-grid">
					<SField
						:label="t('pages.settings.importLists.mediaKind')"
						control-id="list-media-kind"
					>
						<SSelect
							v-model="mediaKind"
							control-id="list-media-kind"
							:options="mediaKindOptions.map(option => ({ ...option, label: t(option.label, option.label) }))"
						/>
					</SField>
					<SField
						:label="t('pages.settings.importLists.qualityProfile')"
						control-id="list-quality-profile"
					>
						<SSelect
							v-model="qualityProfileId"
							control-id="list-quality-profile"
							:options="qualityProfileOptions"
						/>
					</SField>
					<SField
						:label="t('pages.settings.importLists.languageProfile')"
						control-id="list-language-profile"
					>
						<SSelect
							v-model="languageProfileId"
							control-id="list-language-profile"
							:options="languageProfileOptions"
						/>
					</SField>
					<SField
						:label="t('pages.settings.importLists.rootFolder')"
						control-id="list-root-folder"
					>
						<SSelect
							v-model="rootFolderId"
							control-id="list-root-folder"
							:options="rootFolderOptions"
						/>
					</SField>
					<SField
						:label="t('pages.settings.importLists.monitor')"
						control-id="list-monitor"
					>
						<SSelect
							v-model="monitor"
							control-id="list-monitor"
							:options="monitorNewItemsOptions.map(option => ({ ...option, label: t(option.label, option.label) }))"
						/>
					</SField>
					<SField
						v-if="mediaKind === 'MOVIES'"
						:label="t('pages.settings.importLists.minimumAvailability')"
						control-id="list-minimum-availability"
					>
						<SSelect
							v-model="minimumAvailability"
							control-id="list-minimum-availability"
							:options="minimumAvailabilityOptions.map(option => ({ ...option, label: t(option.label, option.label) }))"
						/>
					</SField>
					<SField
						v-if="mediaKind === 'SERIES'"
						:label="t('pages.settings.importLists.seriesType')"
						control-id="list-series-type"
					>
						<SSelect
							v-model="seriesType"
							control-id="list-series-type"
							:options="seriesTypeOptions.map(option => ({ ...option, label: t(option.label, option.label) }))"
						/>
					</SField>
				</div>

				<SSwitch
					v-if="mediaKind === 'SERIES'"
					v-model="seasonFolder"
					:label="t('pages.settings.importLists.useSeasonFolders')"
				/>

				<SField
					:label="t('pages.settings.importLists.tags')"
					control-id="list-tags"
				>
					<TagPicker
						v-model:tag-ids="tagIds"
						control-id="list-tags"
					/>
				</SField>

				<SchemaForm
					v-if="normalizedFields.length > 0"
					v-model="settingsDraft"
					provider-type="import-lists"
					:fields="normalizedFields"
					:field-errors="fieldErrors"
				/>
			</div>

			<template
				v-if="selectedType !== null"
				#footer
			>
				<SButton
					variant="secondary"
					:disabled="saving"
					@click="dialogOpen = false"
				>
					{{ t('pages.settings.importLists.cancel') }}
				</SButton>
				<SButton
					v-if="editingId !== null"
					variant="secondary"
					:loading="testing"
					@click="testList(editingId)"
				>
					{{ t('pages.settings.importLists.test') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="saving"
					@click="save"
				>
					{{ editingId === null ? t('pages.settings.importLists.addImportList') : t('pages.settings.importLists.saveChanges') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteTargetOpen"
			:title="t('pages.settings.importLists.deleteImportList')"
		>
			<p v-if="deleteTarget">
				{{ t('pages.settings.importLists.deleteConfirm', { name: deleteTarget.name }) }}
			</p>
			<template #footer>
				<SButton
					variant="secondary"
					:disabled="deleting"
					@click="deleteTarget = null"
				>
					{{ t('pages.settings.importLists.cancel') }}
				</SButton>
				<SButton
					variant="danger"
					:loading="deleting"
					@click="doDelete"
				>
					{{ t('pages.settings.importLists.deleteImportList') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="previewOpen"
			:title="t('pages.settings.importLists.preview')"
			:description="t('pages.settings.importLists.previewDescription')"
			wide
		>
			<SSpinner v-if="previewLoading" />
			<p
				v-else-if="previewError"
				class="s-field-error"
				role="alert"
			>
				{{ previewError }}
			</p>
			<STable
				v-else
				:columns="previewColumns"
				:rows="previewItems"
				:row-key="previewRowKey"
			>
				<template #cell-status="{ row }">
					<SBadge :tone="previewStatusTone(row.status)">
						{{ row.status }}
					</SBadge>
				</template>
				<template #empty>
					<SEmptyState :message="t('pages.settings.importLists.previewEmpty')" />
				</template>
			</STable>
			<template #footer>
				<SButton
					variant="secondary"
					@click="previewOpen = false"
				>
					{{ t('pages.settings.importLists.close') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="exclusionDialogOpen"
			:title="t('pages.settings.importLists.addExclusion')"
		>
			<SField
				:label="t('pages.settings.importLists.titleColumn')"
				control-id="exclusion-title"
			>
				<SInput
					id="exclusion-title"
					v-model="exclusionTitle"
				/>
			</SField>
			<div class="field-grid">
				<SField
					:label="t('pages.settings.importLists.year')"
					control-id="exclusion-year"
				>
					<SInput
						id="exclusion-year"
						v-model="exclusionYear"
						type="number"
					/>
				</SField>
				<SField
					:label="t('pages.settings.importLists.tvdbId')"
					:hint="t('pages.settings.importLists.forSeries')"
					control-id="exclusion-tvdb"
				>
					<SInput
						id="exclusion-tvdb"
						v-model="exclusionTvdbId"
						type="number"
					/>
				</SField>
				<SField
					:label="t('pages.settings.importLists.tmdbId')"
					:hint="t('pages.settings.importLists.forMovies')"
					control-id="exclusion-tmdb"
				>
					<SInput
						id="exclusion-tmdb"
						v-model="exclusionTmdbId"
						type="number"
					/>
				</SField>
			</div>
			<p
				v-if="exclusionError"
				class="s-field-error"
				role="alert"
			>
				{{ exclusionError }}
			</p>
			<template #footer>
				<SButton
					variant="secondary"
					:disabled="exclusionSaving"
					@click="exclusionDialogOpen = false"
				>
					{{ t('pages.settings.importLists.cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="exclusionSaving"
					@click="saveExclusion"
				>
					{{ t('pages.settings.importLists.addExclusion') }}
				</SButton>
			</template>
		</SDialog>
	</div>
</template>

<style scoped>
.type-grid {
	display: grid;
	grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
	gap: 12px;
}

.list-form {
	display: grid;
	gap: 16px;
}

.change-type {
	display: inline-flex;
	align-items: center;
	gap: 6px;
	align-self: flex-start;
	border: none;
	background: transparent;
	color: var(--fg-muted);
	font-size: var(--text-sm);
	cursor: pointer;
	padding: 0;
}

.change-type:hover {
	color: var(--fg);
}

.switch-row {
	display: flex;
	flex-wrap: wrap;
	gap: 24px;
}

.field-grid {
	display: grid;
	grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
	gap: 16px;
}

.exclusion-actions {
	margin-top: 12px;
}
</style>
