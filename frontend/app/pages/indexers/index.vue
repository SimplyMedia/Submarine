<script setup lang="ts">
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useReferenceStore } from '~/stores/reference'
import { useIndexersStore } from '~/stores/indexers'
import {
	cardigannFieldToSchemaField,
	indexerImplementationIcon,
	indexerImplementationLabel,
	indexerImplementationOptions,
	settingsFieldToSchemaField,
} from '~/utils/indexer-labels'
import { protocolLabel, protocolOptions } from '~/utils/settings-labels'
import { formatDateTime, formatRelative } from '~/composables/useFormat'
import { navChildren } from '~/navigation'
import type { IndexerDto, IndexerRequest } from '~/stores/indexers'
import type { SchemaField } from '~/types/schema-form'
import type { components } from '~/types/api'

type IndexerImplementation = components['schemas']['IndexerImplementation']
type Protocol = components['schemas']['Protocol']
type DownloadClientDto = components['schemas']['DownloadClientDto']
type IndexerProxyDto = components['schemas']['IndexerProxyDto']
type IndexerDefinitionInfo = components['schemas']['IndexerDefinitionInfo']
type IndexerTestResult = components['schemas']['IndexerTestResult']
type IndexerHistoryDto = components['schemas']['IndexerHistoryDto']

definePageMeta({ layout: 'default' })
useHead({ title: 'Indexers' })

const api = useApi()
const reference = useReferenceStore()
const store = useIndexersStore()
const { toast } = useToast()

const loading = ref(true)
const downloadClients = ref<DownloadClientDto[]>([])
const proxies = ref<IndexerProxyDto[]>([])
const apiKey = ref('')
const testingAll = ref(false)

const SETTINGS_FIELDS_EXCLUDED = new Set(['baseUrl', 'categories', 'animeCategories', 'minimumSeeders', 'seedCriteria'])

async function loadDownloadClients() {
	const result = await api.GET('/api/v1/download-clients', { params: { query: { PageSize: 200 } } })
	downloadClients.value = result.data?.items ?? []
}

async function loadProxies() {
	const result = await api.GET('/api/v1/indexer-proxies', { params: { query: { PageSize: 200 } } })
	proxies.value = result.data?.items ?? []
}

onMounted(async () => {
	loading.value = true
	await Promise.all([
		store.load(),
		reference.load(),
		loadDownloadClients(),
		loadProxies(),
		api.GET('/api/v1/config/general').then((result) => {
			apiKey.value = result.data?.apiKey ?? ''
		}),
	])
	loading.value = false
})

// --- List ---------------------------------------------------------------------
const columns = [
	{ key: 'select', label: '' },
	{ key: 'name', label: 'Name' },
	{ key: 'implementation', label: 'Type' },
	{ key: 'protocol', label: 'Protocol' },
	{ key: 'modes', label: 'RSS / Auto / Interactive' },
	{ key: 'priority', label: 'Priority', align: 'right' as const },
	{ key: 'status', label: 'Status' },
	{ key: 'tags', label: 'Tags' },
	{ key: 'actions', label: '', align: 'right' as const },
]

function indexerTags(indexer: IndexerDto): string {
	return indexer.tagIds.map(id => reference.tagLabel(id)).join(', ')
}

function toRequest(indexer: IndexerDto, overrides: Partial<IndexerRequest> = {}): IndexerRequest {
	return {
		name: indexer.name,
		implementation: indexer.implementation,
		definitionId: indexer.definitionId,
		protocol: indexer.protocol,
		baseUrl: indexer.baseUrl,
		settings: indexer.settings,
		enableRss: indexer.enableRss,
		enableAutomaticSearch: indexer.enableAutomaticSearch,
		enableInteractiveSearch: indexer.enableInteractiveSearch,
		priority: indexer.priority,
		downloadClientId: indexer.downloadClientId,
		proxyId: indexer.proxyId,
		categories: indexer.categories,
		animeCategories: indexer.animeCategories,
		minimumSeeders: indexer.minimumSeeders,
		seedRatio: indexer.seedRatio,
		seedTimeMinutes: indexer.seedTimeMinutes,
		seasonPackSeedTimeMinutes: indexer.seasonPackSeedTimeMinutes,
		animeStandardFormatSearch: indexer.animeStandardFormatSearch,
		tagIds: indexer.tagIds,
		...overrides,
		vipExpiration: indexer.vipExpiration,
		queryLimit: indexer.queryLimit,
		grabLimit: indexer.grabLimit,
		limitsUnit: indexer.limitsUnit,
		redirect: indexer.redirect,
		requiredFlags: indexer.requiredFlags,
		seasonSearchMaximumSingleEpisodeAge: indexer.seasonSearchMaximumSingleEpisodeAge,
	}
}

const modeBusyId = ref<number | null>(null)

async function toggleMode(indexer: IndexerDto, field: 'enableRss' | 'enableAutomaticSearch' | 'enableInteractiveSearch') {
	modeBusyId.value = indexer.id
	try {
		await store.updateIndexer(indexer.id, toRequest(indexer, { [field]: !indexer[field] }))
	}
	catch (error) {
		toast({ title: 'Could not update indexer', description: toApiError(error).message, tone: 'danger' })
	}
	finally {
		modeBusyId.value = null
	}
}

function disabledReason(indexer: IndexerDto): string | undefined {
	if (!indexer.status.disabledUntil) {
		return undefined
	}
	return indexer.status.mostRecentFailure ? `Failed ${formatRelative(indexer.status.mostRecentFailure)}` : undefined
}

// --- Selection & bulk actions ---------------------------------------------------
const selectedIds = ref<number[]>([])
const allSelected = computed(() => store.indexers.length > 0 && store.indexers.every(indexer => selectedIds.value.includes(indexer.id)))

function toggleRow(id: number, value: boolean) {
	selectedIds.value = value ? [...selectedIds.value, id] : selectedIds.value.filter(existing => existing !== id)
}

function toggleAll(value: boolean) {
	selectedIds.value = value ? store.indexers.map(indexer => indexer.id) : []
}

const bulkBusy = ref(false)

async function bulkSetEnabled(value: boolean) {
	bulkBusy.value = true
	try {
		await store.bulkUpdate({ ids: selectedIds.value, enableRss: value, enableAutomaticSearch: value, enableInteractiveSearch: value, priority: null, proxyId: null, tags: null })
		toast({ title: value ? 'Indexers enabled' : 'Indexers disabled', tone: 'ok' })
	}
	catch (error) {
		toast({ title: 'Bulk update failed', description: toApiError(error).message, tone: 'danger' })
	}
	finally {
		bulkBusy.value = false
	}
}

const bulkPriorityOpen = ref(false)
const bulkPriority = ref(25)

async function applyBulkPriority() {
	bulkBusy.value = true
	try {
		await store.bulkUpdate({ ids: selectedIds.value, priority: bulkPriority.value, enableRss: null, enableAutomaticSearch: null, enableInteractiveSearch: null, proxyId: null, tags: null })
		bulkPriorityOpen.value = false
		toast({ title: 'Priority updated', tone: 'ok' })
	}
	catch (error) {
		toast({ title: 'Bulk update failed', description: toApiError(error).message, tone: 'danger' })
	}
	finally {
		bulkBusy.value = false
	}
}

const bulkTagsOpen = ref(false)
const bulkTagMode = ref<'add' | 'remove' | 'replace'>('add')
const bulkTagIds = ref<number[]>([])

async function applyBulkTags() {
	bulkBusy.value = true
	try {
		await store.bulkUpdate({
			ids: selectedIds.value,
			enableRss: null,
			enableAutomaticSearch: null,
			enableInteractiveSearch: null,
			priority: null,
			proxyId: null,
			tags: { mode: bulkTagMode.value, tagIds: bulkTagIds.value },
		})
		bulkTagsOpen.value = false
		bulkTagIds.value = []
		toast({ title: 'Tags updated', tone: 'ok' })
	}
	catch (error) {
		toast({ title: 'Bulk update failed', description: toApiError(error).message, tone: 'danger' })
	}
	finally {
		bulkBusy.value = false
	}
}

const bulkDeleteOpen = ref(false)

async function applyBulkDelete() {
	bulkBusy.value = true
	try {
		await store.bulkDelete(selectedIds.value)
		selectedIds.value = []
		bulkDeleteOpen.value = false
		toast({ title: 'Indexers deleted', tone: 'ok' })
	}
	catch (error) {
		toast({ title: 'Bulk delete failed', description: toApiError(error).message, tone: 'danger' })
	}
	finally {
		bulkBusy.value = false
	}
}

async function testAll() {
	testingAll.value = true
	try {
		const result = await api.POST('/api/v1/indexers/test-all')
		const results = result.data ?? []
		const failures = results.filter(entry => !entry.isValid).length
		toast({
			title: failures === 0 ? 'All indexers passed' : `${failures} of ${results.length} indexers failed`,
			tone: failures === 0 ? 'ok' : 'danger',
		})
	}
	catch (error) {
		toast({ title: 'Could not test indexers', description: toApiError(error).message, tone: 'danger' })
	}
	finally {
		testingAll.value = false
	}
}

// --- Add/edit dialog --------------------------------------------------------
const dialogOpen = ref(false)
const editingId = ref<number | null>(null)
const implementation = ref<IndexerImplementation | null>(null)
const definitionSearch = ref('')
const selectedDefinition = ref<IndexerDefinitionInfo | null>(null)
const name = ref('')
const baseUrl = ref('')
const protocol = ref<Protocol>('BITTORRENT')
const priority = ref(25)
const enableRss = ref(true)
const enableAutomaticSearch = ref(true)
const enableInteractiveSearch = ref(true)
const categories = ref<number[]>([])
const animeCategories = ref<number[]>([])
const animeStandardFormatSearch = ref(false)
const minimumSeeders = ref(1)
const seedRatio = ref<number | null>(null)
const seedTimeMinutes = ref<number | null>(null)
const seasonPackSeedTimeMinutes = ref<number | null>(null)
const downloadClientId = ref<number | null>(null)
const proxyId = ref<number | null>(null)
const tagIds = ref<number[]>([])
const vipExpiration = ref('')
const queryLimit = ref<number | null>(null)
const grabLimit = ref<number | null>(null)
const limitsUnit = ref<components['schemas']['IndexerLimitsUnit']>('DAY')
const redirect = ref(false)
const requiredFlags = ref<components['schemas']['IndexerFlag'][]>([])
const seasonSearchMaximumSingleEpisodeAge = ref(0)
const settingsDraft = ref<Record<string, unknown>>({})
const nameError = ref('')
const schemaFieldErrors = ref<Record<string, string[]>>({})
const saving = ref(false)
const testing = ref(false)
const testResult = ref<IndexerTestResult | null>(null)

const dialogTitle = computed(() => {
	if (editingId.value !== null) {
		return 'Edit indexer'
	}
	return implementation.value ? `Add ${indexerImplementationLabel(implementation.value)}` : 'Add indexer'
})

const filteredDefinitions = computed(() => {
	const term = definitionSearch.value.trim().toLowerCase()
	const pool = store.schema?.cardigannDefinitions ?? []
	if (term.length === 0) {
		return pool.slice(0, 40)
	}
	return pool.filter(def => def.name.toLowerCase().includes(term) || def.description.toLowerCase().includes(term)).slice(0, 40)
})

const genericFields = computed<SchemaField[]>(() => {
	if (implementation.value === 'CARDIGANN') {
		return (selectedDefinition.value?.settings ?? []).map(field => cardigannFieldToSchemaField(field).field)
	}
	const schema = store.schema?.implementations.find(entry => entry.implementation === implementation.value)
	return (schema?.fields ?? []).filter(field => !SETTINGS_FIELDS_EXCLUDED.has(field.name)).map(settingsFieldToSchemaField)
})

const cardigannOptionLabels = computed<Record<string, Record<string, string>>>(() => {
	const map: Record<string, Record<string, string>> = {}
	for (const field of selectedDefinition.value?.settings ?? []) {
		if (field.options) {
			map[field.name] = field.options
		}
	}
	return map
})

function schemaOptionLabel(field: SchemaField, value: string): string {
	return cardigannOptionLabels.value[field.name]?.[value] ?? value
}

function resetForm() {
	editingId.value = null
	implementation.value = null
	definitionSearch.value = ''
	selectedDefinition.value = null
	name.value = ''
	baseUrl.value = ''
	protocol.value = 'BITTORRENT'
	priority.value = 25
	enableRss.value = true
	enableAutomaticSearch.value = true
	enableInteractiveSearch.value = true
	categories.value = []
	animeCategories.value = []
	animeStandardFormatSearch.value = false
	minimumSeeders.value = 1
	seedRatio.value = null
	seedTimeMinutes.value = null
	seasonPackSeedTimeMinutes.value = null
	downloadClientId.value = null
	proxyId.value = null
	tagIds.value = []
	settingsDraft.value = {}
	nameError.value = ''
	vipExpiration.value = ''
	queryLimit.value = null
	grabLimit.value = null
	limitsUnit.value = 'DAY'
	redirect.value = false
	requiredFlags.value = []
	seasonSearchMaximumSingleEpisodeAge.value = 0
	schemaFieldErrors.value = {}
	testResult.value = null
}

function openCreate() {
	resetForm()
	dialogOpen.value = true
}

function pickImplementation(value: IndexerImplementation) {
	implementation.value = value
	protocol.value = value === 'NEWZNAB' ? 'USENET' : 'BITTORRENT'
	settingsDraft.value = {}
}

function pickDefinition(definition: IndexerDefinitionInfo) {
	selectedDefinition.value = definition
	name.value = name.value || definition.name
	protocol.value = definition.protocol.toUpperCase() === 'USENET' ? 'USENET' : 'BITTORRENT'
	categories.value = [...definition.categories]
	settingsDraft.value = {}
}

function openEdit(indexer: IndexerDto) {
	resetForm()
	editingId.value = indexer.id
	implementation.value = indexer.implementation
	name.value = indexer.name
	baseUrl.value = indexer.baseUrl
	protocol.value = indexer.protocol
	priority.value = indexer.priority
	enableRss.value = indexer.enableRss
	enableAutomaticSearch.value = indexer.enableAutomaticSearch
	enableInteractiveSearch.value = indexer.enableInteractiveSearch
	categories.value = [...indexer.categories]
	animeCategories.value = [...indexer.animeCategories]
	animeStandardFormatSearch.value = indexer.animeStandardFormatSearch
	minimumSeeders.value = indexer.minimumSeeders ?? 1
	seedRatio.value = indexer.seedRatio
	seedTimeMinutes.value = indexer.seedTimeMinutes
	seasonPackSeedTimeMinutes.value = indexer.seasonPackSeedTimeMinutes
	downloadClientId.value = indexer.downloadClientId
	proxyId.value = indexer.proxyId
	tagIds.value = [...indexer.tagIds]
	settingsDraft.value = { ...(indexer.settings as Record<string, unknown>) }
	vipExpiration.value = indexer.vipExpiration ?? ''
	queryLimit.value = indexer.queryLimit
	grabLimit.value = indexer.grabLimit
	limitsUnit.value = indexer.limitsUnit
	redirect.value = indexer.redirect
	requiredFlags.value = [...indexer.requiredFlags]
	seasonSearchMaximumSingleEpisodeAge.value = indexer.seasonSearchMaximumSingleEpisodeAge
	if (indexer.implementation === 'CARDIGANN' && indexer.definitionId) {
		selectedDefinition.value = store.schema?.cardigannDefinitions.find(def => def.id === indexer.definitionId) ?? null
	}
	dialogOpen.value = true
}

function buildSettings(): Record<string, unknown> {
	if (implementation.value === 'CARDIGANN') {
		return {
			definitionId: selectedDefinition.value?.id ?? '',
			baseUrl: baseUrl.value || null,
			fields: settingsDraft.value,
		}
	}
	const hasSeedCriteria = seedRatio.value != null || seedTimeMinutes.value != null || seasonPackSeedTimeMinutes.value != null
	return {
		baseUrl: baseUrl.value,
		apiPath: (settingsDraft.value.apiPath as string) || '/api',
		apiKey: settingsDraft.value.apiKey ?? null,
		categories: categories.value.length > 0 ? categories.value : null,
		animeCategories: animeCategories.value.length > 0 ? animeCategories.value : null,
		additionalParameters: settingsDraft.value.additionalParameters ?? null,
		minimumSeeders: minimumSeeders.value,
		seedCriteria: hasSeedCriteria
			? { seedRatio: seedRatio.value, seedTimeMinutes: seedTimeMinutes.value, seasonPackSeedTimeMinutes: seasonPackSeedTimeMinutes.value }
			: null,
	}
}

function buildRequest(): IndexerRequest {
	return {
		name: name.value,
		implementation: implementation.value!,
		definitionId: implementation.value === 'CARDIGANN' ? (selectedDefinition.value?.id ?? null) : null,
		protocol: protocol.value,
		baseUrl: implementation.value === 'CARDIGANN' ? (baseUrl.value || selectedDefinition.value?.links[0] || '') : baseUrl.value,
		settings: buildSettings(),
		enableRss: enableRss.value,
		enableAutomaticSearch: enableAutomaticSearch.value,
		enableInteractiveSearch: enableInteractiveSearch.value,
		priority: priority.value,
		downloadClientId: downloadClientId.value,
		proxyId: proxyId.value,
		categories: categories.value,
		animeCategories: animeCategories.value,
		minimumSeeders: minimumSeeders.value,
		seedRatio: seedRatio.value,
		seedTimeMinutes: seedTimeMinutes.value,
		seasonPackSeedTimeMinutes: seasonPackSeedTimeMinutes.value,
		animeStandardFormatSearch: animeStandardFormatSearch.value,
		tagIds: tagIds.value,
		vipExpiration: vipExpiration.value || null,
		queryLimit: queryLimit.value,
		grabLimit: grabLimit.value,
		limitsUnit: limitsUnit.value,
		redirect: redirect.value,
		requiredFlags: protocol.value === 'BITTORRENT' ? requiredFlags.value : [],
		seasonSearchMaximumSingleEpisodeAge: seasonSearchMaximumSingleEpisodeAge.value,
	}
}

async function save() {
	if (!implementation.value) {
		return
	}
	nameError.value = ''
	schemaFieldErrors.value = {}
	saving.value = true
	try {
		if (editingId.value === null) {
			await store.createIndexer(buildRequest())
			toast({ title: 'Indexer added', tone: 'ok' })
		}
		else {
			await store.updateIndexer(editingId.value, buildRequest())
			toast({ title: 'Indexer saved', tone: 'ok' })
		}
		dialogOpen.value = false
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		let matched = false
		for (const [key, messages] of Object.entries(apiError.fieldErrors)) {
			if (key === 'name' || key === 'Name') {
				nameError.value = messages[0] ?? ''
				matched = true
			}
		}
		if (!matched) {
			toast({ title: 'Could not save indexer', description: apiError.message, tone: 'danger' })
		}
	}
	finally {
		saving.value = false
	}
}

async function testConnection() {
	if (!implementation.value) {
		return
	}
	testing.value = true
	testResult.value = null
	schemaFieldErrors.value = {}
	try {
		const result = await api.POST('/api/v1/indexers/test', {
			body: {
				implementation: implementation.value,
				definitionId: implementation.value === 'CARDIGANN' ? (selectedDefinition.value?.id ?? null) : null,
				settings: buildSettings(),
			},
		})
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		testResult.value = result.data
		if (Object.keys(result.data.fieldErrors).length > 0) {
			schemaFieldErrors.value = result.data.fieldErrors
		}
		toast({ title: result.data.isValid ? 'Connection successful' : 'Connection failed', description: result.data.message ?? undefined, tone: result.data.isValid ? 'ok' : 'danger' })
	}
	catch (error) {
		toast({ title: 'Connection failed', description: toApiError(error).message, tone: 'danger' })
	}
	finally {
		testing.value = false
	}
}

// --- Delete -------------------------------------------------------------------
const deleteTarget = ref<IndexerDto | null>(null)
const deleting = ref(false)

const deleteTargetOpen = computed({
	get: () => deleteTarget.value !== null,
	set: (value: boolean) => {
		if (!value) {
			deleteTarget.value = null
		}
	},
})

async function doDelete() {
	if (!deleteTarget.value) {
		return
	}
	deleting.value = true
	try {
		await store.deleteIndexer(deleteTarget.value.id)
		toast({ title: 'Indexer deleted', tone: 'ok' })
		deleteTarget.value = null
	}
	catch (error) {
		toast({ title: 'Could not delete indexer', description: toApiError(error).message, tone: 'danger' })
	}
	finally {
		deleting.value = false
	}
}

// --- Capabilities & history dialogs --------------------------------------------
const capabilitiesTarget = ref<IndexerDto | null>(null)
const capabilities = ref<components['schemas']['IndexerCapabilitiesSummary'] | null>(null)
const capabilitiesLoading = ref(false)

const capabilitiesTargetOpen = computed({
	get: () => capabilitiesTarget.value !== null,
	set: (value: boolean) => {
		if (!value) {
			capabilitiesTarget.value = null
		}
	},
})

async function openCapabilities(indexer: IndexerDto) {
	capabilitiesTarget.value = indexer
	capabilitiesLoading.value = true
	const result = await api.GET('/api/v1/indexers/{id}/capabilities', { params: { path: { id: indexer.id } } })
	capabilities.value = result.data ?? null
	capabilitiesLoading.value = false
}

const historyTarget = ref<IndexerDto | null>(null)
const historyItems = ref<IndexerHistoryDto[]>([])
const historyLoading = ref(false)

const historyTargetOpen = computed({
	get: () => historyTarget.value !== null,
	set: (value: boolean) => {
		if (!value) {
			historyTarget.value = null
		}
	},
})
const historyColumns = [
	{ key: 'eventType', label: 'Event' },
	{ key: 'query', label: 'Query' },
	{ key: 'source', label: 'Source' },
	{ key: 'elapsedMs', label: 'Elapsed', align: 'right' as const },
	{ key: 'date', label: 'Date' },
]
const historyEventType = ref<'ALL' | components['schemas']['IndexerHistoryEventType']>('ALL')
const historySuccessful = ref<'ALL' | 'true' | 'false'>('ALL')

async function openHistory(indexer: IndexerDto) {
	historyTarget.value = indexer
	historyLoading.value = true
	const result = await api.GET('/api/v1/indexers/{id}/history', {
		params: {
			path: { id: indexer.id },
			query: {
				PageSize: 100,
				eventType: historyEventType.value === 'ALL' ? undefined : historyEventType.value,
				successful: historySuccessful.value === 'ALL' ? undefined : historySuccessful.value === 'true',
			},
		},
	})
	historyItems.value = result.data?.items ?? []
	historyLoading.value = false
}

// --- Outbound Newznab panel ------------------------------------------------
const origin = computed(() => (import.meta.client ? window.location.origin : ''))
const aggregateUrl = computed(() => `${origin.value}/api/v1/indexers/newznab/api?apikey=${apiKey.value}`)

function indexerUrl(indexer: IndexerDto): string {
	return `${origin.value}/api/v1/indexer/${indexer.id}/newznab/api?apikey=${apiKey.value}`
}

async function copyToClipboard(value: string) {
	await navigator.clipboard.writeText(value)
	toast({ title: 'Copied to clipboard', tone: 'ok' })
}
</script>

<template>
	<div>
		<SPageHeader title="Indexers">
			<template #actions>
				<SButton
					variant="secondary"
					:loading="testingAll"
					@click="testAll"
				>
					Test all
				</SButton>
				<SButton
					variant="primary"
					@click="openCreate"
				>
					Add indexer
				</SButton>
			</template>
		</SPageHeader>

		<SubNav
			label="Indexers"
			:items="navChildren('indexers')"
		/>

		<SEmptyState
			v-if="store.loadError"
			:message="store.loadError"
			icon="lucide:alert-triangle"
		>
			<template #action>
				<SButton @click="store.load(true)">
					Retry
				</SButton>
			</template>
		</SEmptyState>
		<SSpinner v-else-if="loading" />
		<template v-else>
			<div
				v-if="selectedIds.length > 0"
				class="indexers-bulk-toolbar"
			>
				<span>{{ selectedIds.length }} selected</span>
				<SButton
					size="sm"
					variant="secondary"
					:loading="bulkBusy"
					@click="bulkSetEnabled(true)"
				>
					Enable
				</SButton>
				<SButton
					size="sm"
					variant="secondary"
					:loading="bulkBusy"
					@click="bulkSetEnabled(false)"
				>
					Disable
				</SButton>
				<SButton
					size="sm"
					variant="secondary"
					@click="bulkPriorityOpen = true"
				>
					Set priority
				</SButton>
				<SButton
					size="sm"
					variant="secondary"
					@click="bulkTagsOpen = true"
				>
					Tags
				</SButton>
				<SButton
					size="sm"
					variant="danger"
					@click="bulkDeleteOpen = true"
				>
					Delete
				</SButton>
			</div>

			<SCheckbox
				v-if="store.indexers.length > 0"
				:model-value="allSelected"
				label="Select all"
				class="indexers-select-all"
				@update:model-value="toggleAll"
			/>

			<STable
				:columns="columns"
				:rows="store.indexers"
				:row-key="(row) => row.id"
			>
				<template #empty>
					<SEmptyState message="Add an indexer to start searching for releases.">
						<template #action>
							<SButton
								variant="primary"
								@click="openCreate"
							>
								Add indexer
							</SButton>
						</template>
					</SEmptyState>
				</template>
				<template #cell-select="{ row }">
					<SCheckbox
						:model-value="selectedIds.includes(row.id)"
						:aria-label="`Select ${row.name}`"
						@update:model-value="value => toggleRow(row.id, value)"
					/>
				</template>
				<template #cell-name="{ row }">
					{{ row.name }}
				</template>
				<template #cell-implementation="{ row }">
					{{ indexerImplementationLabel(row.implementation) }}
				</template>
				<template #cell-protocol="{ row }">
					<SBadge tone="neutral">
						{{ protocolLabel(row.protocol) }}
					</SBadge>
				</template>
				<template #cell-modes="{ row }">
					<div class="indexers-modes-cell">
						<STooltip text="RSS sync">
							<SSwitch
								:model-value="row.enableRss"
								:disabled="modeBusyId === row.id"
								@update:model-value="toggleMode(row, 'enableRss')"
							/>
						</STooltip>
						<STooltip text="Automatic search">
							<SSwitch
								:model-value="row.enableAutomaticSearch"
								:disabled="modeBusyId === row.id"
								@update:model-value="toggleMode(row, 'enableAutomaticSearch')"
							/>
						</STooltip>
						<STooltip text="Interactive search">
							<SSwitch
								:model-value="row.enableInteractiveSearch"
								:disabled="modeBusyId === row.id"
								@update:model-value="toggleMode(row, 'enableInteractiveSearch')"
							/>
						</STooltip>
					</div>
				</template>
				<template #cell-tags="{ row }">
					<span v-if="indexerTags(row)">{{ indexerTags(row) }}</span>
					<span
						v-else
						class="s-cell-muted"
					>None</span>
				</template>
				<template #cell-status="{ row }">
					<STooltip :text="disabledReason(row)">
						<SBadge :tone="row.status.disabledUntil ? 'danger' : 'ok'">
							{{ row.status.disabledUntil ? `Disabled until ${formatDateTime(row.status.disabledUntil)}` : 'Enabled' }}
						</SBadge>
					</STooltip>
				</template>
				<template #cell-actions="{ row }">
					<SDropdownMenu
						:items="[
							{ label: 'Edit', icon: 'lucide:pencil', onSelect: () => openEdit(row) },
							{ label: 'Capabilities', icon: 'lucide:list-checks', onSelect: () => openCapabilities(row) },
							{ label: 'History', icon: 'lucide:history', onSelect: () => openHistory(row) },
							{ label: 'Copy Newznab URL', icon: 'lucide:copy', onSelect: () => copyToClipboard(indexerUrl(row)) },
							{ label: 'Delete', icon: 'lucide:trash-2', danger: true, onSelect: () => (deleteTarget = row) },
						]"
					>
						<template #trigger>
							<SIconButton label="Indexer actions">
								<Icon
									name="lucide:more-horizontal"
									aria-hidden="true"
								/>
							</SIconButton>
						</template>
					</SDropdownMenu>
				</template>
			</STable>

			<SSection title="Use Submarine as an indexer in other apps">
				<p class="indexers-outbound-hint">
					Point Sonarr, Radarr or another Prowlarr-compatible app at this Newznab URL to search every enabled indexer through Submarine.
				</p>
				<div class="indexers-outbound-row">
					<code>{{ aggregateUrl }}</code>
					<SIconButton
						label="Copy aggregate Newznab URL"
						@click="copyToClipboard(aggregateUrl)"
					>
						<Icon
							name="lucide:copy"
							aria-hidden="true"
						/>
					</SIconButton>
				</div>
			</SSection>
		</template>

		<!-- Add/edit dialog -->
		<SDialog
			v-model="dialogOpen"
			:title="dialogTitle"
			wide
		>
			<div v-if="!implementation">
				<div class="provider-grid">
					<ProviderCard
						v-for="option in indexerImplementationOptions"
						:key="option.value"
						:icon="indexerImplementationIcon(option.value as IndexerImplementation)"
						:label="option.label"
						@click="pickImplementation(option.value as IndexerImplementation)"
					/>
				</div>
			</div>
			<div v-else-if="implementation === 'CARDIGANN' && !selectedDefinition">
				<SInput
					v-model="definitionSearch"
					type="search"
					placeholder="Search Cardigann definitions"
				/>
				<ul class="definition-list">
					<li
						v-for="definition in filteredDefinitions"
						:key="definition.id"
						class="definition-row"
						@click="pickDefinition(definition)"
					>
						<div class="definition-row-head">
							<span class="definition-name">{{ definition.name }}</span>
							<SBadge :tone="definition.type === 'public' ? 'ok' : 'neutral'">
								{{ definition.type }}
							</SBadge>
							<SBadge tone="neutral">
								{{ definition.language }}
							</SBadge>
						</div>
						<p class="definition-description">
							{{ definition.description }}
						</p>
					</li>
				</ul>
				<SEmptyState
					v-if="filteredDefinitions.length === 0"
					message="No definitions match your search."
				/>
			</div>
			<div v-else>
				<SField
					label="Name"
					:error="nameError"
					control-id="idx-name"
				>
					<SInput
						id="idx-name"
						v-model="name"
						:invalid="!!nameError"
					/>
				</SField>

				<SField
					v-if="implementation !== 'CARDIGANN'"
					label="Base URL"
					control-id="idx-base-url"
				>
					<SInput
						id="idx-base-url"
						v-model="baseUrl"
						type="url"
						placeholder="https://example.com"
					/>
				</SField>
				<SField
					v-else
					label="Base URL override"
					hint="Leave blank to use the definition's default"
					control-id="idx-base-url"
				>
					<SInput
						id="idx-base-url"
						v-model="baseUrl"
						type="url"
						:placeholder="selectedDefinition?.links[0]"
					/>
				</SField>

				<div class="field-grid">
					<SField
						label="Protocol"
						control-id="idx-protocol"
					>
						<SSelect
							control-id="idx-protocol"
							:model-value="protocol"
							:options="protocolOptions"
							@update:model-value="protocol = $event as Protocol"
						/>
					</SField>
					<SField
						label="Priority"
						hint="1-50, lower is tried first"
						control-id="idx-priority"
					>
						<SInput
							id="idx-priority"
							type="number"
							:model-value="String(priority)"
							@update:model-value="priority = Number($event) || 1"
						/>
					</SField>
				</div>

				<SchemaForm
					v-if="genericFields.length > 0"
					v-model="settingsDraft"
					:fields="genericFields"
					:field-errors="schemaFieldErrors"
					:option-label="schemaOptionLabel"
				/>

				<div class="field-grid">
					<SField label="Categories">
						<IndexerCategoryPicker
							v-model="categories"
							:categories="store.categories"
						/>
					</SField>
					<SField
						label="VIP expiration"
						hint="Optional date when indexer access expires"
						control-id="idx-vip-expiration"
					>
						<input
							id="idx-vip-expiration"
							v-model="vipExpiration"
							class="s-input"
							type="date"
						>
					</SField>
					<SField
						label="Season search maximum single episode age (days)"
						hint="Optional maximum age for searching individual season episodes"
						control-id="idx-season-search-age"
					>
						<SInput
							id="idx-season-search-age"
							type="number"
							:model-value="String(seasonSearchMaximumSingleEpisodeAge)"
							@update:model-value="seasonSearchMaximumSingleEpisodeAge = $event === '' ? 0 : Number($event)"
						/>
					</SField>
				</div>
				<div class="field-grid">
					<SField
						label="Query limit"
						control-id="idx-query-limit"
					>
						<SInput
							id="idx-query-limit"
							type="number"
							:model-value="queryLimit == null ? '' : String(queryLimit)"
							@update:model-value="queryLimit = $event === '' ? null : Number($event)"
						/>
					</SField>
					<SField
						label="Grab limit"
						control-id="idx-grab-limit"
					>
						<SInput
							id="idx-grab-limit"
							type="number"
							:model-value="grabLimit == null ? '' : String(grabLimit)"
							@update:model-value="grabLimit = $event === '' ? null : Number($event)"
						/>
					</SField>
					<SField
						label="Limits unit"
						control-id="idx-limits-unit"
					>
						<SSelect
							control-id="idx-limits-unit"
							:model-value="limitsUnit"
							:options="[{ value: 'DAY', label: 'Per day' }, { value: 'HOUR', label: 'Per hour' }]"
							@update:model-value="limitsUnit = $event as components['schemas']['IndexerLimitsUnit']"
						/>
					</SField>
				</div>
				<SSwitch
					v-model="redirect"
					label="Redirect download requests"
				/>
				<p
					v-if="protocol === 'USENET'"
					class="s-field-hint"
				>
					Usenet indexers require redirect mode.
				</p>
				<div
					v-if="protocol === 'BITTORRENT'"
					class="field-grid-stack"
				>
					<span class="s-field-label">Required release flags</span>
					<SCheckbox
						v-for="flag in (['FREELEECH', 'HALFLEECH', 'DOUBLE_UPLOAD', 'INTERNAL', 'SCENE', 'EXCLUSIVE', 'G_FREELEECH'] as const)"
						:key="flag"
						:model-value="requiredFlags.includes(flag)"
						:label="flag.replaceAll('_', ' ').toLowerCase()"
						@update:model-value="requiredFlags = $event ? [...requiredFlags, flag] : requiredFlags.filter(value => value !== flag)"
					/>
				</div>
				<div class="field-grid">
					<SField label="Anime categories">
						<IndexerCategoryPicker
							v-model="animeCategories"
							:categories="store.categories"
						/>
					</SField>
					<div class="field-grid-stack">
						<SSwitch
							v-model="animeStandardFormatSearch"
							label="Anime uses standard episode numbering"
						/>
					</div>
				</div>
				<div
					v-if="protocol === 'BITTORRENT'"
					class="field-grid"
				>
					<SField
						label="Minimum seeders"
						control-id="idx-min-seeders"
					>
						<SInput
							id="idx-min-seeders"
							type="number"
							:model-value="String(minimumSeeders)"
							@update:model-value="minimumSeeders = Number($event) || 0"
						/>
					</SField>
					<SField
						label="Seed ratio"
						hint="Optional"
						control-id="idx-seed-ratio"
					>
						<SInput
							id="idx-seed-ratio"
							type="number"
							:model-value="seedRatio == null ? '' : String(seedRatio)"
							@update:model-value="seedRatio = $event === '' ? null : Number($event)"
						/>
					</SField>
					<SField
						label="Seed time (minutes)"
						hint="Optional"
						control-id="idx-seed-time"
					>
						<SInput
							id="idx-seed-time"
							type="number"
							:model-value="seedTimeMinutes == null ? '' : String(seedTimeMinutes)"
							@update:model-value="seedTimeMinutes = $event === '' ? null : Number($event)"
						/>
					</SField>
					<SField
						label="Season pack seed time (minutes)"
						hint="Optional"
						control-id="idx-season-seed-time"
					>
						<SInput
							id="idx-season-seed-time"
							type="number"
							:model-value="seasonPackSeedTimeMinutes == null ? '' : String(seasonPackSeedTimeMinutes)"
							@update:model-value="seasonPackSeedTimeMinutes = $event === '' ? null : Number($event)"
						/>
					</SField>
				</div>

				<div class="field-grid">
					<SField label="Download client override">
						<SSelect
							:model-value="downloadClientId == null ? 'none' : String(downloadClientId)"
							:options="[{ value: 'none', label: 'Use default' }, ...downloadClients.map(c => ({ value: String(c.id), label: c.name }))]"
							@update:model-value="downloadClientId = $event === 'none' ? null : Number($event)"
						/>
					</SField>
					<SField label="Proxy">
						<SSelect
							:model-value="proxyId == null ? 'none' : String(proxyId)"
							:options="[{ value: 'none', label: 'No proxy' }, ...proxies.map(p => ({ value: String(p.id), label: p.name }))]"
							@update:model-value="proxyId = $event === 'none' ? null : Number($event)"
						/>
					</SField>
				</div>

				<SField label="Tags">
					<TagPicker v-model:tag-ids="tagIds" />
				</SField>

				<div class="field-grid">
					<SSwitch
						v-model="enableRss"
						label="Enable RSS sync"
					/>
					<SSwitch
						v-model="enableAutomaticSearch"
						label="Enable automatic search"
					/>
					<SSwitch
						v-model="enableInteractiveSearch"
						label="Enable interactive search"
					/>
				</div>

				<div
					v-if="testResult?.capabilities"
					class="indexers-test-result"
				>
					<SBadge tone="ok">
						{{ testResult.capabilities.categoryCount }} categories
					</SBadge>
					<SBadge :tone="testResult.capabilities.searchAvailable ? 'ok' : 'neutral'">
						Search
					</SBadge>
					<SBadge :tone="testResult.capabilities.tvSearchAvailable ? 'ok' : 'neutral'">
						TV search
					</SBadge>
					<SBadge :tone="testResult.capabilities.movieSearchAvailable ? 'ok' : 'neutral'">
						Movie search
					</SBadge>
				</div>
			</div>

			<template #footer>
				<SButton
					variant="secondary"
					@click="dialogOpen = false"
				>
					Cancel
				</SButton>
				<SButton
					v-if="implementation && (implementation !== 'CARDIGANN' || selectedDefinition)"
					variant="secondary"
					:loading="testing"
					@click="testConnection"
				>
					Test
				</SButton>
				<SButton
					v-if="implementation && (implementation !== 'CARDIGANN' || selectedDefinition)"
					variant="primary"
					:loading="saving"
					@click="save"
				>
					{{ editingId === null ? 'Add indexer' : 'Save changes' }}
				</SButton>
			</template>
		</SDialog>

		<!-- Delete confirm -->
		<SDialog
			v-model="deleteTargetOpen"
			title="Delete indexer"
		>
			<p v-if="deleteTarget">
				Delete "{{ deleteTarget.name }}"? This cannot be undone.
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
					Delete indexer
				</SButton>
			</template>
		</SDialog>

		<!-- Bulk priority -->
		<SDialog
			v-model="bulkPriorityOpen"
			title="Set priority"
		>
			<SField
				label="Priority"
				hint="1-50, lower is tried first"
				control-id="bulk-priority"
			>
				<SInput
					id="bulk-priority"
					type="number"
					:model-value="String(bulkPriority)"
					@update:model-value="bulkPriority = Number($event) || 1"
				/>
			</SField>
			<template #footer>
				<SButton
					variant="secondary"
					@click="bulkPriorityOpen = false"
				>
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="bulkBusy"
					@click="applyBulkPriority"
				>
					Apply
				</SButton>
			</template>
		</SDialog>

		<!-- Bulk tags -->
		<SDialog
			v-model="bulkTagsOpen"
			title="Manage tags"
		>
			<SField label="Mode">
				<SSelect
					:model-value="bulkTagMode"
					:options="[{ value: 'add', label: 'Add' }, { value: 'remove', label: 'Remove' }, { value: 'replace', label: 'Replace' }]"
					@update:model-value="bulkTagMode = $event as 'add' | 'remove' | 'replace'"
				/>
			</SField>
			<SField label="Tags">
				<TagPicker v-model:tag-ids="bulkTagIds" />
			</SField>
			<template #footer>
				<SButton
					variant="secondary"
					@click="bulkTagsOpen = false"
				>
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="bulkBusy"
					@click="applyBulkTags"
				>
					Apply
				</SButton>
			</template>
		</SDialog>

		<!-- Bulk delete -->
		<SDialog
			v-model="bulkDeleteOpen"
			title="Delete selected indexers"
		>
			<p>Delete {{ selectedIds.length }} indexer(s)? This cannot be undone.</p>
			<template #footer>
				<SButton
					variant="secondary"
					@click="bulkDeleteOpen = false"
				>
					Cancel
				</SButton>
				<SButton
					variant="danger"
					:loading="bulkBusy"
					@click="applyBulkDelete"
				>
					Delete
				</SButton>
			</template>
		</SDialog>

		<!-- Capabilities -->
		<SDialog
			v-model="capabilitiesTargetOpen"
			title="Capabilities"
			:description="capabilitiesTarget?.name"
		>
			<SSpinner v-if="capabilitiesLoading" />
			<div
				v-else-if="capabilities"
				class="indexers-test-result"
			>
				<SBadge tone="ok">
					{{ capabilities.categoryCount }} categories
				</SBadge>
				<SBadge :tone="capabilities.searchAvailable ? 'ok' : 'neutral'">
					Search
				</SBadge>
				<SBadge :tone="capabilities.tvSearchAvailable ? 'ok' : 'neutral'">
					TV search
				</SBadge>
				<SBadge :tone="capabilities.movieSearchAvailable ? 'ok' : 'neutral'">
					Movie search
				</SBadge>
			</div>
			<SEmptyState
				v-else
				message="Capabilities have not been fetched yet. Run a test to load them."
			/>
		</SDialog>

		<!-- Per-indexer history -->
		<SDialog
			v-model="historyTargetOpen"
			title="Indexer history"
			:description="historyTarget?.name"
			wide
		>
			<SSpinner v-if="historyLoading" />
			<template v-else>
				<div class="history-filters">
					<SField label="Event type">
						<SSelect
							:model-value="historyEventType"
							:options="[
								{ value: 'ALL', label: 'All events' },
								{ value: 'QUERY', label: 'Query' },
								{ value: 'RSS', label: 'RSS' },
								{ value: 'GRAB', label: 'Grab' },
								{ value: 'AUTH', label: 'Auth' },
								{ value: 'FAILED', label: 'Failed' },
							]"
							@update:model-value="historyEventType = $event as typeof historyEventType; openHistory(historyTarget!)"
						/>
					</SField>
					<SField label="Result">
						<SSelect
							:model-value="historySuccessful"
							:options="[
								{ value: 'ALL', label: 'All results' },
								{ value: 'true', label: 'Successful' },
								{ value: 'false', label: 'Failed' },
							]"
							@update:model-value="historySuccessful = $event as typeof historySuccessful; openHistory(historyTarget!)"
						/>
					</SField>
				</div>
				<STable
					:columns="historyColumns"
					:rows="historyItems"
					:row-key="(row) => row.id"
				>
					<template #cell-eventType="{ row }">
						<SBadge :tone="row.successful ? 'ok' : 'danger'">
							{{ row.eventType }}
						</SBadge>
					</template>
					<template #cell-query="{ row }">
						<span v-if="row.query">{{ row.query }}</span>
						<span
							v-else
							class="s-cell-muted"
						>None</span>
					</template>
					<template #cell-source="{ row }">
						<span v-if="row.source">{{ row.source }}</span>
						<span
							v-else
							class="s-cell-muted"
						>None</span>
					</template>
					<template #cell-elapsedMs="{ row }">
						<span v-if="row.elapsedMs != null">{{ row.elapsedMs }} ms</span>
						<span
							v-else
							class="s-cell-muted"
						>None</span>
					</template>
					<template #cell-date="{ row }">
						{{ formatDateTime(row.date) }}
					</template>
				</STable>
			</template>
		</SDialog>
	</div>
</template>

<style scoped>
.indexers-bulk-toolbar {
	display: flex;
	align-items: center;
	gap: 8px;
	margin-bottom: 12px;
	padding: 8px 12px;
	background: var(--surface-2);
	border-radius: var(--r-control);
}

.indexers-select-all {
	margin-bottom: 12px;
}

.indexers-modes-cell {
	display: flex;
	align-items: center;
	gap: 12px;
}

.field-grid {
	display: grid;
	grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
	gap: 16px;
	margin-bottom: 16px;
}

.field-grid-stack {
	display: flex;
	align-items: flex-end;
}

.provider-grid {
	display: grid;
	grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
	gap: 12px;
}

.definition-list {
	display: flex;
	flex-direction: column;
	gap: 4px;
	margin-top: 12px;
	max-height: 360px;
	overflow-y: auto;
}

.definition-row {
	padding: 10px 12px;
	border: 1px solid var(--line);
	border-radius: var(--r-control);
	cursor: pointer;
}

.definition-row:hover {
	background: var(--surface-2);
}

.definition-row-head {
	display: flex;
	align-items: center;
	gap: 8px;
}

.definition-name {
	font-weight: 500;
}

.definition-description {
	margin-top: 4px;
	font-size: 0.8125rem;
	color: var(--fg-muted);
}

.indexers-test-result {
	display: flex;
	gap: 8px;
	flex-wrap: wrap;
}

.indexers-outbound-hint {
	font-size: 0.8125rem;
	color: var(--fg-muted);
	margin-bottom: 12px;
}

.indexers-outbound-row {
	display: flex;
	align-items: center;
	gap: 8px;
}

.indexers-outbound-row code {
	flex: 1;
	padding: 8px 12px;
	background: var(--surface-2);
	border-radius: var(--r-control);
	font-size: 0.8125rem;
	overflow-x: auto;
	white-space: nowrap;
}

.history-filters {
	display: grid;
	grid-template-columns: repeat(auto-fit, minmax(180px, 240px));
	gap: 12px;
	margin-bottom: 16px;
}
</style>
