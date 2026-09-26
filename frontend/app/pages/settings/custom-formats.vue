<script setup lang="ts">
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useSettingsStore } from '~/stores/settings'
import {
	customFormatSpecTypeOptions,
	indexerFlagOptions,
	languageOptions,
	protocolOptions,
	qualityResolutionLabel,
	qualitySourceOptions,
	releaseFilterFieldLabel,
	releaseFilterFieldOptions,
	releaseFilterModeLabel,
	releaseFilterModeOptions,
	releaseFlagOptions,
	streamingProviderOptions,
} from '~/utils/settings-labels'
import type { components } from '~/types/api'

type CustomFormatResource = components['schemas']['CustomFormatResource']
type SpecificationResource = components['schemas']['SpecificationResource']
type CustomFormatSpecificationType = components['schemas']['CustomFormatSpecificationType']
type ReleaseFilterResource = components['schemas']['ReleaseFilterResource']
type ReleaseGroupOverrideResource = components['schemas']['ReleaseGroupOverrideResource']

definePageMeta({ layout: 'default' })
useHead({ title: 'Custom formats' })

const api = useApi()
const settings = useSettingsStore()
const { toast } = useToast()

const RESOLUTIONS = ['R360_P', 'R480_P', 'R540_P', 'R576_P', 'R720_P', 'R1080_P', 'R2160_P']
const resolutionOptions = RESOLUTIONS.map(value => ({ value, label: qualityResolutionLabel(value) }))

const loading = ref(true)

// --- Custom formats ----------------------------------------------------------
const editorOpen = ref(false)
const editingId = ref<number | null>(null)
const name = ref('')
const includeWhenRenaming = ref(false)
const specs = ref<SpecificationResource[]>([])
const nameError = ref('')
const saving = ref(false)

const deleteTarget = ref<CustomFormatResource | null>(null)
const deleteTargetOpen = computed({
	get: () => deleteTarget.value !== null,
	set: (value: boolean) => {
		if (!value) {
			deleteTarget.value = null
		}
	},
})
const deleting = ref(false)
const deleteError = ref('')

async function load() {
	loading.value = true
	await settings.refreshCustomFormats()
	loading.value = false
}

void load()

function specDefaultValue(type: CustomFormatSpecificationType): unknown {
	switch (type) {
		case 'HARDCODED_SUBS':
			return true
		case 'SIZE':
		case 'YEAR':
			return { min: null, max: null }
		default:
			return ''
	}
}

function addSpec() {
	specs.value = [...specs.value, { name: '', type: 'RELEASE_TITLE', negate: false, required: false, value: specDefaultValue('RELEASE_TITLE') }]
}

function removeSpec(index: number) {
	specs.value = specs.value.filter((_, i) => i !== index)
}

function onSpecTypeChange(index: number, type: CustomFormatSpecificationType) {
	const next = [...specs.value]
	next[index] = { ...next[index]!, type, value: specDefaultValue(type) }
	specs.value = next
}

function specOptions(type: CustomFormatSpecificationType) {
	switch (type) {
		case 'LANGUAGE':
			return languageOptions
		case 'QUALITY_SOURCE':
			return qualitySourceOptions
		case 'RESOLUTION':
			return resolutionOptions
		case 'STREAMING_PROVIDER':
			return streamingProviderOptions
		case 'RELEASE_FLAG':
			return releaseFlagOptions
		case 'PROTOCOL':
			return protocolOptions
		case 'INDEXER_FLAG':
			return indexerFlagOptions
		default:
			return []
	}
}

function isSelectSpec(type: CustomFormatSpecificationType) {
	return specOptions(type).length > 0
}

function openCreate() {
	editingId.value = null
	name.value = ''
	includeWhenRenaming.value = false
	specs.value = []
	nameError.value = ''
	editorOpen.value = true
}

function openEdit(format: CustomFormatResource) {
	editingId.value = format.id
	name.value = format.name
	includeWhenRenaming.value = format.includeCustomFormatWhenRenaming
	specs.value = format.specifications.map(spec => ({ ...spec }))
	nameError.value = ''
	editorOpen.value = true
}

async function save() {
	nameError.value = ''
	saving.value = true
	const body = {
		name: name.value,
		includeCustomFormatWhenRenaming: includeWhenRenaming.value,
		specifications: specs.value,
	}
	try {
		const result = editingId.value === null
			? await api.POST('/api/v1/custom-formats', { body })
			: await api.PUT('/api/v1/custom-formats/{id}', { params: { path: { id: editingId.value } }, body })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: 'Saved', tone: 'ok' })
		editorOpen.value = false
		await load()
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		nameError.value = apiError.fieldErrors.name?.[0] ?? apiError.message
	}
	finally {
		saving.value = false
	}
}

function confirmDelete(format: CustomFormatResource) {
	deleteError.value = ''
	deleteTarget.value = format
}

async function doDelete() {
	if (!deleteTarget.value) {
		return
	}
	deleting.value = true
	deleteError.value = ''
	const result = await api.DELETE('/api/v1/custom-formats/{id}', { params: { path: { id: deleteTarget.value.id } } })
	if (!result.response.ok) {
		deleteError.value = toApiError(result.error, result.response).message
		deleting.value = false
		return
	}
	toast({ title: 'Custom format deleted', tone: 'ok' })
	deleteTarget.value = null
	deleting.value = false
	await load()
}

async function exportFormat(format: CustomFormatResource) {
	const result = await api.GET('/api/v1/custom-formats/{id}/export', { params: { path: { id: format.id } } })
	if (!result.data) {
		toast({ title: 'Could not export', description: toApiError(result.error, result.response).message, tone: 'danger' })
		return
	}
	const blob = new Blob([JSON.stringify(result.data, null, 2)], { type: 'application/json' })
	const url = URL.createObjectURL(blob)
	const link = document.createElement('a')
	link.href = url
	link.download = `${format.name}.json`
	link.click()
	URL.revokeObjectURL(url)
}

// --- Import ------------------------------------------------------------------
const importOpen = ref(false)
const importText = ref('')
const importError = ref('')
const importing = ref(false)

function openImport() {
	importText.value = ''
	importError.value = ''
	importOpen.value = true
}

async function runImport() {
	importError.value = ''
	let parsed: unknown
	try {
		parsed = JSON.parse(importText.value)
	}
	catch {
		importError.value = 'That is not valid JSON.'
		return
	}
	importing.value = true
	const result = await api.POST('/api/v1/custom-formats/import', { body: parsed })
	importing.value = false
	if (!result.data) {
		importError.value = toApiError(result.error, result.response).message
		return
	}
	toast({ title: `Imported ${result.data.length} custom format${result.data.length === 1 ? '' : 's'}`, tone: 'ok' })
	importOpen.value = false
	await load()
}

// --- Test ---------------------------------------------------------------------
const testOpen = ref(false)
const testTitle = ref('')
const testSizeGb = ref<number | null>(null)
const testing = ref(false)
const testResult = ref<{ matched: CustomFormatResource[], score: number } | null>(null)
const testError = ref('')

function openTest() {
	testTitle.value = ''
	testSizeGb.value = null
	testResult.value = null
	testError.value = ''
	testOpen.value = true
}

async function runTest() {
	testError.value = ''
	testing.value = true
	const result = await api.POST('/api/v1/custom-formats/test', {
		body: {
			title: testTitle.value,
			size: testSizeGb.value === null ? null : Math.round(testSizeGb.value * 1024 * 1024 * 1024),
			formatItems: null,
		},
	})
	testing.value = false
	if (!result.data) {
		testError.value = toApiError(result.error, result.response).message
		return
	}
	testResult.value = result.data
}

const formatColumns = [
	{ key: 'name', label: 'Name' },
	{ key: 'specifications', label: 'Specifications', align: 'right' as const },
	{ key: 'renaming', label: 'Renamed' },
	{ key: 'actions', label: '', align: 'right' as const },
]

// --- Release filters -----------------------------------------------------------
const filters = ref<ReleaseFilterResource[]>([])
const filtersLoadError = ref('')
const filterEditorOpen = ref(false)
const filterEditingId = ref<number | null>(null)
const filterField = ref<components['schemas']['ReleaseFilterField']>('RELEASE_GROUP')
const filterMode = ref<components['schemas']['ReleaseFilterMode']>('BLOCK')
const filterTier = ref(0)
const filterValues = ref<string[]>([])
const filterValueTerm = ref('')
const filterSaving = ref(false)
const filterDeleteTarget = ref<ReleaseFilterResource | null>(null)
const filterDeleteTargetOpen = computed({
	get: () => filterDeleteTarget.value !== null,
	set: (value: boolean) => {
		if (!value) {
			filterDeleteTarget.value = null
		}
	},
})

async function loadFilters() {
	filtersLoadError.value = ''
	const result = await api.GET('/api/v1/release-filters')
	if (!result.data) {
		filtersLoadError.value = 'Could not load release filters. Check your connection and try again.'
	}
	filters.value = result.data ?? []
}

void loadFilters()

function openFilterCreate() {
	filterEditingId.value = null
	filterField.value = 'RELEASE_GROUP'
	filterMode.value = 'BLOCK'
	filterTier.value = 0
	filterValues.value = []
	filterValueTerm.value = ''
	filterEditorOpen.value = true
}

function openFilterEdit(filter: ReleaseFilterResource) {
	filterEditingId.value = filter.id
	filterField.value = filter.field
	filterMode.value = filter.mode
	filterTier.value = filter.tier
	filterValues.value = [...filter.values]
	filterValueTerm.value = ''
	filterEditorOpen.value = true
}

function addFilterValue() {
	const value = filterValueTerm.value.trim()
	if (value.length === 0) {
		return
	}
	filterValues.value = [...filterValues.value, value]
	filterValueTerm.value = ''
}

async function saveFilter() {
	filterSaving.value = true
	const body = { field: filterField.value, values: filterValues.value, mode: filterMode.value, tier: filterTier.value }
	const result = filterEditingId.value === null
		? await api.POST('/api/v1/release-filters', { body })
		: await api.PUT('/api/v1/release-filters/{id}', { params: { path: { id: filterEditingId.value } }, body })
	filterSaving.value = false
	if (!result.data) {
		toast({ title: 'Could not save', description: toApiError(result.error, result.response).message, tone: 'danger' })
		return
	}
	toast({ title: 'Saved', tone: 'ok' })
	filterEditorOpen.value = false
	await loadFilters()
}

async function deleteFilter() {
	if (!filterDeleteTarget.value) {
		return
	}
	const result = await api.DELETE('/api/v1/release-filters/{id}', { params: { path: { id: filterDeleteTarget.value.id } } })
	if (!result.response.ok) {
		toast({ title: 'Could not delete', description: toApiError(result.error, result.response).message, tone: 'danger' })
		return
	}
	toast({ title: 'Release filter deleted', tone: 'ok' })
	filterDeleteTarget.value = null
	await loadFilters()
}

const filterColumns = [
	{ key: 'field', label: 'Field' },
	{ key: 'mode', label: 'Mode' },
	{ key: 'values', label: 'Values' },
	{ key: 'tier', label: 'Tier', align: 'right' as const },
	{ key: 'actions', label: '', align: 'right' as const },
]

// --- Quality overrides -----------------------------------------------------------
const overrides = ref<ReleaseGroupOverrideResource[]>([])
const overridesLoadError = ref('')
const overrideEditorOpen = ref(false)
const overrideEditingId = ref<number | null>(null)
const overrideGroup = ref('')
const overrideSource = ref<Exclude<components['schemas']['QualitySource'], null>>('WEB_DL')
const overrideError = ref('')
const overrideSaving = ref(false)
const overrideDeleteTarget = ref<ReleaseGroupOverrideResource | null>(null)
const overrideDeleteTargetOpen = computed({
	get: () => overrideDeleteTarget.value !== null,
	set: (value: boolean) => {
		if (!value) {
			overrideDeleteTarget.value = null
		}
	},
})

async function loadOverrides() {
	overridesLoadError.value = ''
	const result = await api.GET('/api/v1/quality-overrides')
	if (!result.data) {
		overridesLoadError.value = 'Could not load quality overrides. Check your connection and try again.'
	}
	overrides.value = result.data ?? []
}

void loadOverrides()

function openOverrideCreate() {
	overrideEditingId.value = null
	overrideGroup.value = ''
	overrideSource.value = 'WEB_DL'
	overrideError.value = ''
	overrideEditorOpen.value = true
}

function openOverrideEdit(override: ReleaseGroupOverrideResource) {
	overrideEditingId.value = override.id
	overrideGroup.value = override.releaseGroup
	overrideSource.value = override.source ?? 'WEB_DL'
	overrideError.value = ''
	overrideEditorOpen.value = true
}

async function saveOverride() {
	overrideError.value = ''
	overrideSaving.value = true
	const body = { releaseGroup: overrideGroup.value, source: overrideSource.value }
	try {
		const result = overrideEditingId.value === null
			? await api.POST('/api/v1/quality-overrides', { body })
			: await api.PUT('/api/v1/quality-overrides/{id}', { params: { path: { id: overrideEditingId.value } }, body })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: 'Saved', tone: 'ok' })
		overrideEditorOpen.value = false
		await loadOverrides()
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		overrideError.value = apiError.fieldErrors.releaseGroup?.[0] ?? apiError.message
	}
	finally {
		overrideSaving.value = false
	}
}

async function deleteOverride() {
	if (!overrideDeleteTarget.value) {
		return
	}
	const result = await api.DELETE('/api/v1/quality-overrides/{id}', { params: { path: { id: overrideDeleteTarget.value.id } } })
	if (!result.response.ok) {
		toast({ title: 'Could not delete', description: toApiError(result.error, result.response).message, tone: 'danger' })
		return
	}
	toast({ title: 'Override deleted', tone: 'ok' })
	overrideDeleteTarget.value = null
	await loadOverrides()
}

const overrideColumns = [
	{ key: 'releaseGroup', label: 'Release group' },
	{ key: 'source', label: 'Assumed source' },
	{ key: 'actions', label: '', align: 'right' as const },
]
</script>

<template>
	<div>
		<SPageHeader title="Custom formats">
			<template #actions>
				<SButton
					variant="secondary"
					@click="openTest"
				>
					Test
				</SButton>
				<SButton
					variant="secondary"
					@click="openImport"
				>
					Import
				</SButton>
				<SButton
					variant="primary"
					@click="openCreate"
				>
					Add custom format
				</SButton>
			</template>
		</SPageHeader>

		<SSection>
			<SEmptyState
				v-if="settings.customFormatsLoadError"
				:message="settings.customFormatsLoadError"
				icon="lucide:alert-triangle"
			>
				<template #action>
					<SButton @click="load">
						Retry
					</SButton>
				</template>
			</SEmptyState>
			<SSpinner v-else-if="loading" />
			<STable
				v-else
				:columns="formatColumns"
				:rows="settings.customFormats"
				:row-key="(row) => row.id"
			>
				<template #cell-specifications="{ row }">
					{{ row.specifications.length }}
				</template>
				<template #cell-renaming="{ row }">
					<SBadge :tone="row.includeCustomFormatWhenRenaming ? 'ok' : 'neutral'">
						{{ row.includeCustomFormatWhenRenaming ? 'Yes' : 'No' }}
					</SBadge>
				</template>
				<template #cell-actions="{ row }">
					<SDropdownMenu
						:items="[
							{ label: 'Edit', icon: 'lucide:pencil', onSelect: () => openEdit(row) },
							{ label: 'Export', icon: 'lucide:download', onSelect: () => exportFormat(row) },
							{ label: 'Delete', icon: 'lucide:trash-2', danger: true, onSelect: () => confirmDelete(row) },
						]"
					>
						<template #trigger>
							<SIconButton label="Custom format actions">
								<Icon
									name="lucide:more-horizontal"
									aria-hidden="true"
								/>
							</SIconButton>
						</template>
					</SDropdownMenu>
				</template>
				<template #empty>
					<SEmptyState message="No custom formats yet. Import a TRaSH format or build one from scratch.">
						<template #action>
							<SButton
								variant="primary"
								@click="openCreate"
							>
								Add custom format
							</SButton>
						</template>
					</SEmptyState>
				</template>
			</STable>
		</SSection>

		<SSection title="Release filters">
			<SEmptyState
				v-if="filtersLoadError"
				:message="filtersLoadError"
				icon="lucide:alert-triangle"
			>
				<template #action>
					<SButton @click="loadFilters">
						Retry
					</SButton>
				</template>
			</SEmptyState>
			<STable
				v-else
				:columns="filterColumns"
				:rows="filters"
				:row-key="(row) => row.id"
			>
				<template #cell-field="{ row }">
					{{ releaseFilterFieldLabel(row.field) }}
				</template>
				<template #cell-mode="{ row }">
					{{ releaseFilterModeLabel(row.mode) }}
				</template>
				<template #cell-values="{ row }">
					{{ row.values.join(', ') }}
				</template>
				<template #cell-actions="{ row }">
					<SDropdownMenu
						:items="[
							{ label: 'Edit', icon: 'lucide:pencil', onSelect: () => openFilterEdit(row) },
							{ label: 'Delete', icon: 'lucide:trash-2', danger: true, onSelect: () => filterDeleteTarget = row },
						]"
					>
						<template #trigger>
							<SIconButton label="Filter actions">
								<Icon
									name="lucide:more-horizontal"
									aria-hidden="true"
								/>
							</SIconButton>
						</template>
					</SDropdownMenu>
				</template>
				<template #empty>
					<SEmptyState message="No release filters yet. Add one to allow, block or prefer releases by group, quality, language or source.">
						<template #action>
							<SButton
								variant="primary"
								@click="openFilterCreate"
							>
								Add release filter
							</SButton>
						</template>
					</SEmptyState>
				</template>
			</STable>
			<div
				v-if="filters.length > 0"
				class="section-actions"
			>
				<SButton
					variant="secondary"
					@click="openFilterCreate"
				>
					Add release filter
				</SButton>
			</div>
		</SSection>

		<SSection title="Quality overrides">
			<p class="hint">
				Assume a quality source for releases from a release group when the title doesn't say.
			</p>
			<SEmptyState
				v-if="overridesLoadError"
				:message="overridesLoadError"
				icon="lucide:alert-triangle"
			>
				<template #action>
					<SButton @click="loadOverrides">
						Retry
					</SButton>
				</template>
			</SEmptyState>
			<STable
				v-else
				:columns="overrideColumns"
				:rows="overrides"
				:row-key="(row) => row.id"
			>
				<template #cell-source="{ row }">
					{{ qualitySourceOptions.find(o => o.value === row.source)?.label ?? row.source }}
				</template>
				<template #cell-actions="{ row }">
					<SDropdownMenu
						:items="[
							{ label: 'Edit', icon: 'lucide:pencil', onSelect: () => openOverrideEdit(row) },
							{ label: 'Delete', icon: 'lucide:trash-2', danger: true, onSelect: () => overrideDeleteTarget = row },
						]"
					>
						<template #trigger>
							<SIconButton label="Override actions">
								<Icon
									name="lucide:more-horizontal"
									aria-hidden="true"
								/>
							</SIconButton>
						</template>
					</SDropdownMenu>
				</template>
				<template #empty>
					<SEmptyState message="No quality overrides yet.">
						<template #action>
							<SButton
								variant="primary"
								@click="openOverrideCreate"
							>
								Add override
							</SButton>
						</template>
					</SEmptyState>
				</template>
			</STable>
			<div
				v-if="overrides.length > 0"
				class="section-actions"
			>
				<SButton
					variant="secondary"
					@click="openOverrideCreate"
				>
					Add override
				</SButton>
			</div>
		</SSection>

		<!-- Custom format editor -->
		<SDialog
			v-model="editorOpen"
			:title="editingId === null ? 'Add custom format' : 'Edit custom format'"
			wide
		>
			<SField
				label="Name"
				:error="nameError"
				control-id="format-name"
			>
				<SInput
					id="format-name"
					v-model="name"
					:invalid="!!nameError"
				/>
			</SField>
			<SSwitch
				v-model="includeWhenRenaming"
				label="Include the format name when renaming"
			/>

			<SSection title="Specifications">
				<div class="spec-list">
					<div
						v-for="(spec, index) in specs"
						:key="index"
						class="spec-row"
					>
						<SInput
							v-model="spec.name"
							placeholder="Specification name"
							class="spec-name"
						/>
						<SSelect
							:model-value="spec.type"
							:options="customFormatSpecTypeOptions"
							@update:model-value="onSpecTypeChange(index, $event as CustomFormatSpecificationType)"
						/>
						<template v-if="isSelectSpec(spec.type)">
							<SSelect
								:model-value="spec.value as string"
								:options="specOptions(spec.type)"
								@update:model-value="spec.value = $event"
							/>
						</template>
						<template v-else-if="spec.type === 'HARDCODED_SUBS'">
							<SCheckbox
								:model-value="spec.value !== false"
								label="Present"
								@update:model-value="spec.value = $event"
							/>
						</template>
						<template v-else-if="spec.type === 'SIZE' || spec.type === 'YEAR'">
							<div class="spec-range">
								<input
									type="number"
									class="s-input"
									placeholder="Min"
									:value="(spec.value as { min: number | null, max: number | null })?.min ?? ''"
									@input="spec.value = { ...(spec.value as object), min: ($event.target as HTMLInputElement).valueAsNumber || null }"
								>
								<input
									type="number"
									class="s-input"
									placeholder="Max"
									:value="(spec.value as { min: number | null, max: number | null })?.max ?? ''"
									@input="spec.value = { ...(spec.value as object), max: ($event.target as HTMLInputElement).valueAsNumber || null }"
								>
							</div>
						</template>
						<template v-else>
							<SInput
								:model-value="spec.value as string"
								placeholder="Text or /regex/"
								@update:model-value="spec.value = $event"
							/>
						</template>
						<SCheckbox
							v-model="spec.negate"
							label="Negate"
						/>
						<SCheckbox
							v-model="spec.required"
							label="Required"
						/>
						<SIconButton
							label="Remove specification"
							@click="removeSpec(index)"
						>
							<Icon
								name="lucide:x"
								aria-hidden="true"
							/>
						</SIconButton>
					</div>
				</div>
				<SButton
					variant="secondary"
					@click="addSpec"
				>
					Add specification
				</SButton>
			</SSection>

			<template #footer>
				<SButton
					variant="secondary"
					@click="editorOpen = false"
				>
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="saving"
					@click="save"
				>
					Save changes
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteTargetOpen"
			title="Delete custom format"
		>
			<p v-if="deleteTarget">
				Delete "{{ deleteTarget.name }}"? This cannot be undone.
			</p>
			<p
				v-if="deleteError"
				class="s-field-error"
				role="alert"
			>
				{{ deleteError }}
			</p>
			<template #footer>
				<SButton
					variant="secondary"
					:disabled="deleting"
					@click="deleteTarget = null"
				>
					Cancel
				</SButton>
				<SButton
					variant="danger"
					:loading="deleting"
					@click="doDelete"
				>
					Delete
				</SButton>
			</template>
		</SDialog>

		<!-- Import -->
		<SDialog
			v-model="importOpen"
			title="Import custom formats"
			description="Paste a TRaSH Guides custom format JSON object, or an array of them."
		>
			<SField
				label="TRaSH JSON"
				:error="importError"
			>
				<STextarea
					v-model="importText"
					:rows="10"
					:invalid="!!importError"
					placeholder="{&quot;name&quot;: &quot;...&quot;, &quot;specifications&quot;: [...] }"
				/>
			</SField>
			<template #footer>
				<SButton
					variant="secondary"
					@click="importOpen = false"
				>
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="importing"
					@click="runImport"
				>
					Import
				</SButton>
			</template>
		</SDialog>

		<!-- Test -->
		<SDialog
			v-model="testOpen"
			title="Test a release title"
		>
			<SField label="Release title">
				<SInput
					v-model="testTitle"
					placeholder="Some.Series.S01E01.1080p.WEB-DL.DDP5.1.H.264-GROUP"
				/>
			</SField>
			<SField
				label="Size (GB)"
				hint="Optional, used by size specifications"
			>
				<SInput
					type="number"
					:model-value="testSizeGb === null ? '' : String(testSizeGb)"
					@update:model-value="testSizeGb = $event === '' ? null : Number($event)"
				/>
			</SField>
			<p
				v-if="testError"
				class="s-field-error"
				role="alert"
			>
				{{ testError }}
			</p>
			<div v-if="testResult">
				<p class="hint">
					Score: {{ testResult.score }}
				</p>
				<div
					v-if="testResult.matched.length > 0"
					class="test-matches"
				>
					<SBadge
						v-for="format in testResult.matched"
						:key="format.id"
						tone="ok"
					>
						{{ format.name }}
					</SBadge>
				</div>
				<p
					v-else
					class="hint"
				>
					No custom formats matched.
				</p>
			</div>
			<template #footer>
				<SButton
					variant="secondary"
					@click="testOpen = false"
				>
					Close
				</SButton>
				<SButton
					variant="primary"
					:loading="testing"
					:disabled="!testTitle"
					@click="runTest"
				>
					Test
				</SButton>
			</template>
		</SDialog>

		<!-- Release filter editor -->
		<SDialog
			v-model="filterEditorOpen"
			:title="filterEditingId === null ? 'Add release filter' : 'Edit release filter'"
		>
			<SField
				label="Field"
				control-id="filter-field"
			>
				<SSelect
					v-model="filterField"
					control-id="filter-field"
					:options="releaseFilterFieldOptions"
				/>
			</SField>
			<SField label="Values">
				<div class="spec-values">
					<span
						v-for="(value, index) in filterValues"
						:key="index"
						class="value-chip"
					>
						{{ value }}
						<button
							type="button"
							:aria-label="`Remove ${value}`"
							@click="filterValues = filterValues.filter((_, i) => i !== index)"
						>
							<Icon
								name="lucide:x"
								aria-hidden="true"
							/>
						</button>
					</span>
					<input
						v-model="filterValueTerm"
						type="text"
						class="value-input"
						placeholder="Type and press enter"
						@keydown.enter.prevent="addFilterValue"
					>
				</div>
			</SField>
			<div class="field-grid">
				<SField
					label="Mode"
					control-id="filter-mode"
				>
					<SSelect
						v-model="filterMode"
						control-id="filter-mode"
						:options="releaseFilterModeOptions"
					/>
				</SField>
				<SField
					v-if="filterMode === 'PREFER'"
					label="Tier"
					hint="Lower tiers win"
					control-id="filter-tier"
				>
					<SInput
						id="filter-tier"
						type="number"
						:model-value="String(filterTier)"
						@update:model-value="filterTier = Number($event) || 0"
					/>
				</SField>
			</div>
			<template #footer>
				<SButton
					variant="secondary"
					@click="filterEditorOpen = false"
				>
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="filterSaving"
					@click="saveFilter"
				>
					Save changes
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="filterDeleteTargetOpen"
			title="Delete release filter"
		>
			<p v-if="filterDeleteTarget">
				Delete this release filter? This cannot be undone.
			</p>
			<template #footer>
				<SButton
					variant="secondary"
					@click="filterDeleteTarget = null"
				>
					Cancel
				</SButton>
				<SButton
					variant="danger"
					@click="deleteFilter"
				>
					Delete
				</SButton>
			</template>
		</SDialog>

		<!-- Quality override editor -->
		<SDialog
			v-model="overrideEditorOpen"
			:title="overrideEditingId === null ? 'Add quality override' : 'Edit quality override'"
		>
			<SField
				label="Release group"
				:error="overrideError"
				control-id="override-group"
			>
				<SInput
					id="override-group"
					v-model="overrideGroup"
					:invalid="!!overrideError"
				/>
			</SField>
			<SField
				label="Assumed source"
				control-id="override-source"
			>
				<SSelect
					v-model="overrideSource"
					control-id="override-source"
					:options="qualitySourceOptions"
				/>
			</SField>
			<template #footer>
				<SButton
					variant="secondary"
					@click="overrideEditorOpen = false"
				>
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="overrideSaving"
					@click="saveOverride"
				>
					Save changes
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="overrideDeleteTargetOpen"
			title="Delete quality override"
		>
			<p v-if="overrideDeleteTarget">
				Delete the override for "{{ overrideDeleteTarget.releaseGroup }}"?
			</p>
			<template #footer>
				<SButton
					variant="secondary"
					@click="overrideDeleteTarget = null"
				>
					Cancel
				</SButton>
				<SButton
					variant="danger"
					@click="deleteOverride"
				>
					Delete
				</SButton>
			</template>
		</SDialog>
	</div>
</template>

<style scoped>
.hint {
	font-size: 0.8125rem;
	color: var(--fg-muted);
	margin-bottom: 12px;
}

.section-actions {
	margin-top: 12px;
}

.spec-list {
	display: flex;
	flex-direction: column;
	gap: 8px;
	margin-bottom: 12px;
}

.spec-row {
	display: grid;
	grid-template-columns: 1fr 1fr 1fr auto auto auto;
	align-items: center;
	gap: 8px;
}

.spec-name {
	min-width: 0;
}

.spec-range {
	display: flex;
	gap: 6px;
}

.spec-values,
.test-matches {
	display: flex;
	flex-wrap: wrap;
	gap: 6px;
}

.spec-values {
	align-items: center;
	min-height: var(--control-h);
	padding: 6px 8px;
	border: 1px solid var(--line-strong);
	border-radius: var(--r-control);
	background: var(--surface);
}

.value-chip {
	display: inline-flex;
	align-items: center;
	gap: 4px;
	height: 24px;
	padding: 0 6px 0 10px;
	border-radius: 999px;
	background: var(--surface-2);
	font-size: 0.8125rem;
}

.value-chip button {
	display: inline-flex;
	align-items: center;
	justify-content: center;
	width: 16px;
	height: 16px;
	border-radius: 999px;
	color: var(--fg-muted);
	cursor: pointer;
}

.value-chip button:hover {
	background: var(--line);
	color: var(--fg);
}

.value-input {
	flex: 1;
	min-width: 140px;
	height: 24px;
	border: none;
	background: transparent;
	color: var(--fg);
	font: inherit;
	font-size: 0.875rem;
}

.value-input:focus {
	outline: none;
}

.field-grid {
	display: grid;
	grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
	gap: 16px;
}
</style>
