<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useDirtyForm } from '~/composables/useDirtyForm'
import { useReferenceStore } from '~/stores/reference'
import { useSettingsStore } from '~/stores/settings'
import {
	downloadClientTypeIcon,
	downloadClientTypeLabel,
	downloadClientTypeOptions,
	humanizeEnumValue,
	humanizeFieldName,
} from '~/utils/settings-labels'
import type { SchemaField } from '~/types/schema-form'
import type { components } from '~/types/api'

const { t } = useI18n()

type DownloadConfigResource = components['schemas']['DownloadConfigResource']
type DownloadClientDto = components['schemas']['DownloadClientDto']
type DownloadClientType = components['schemas']['DownloadClientType']
type DownloadClientFieldSchema = components['schemas']['DownloadClientFieldSchema']

definePageMeta({ layout: 'default' })
useHead({ title: t('pages.settings.downloadClients.title', 'Download clients') })

const api = useApi()
const reference = useReferenceStore()
const settings = useSettingsStore()
const { toast } = useToast()

const loading = ref(true)

// --- Download handling config (sticky save bar) -----------------------------
const configDraft = ref<DownloadConfigResource | null>(null)
const configDirty = useDirtyForm(configDraft)
const configSaving = ref(false)

async function saveConfig() {
	if (!configDraft.value) {
		return
	}
	configSaving.value = true
	const result = await api.PUT('/api/v1/config/download', { body: configDraft.value })
	configSaving.value = false
	if (!result.data) {
		toast({ title: t('pages.settings.downloadClients.saveError', 'Could not save'), description: toApiError(result.error, result.response).message, tone: 'danger' })
		return
	}
	configDirty.markSaved(result.data)
	toast({ title: t('pages.settings.downloadClients.saved', 'Saved'), tone: 'ok' })
}

// --- Clients list ------------------------------------------------------------
const clients = ref<DownloadClientDto[]>([])
const clientsLoading = ref(true)
const clientsLoadError = ref('')

async function loadClients() {
	clientsLoading.value = true
	clientsLoadError.value = ''
	const result = await api.GET('/api/v1/download-clients', { params: { query: { PageSize: 200 } } })
	if (!result.data) {
		clientsLoadError.value = t('pages.settings.downloadClients.loadError', 'Could not load download clients. Check your connection and try again.')
	}
	clients.value = result.data?.items ?? []
	clientsLoading.value = false
}

const columns = [
	{ key: 'name', label: t('pages.settings.downloadClients.name', 'Name') },
	{ key: 'type', label: t('pages.settings.downloadClients.type', 'Type') },
	{ key: 'enable', label: t('pages.settings.downloadClients.enable', 'Enable') },
	{ key: 'priority', label: t('pages.settings.downloadClients.priority', 'Priority'), align: 'right' as const },
	{ key: 'tags', label: t('pages.settings.downloadClients.tags', 'Tags') },
	{ key: 'actions', label: '', align: 'right' as const },
]

function clientTags(client: DownloadClientDto): string {
	return client.tagIds.map(id => reference.tagLabel(id)).join(', ')
}

// --- Add/edit dialog ---------------------------------------------------------
const dialogOpen = ref(false)
const editingId = ref<number | null>(null)
const selectedType = ref<DownloadClientType | null>(null)
const clientName = ref('')
const clientEnable = ref(true)
const clientPriority = ref(25)
const settingsDraft = ref<Record<string, unknown>>({})
const removeCompleted = ref(true)
const removeFailed = ref(true)
const tagIds = ref<number[]>([])
const nameError = ref('')
const schemaFieldErrors = ref<Record<string, string[]>>({})
const saving = ref(false)
const testing = ref(false)

const dialogTitle = computed(() => {
	if (editingId.value !== null) {
		return t('pages.settings.downloadClients.dialog.edit', 'Edit download client')
	}
	return selectedType.value ? t('pages.settings.downloadClients.dialog.addType', { type: t(downloadClientTypeLabel(selectedType.value), humanizeEnumValue(selectedType.value)) }, 'Add {type}') : t('pages.settings.downloadClients.dialog.add', 'Add download client')
})

function toSchemaField(field: DownloadClientFieldSchema): SchemaField {
	let type: SchemaField['type']
	if (field.clrType === 'bool') {
		type = 'checkbox'
	}
	else if (field.clrType === 'enum') {
		type = 'select'
	}
	else if (field.clrType === 'int' || field.clrType === 'double') {
		type = 'number'
	}
	else {
		type = /password|secret|token/i.test(field.name) ? 'password' : 'text'
	}
	return {
		name: field.name,
		label: t(`schema.${selectedType.value}.${field.name}.label`, humanizeFieldName(field.name)),
		type,
		options: field.enumValues,
		required: field.required,
		helpText: undefined,
		default: field.default,
	}
}

const localizedClientTypeOptions = computed(() => downloadClientTypeOptions.map(option => ({
	...option,
	label: t(option.label, humanizeEnumValue(option.value)),
})))
const normalizedFields = computed<SchemaField[]>(() => {
	const schema = settings.downloadClientSchemas.find(s => s.type === selectedType.value)
	return (schema?.fields ?? []).map(toSchemaField)
})

function defaultsForType(type: DownloadClientType): Record<string, unknown> {
	const schema = settings.downloadClientSchemas.find(s => s.type === type)
	const defaults: Record<string, unknown> = {}
	for (const field of schema?.fields ?? []) {
		if (field.default !== undefined && field.default !== null) {
			defaults[field.name] = field.default
		}
		else if (field.clrType === 'bool') {
			defaults[field.name] = false
		}
		else if (field.clrType === 'int' || field.clrType === 'double') {
			defaults[field.name] = 0
		}
		else {
			defaults[field.name] = ''
		}
	}
	return defaults
}

function resetForm() {
	editingId.value = null
	selectedType.value = null
	clientName.value = ''
	clientEnable.value = true
	clientPriority.value = 25
	settingsDraft.value = {}
	removeCompleted.value = true
	removeFailed.value = true
	tagIds.value = []
	nameError.value = ''
	schemaFieldErrors.value = {}
}

function openCreate() {
	resetForm()
	dialogOpen.value = true
}

function pickType(type: DownloadClientType) {
	selectedType.value = type
	settingsDraft.value = defaultsForType(type)
}

function openEdit(client: DownloadClientDto) {
	resetForm()
	editingId.value = client.id
	selectedType.value = client.type
	clientName.value = client.name
	clientEnable.value = client.enable
	clientPriority.value = client.priority
	settingsDraft.value = { ...(client.settings as Record<string, unknown>) }
	removeCompleted.value = client.removeCompleted
	removeFailed.value = client.removeFailed
	tagIds.value = [...client.tagIds]
	dialogOpen.value = true
}

async function save() {
	if (!selectedType.value) {
		return
	}
	nameError.value = ''
	schemaFieldErrors.value = {}
	saving.value = true
	try {
		const body = {
			name: clientName.value,
			type: selectedType.value,
			enable: clientEnable.value,
			priority: clientPriority.value,
			settings: settingsDraft.value,
			removeCompleted: removeCompleted.value,
			removeFailed: removeFailed.value,
			tagIds: tagIds.value,
		}
		const result = editingId.value === null
			? await api.POST('/api/v1/download-clients', { body })
			: await api.PUT('/api/v1/download-clients/{id}', { params: { path: { id: editingId.value } }, body })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: t('pages.settings.downloadClients.added', 'Download client added'), tone: 'ok' })
		dialogOpen.value = false
		await loadClients()
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		const settingsErrors: Record<string, string[]> = {}
		let matched = false
		for (const [key, messages] of Object.entries(apiError.fieldErrors)) {
			if (key === 'name') {
				nameError.value = messages[0] ?? ''
				matched = true
			}
			else if (key.startsWith('settings.')) {
				settingsErrors[key.slice('settings.'.length)] = messages
				matched = true
			}
		}
		schemaFieldErrors.value = settingsErrors
		if (!matched) {
			toast({ title: t('pages.settings.downloadClients.saveError', 'Could not save'), description: apiError.message, tone: 'danger' })
		}
	}
	finally {
		saving.value = false
	}
}

async function testConnection() {
	if (editingId.value === null && !selectedType.value) {
		return
	}
	testing.value = true
	try {
		const result = editingId.value === null
			? await api.POST('/api/v1/download-clients/test', { body: { type: selectedType.value!, settings: settingsDraft.value } })
			: await api.POST('/api/v1/download-clients/{id}/test', { params: { path: { id: editingId.value } } })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		const testResult = result.data
		if (testResult.fieldErrors && Object.keys(testResult.fieldErrors).length > 0) {
			schemaFieldErrors.value = { ...schemaFieldErrors.value, ...testResult.fieldErrors }
		}
		if (testResult.isValid) {
			toast({ title: t('pages.settings.downloadClients.connectionSuccessful', 'Connection successful'), tone: 'ok' })
		}
		else {
			toast({ title: t('pages.settings.downloadClients.connectionFailed', 'Connection failed'), description: testResult.message ?? undefined, tone: 'danger' })
		}
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		toast({ title: t('pages.settings.downloadClients.connectionFailed', 'Connection failed'), description: apiError.message, tone: 'danger' })
	}
	finally {
		testing.value = false
	}
}

/** Runs the connection test for a saved client directly from its row menu, without opening the edit dialog. */
async function testExistingClient(client: DownloadClientDto) {
	editingId.value = client.id
	selectedType.value = client.type
	schemaFieldErrors.value = {}
	await testConnection()
}

// --- Delete -------------------------------------------------------------------
const deleteTarget = ref<DownloadClientDto | null>(null)
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

async function doDelete() {
	if (!deleteTarget.value) {
		return
	}
	deleting.value = true
	deleteError.value = ''
	const result = await api.DELETE('/api/v1/download-clients/{id}', { params: { path: { id: deleteTarget.value.id } } })
	if (!result.response.ok) {
		deleteError.value = toApiError(result.error, result.response).message
		deleting.value = false
		return
	}
	toast({ title: t('pages.settings.downloadClients.deleted', 'Download client deleted'), tone: 'ok' })
	deleteTarget.value = null
	deleting.value = false
	await loadClients()
}

onMounted(async () => {
	loading.value = true
	const [configResult] = await Promise.all([
		api.GET('/api/v1/config/download'),
		reference.load(),
		settings.ensureDownloadClientSchemas(),
		loadClients(),
	])
	if (configResult.data) {
		configDirty.markSaved(configResult.data)
	}
	loading.value = false
})
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.settings.downloadClients.title', 'Download clients')">
			<template #actions>
				<SButton
					variant="primary"
					@click="openCreate"
				>
					{{ t('pages.settings.downloadClients.add', 'Add download client') }}
				</SButton>
			</template>
		</SPageHeader>

		<SSpinner v-if="loading" />

		<template v-else>
			<SSection
				v-if="configDraft"
				:title="t('pages.settings.downloadClients.handling', 'Download handling')"
			>
				<div class="field-grid">
					<SSwitch
						v-model="configDraft.enableCompletedDownloadHandling"
						:label="t('pages.settings.downloadClients.importAutomatically', 'Import completed downloads automatically')"
					/>
					<SSwitch
						v-model="configDraft.removeCompletedDownloads"
						:label="t('pages.settings.downloadClients.removeImported', 'Remove imported downloads from the client')"
					/>
					<SSwitch
						v-model="configDraft.enableFailedDownloadHandling"
						:label="t('pages.settings.downloadClients.handleFailed', 'Handle failed downloads')"
					/>
					<SSwitch
						v-model="configDraft.redownloadFailedReleases"
						:label="t('pages.settings.downloadClients.redownloadFailed', 'Automatically redownload failed releases')"
					/>
					<SSwitch
						v-model="configDraft.removeFailedDownloads"
						:label="t('pages.settings.downloadClients.removeFailed', 'Remove failed downloads from the client')"
					/>
				</div>
				<SField
					:label="t('pages.settings.downloadClients.checkFinishedInterval', 'Check for finished downloads every (minutes)')"
					control-id="check-interval"
				>
					<SInput
						id="check-interval"
						type="number"
						:model-value="String(configDraft.checkForFinishedDownloadInterval)"
						@update:model-value="configDraft.checkForFinishedDownloadInterval = Number($event) || 1"
					/>
				</SField>
			</SSection>

			<SettingsSaveBar
				:dirty="configDirty.isDirty.value"
				:saving="configSaving"
				@save="saveConfig"
				@discard="configDirty.revert()"
			/>

			<SSection :title="t('pages.settings.downloadClients.clients', 'Clients')">
				<SEmptyState
					v-if="clientsLoadError"
					:message="clientsLoadError"
					icon="lucide:alert-triangle"
				>
					<template #action>
						<SButton @click="loadClients">
							{{ t('pages.settings.downloadClients.retry', 'Retry') }}
						</SButton>
					</template>
				</SEmptyState>
				<SSpinner v-else-if="clientsLoading" />
				<STable
					v-else
					:columns="columns"
					:rows="clients"
					:row-key="(row) => row.id"
				>
					<template #cell-type="{ row }">
						{{ t(downloadClientTypeLabel(row.type), humanizeEnumValue(row.type)) }}
					</template>
					<template #cell-enable="{ row }">
						<SBadge :tone="row.enable ? 'ok' : 'neutral'">
							{{ row.enable ? t('pages.settings.downloadClients.enabled', 'Enabled') : t('pages.settings.downloadClients.disabled', 'Disabled') }}
						</SBadge>
					</template>
					<template #cell-tags="{ row }">
						<span v-if="clientTags(row)">{{ clientTags(row) }}</span>
						<span
							v-else
							class="s-cell-muted"
						>{{ t('pages.settings.downloadClients.none', 'None') }}</span>
					</template>
					<template #cell-actions="{ row }">
						<SDropdownMenu
							:items="[
								{ label: t('pages.settings.downloadClients.edit', 'Edit'), icon: 'lucide:pencil', onSelect: () => openEdit(row) },
								{ label: t('pages.settings.downloadClients.test', 'Test'), icon: 'lucide:plug-zap', onSelect: () => testExistingClient(row) },
								{ label: t('pages.settings.downloadClients.delete', 'Delete'), icon: 'lucide:trash-2', danger: true, onSelect: () => (deleteTarget = row) },
							]"
						>
							<template #trigger>
								<SIconButton :label="t('pages.settings.downloadClients.actions', 'Download client actions')">
									<Icon
										name="lucide:more-horizontal"
										aria-hidden="true"
									/>
								</SIconButton>
							</template>
						</SDropdownMenu>
					</template>
					<template #empty>
						<SEmptyState :message="t('pages.settings.downloadClients.emptyHint', 'Add a download client to send releases from search and RSS.')">
							<template #action>
								<SButton
									variant="primary"
									@click="openCreate"
								>
									{{ t('pages.settings.downloadClients.add', 'Add download client') }}
								</SButton>
							</template>
						</SEmptyState>
					</template>
				</STable>
			</SSection>
		</template>

		<SDialog
			v-model="dialogOpen"
			:title="dialogTitle"
			wide
		>
			<div
				v-if="!selectedType"
				class="provider-grid"
			>
				<ProviderCard
					v-for="option in localizedClientTypeOptions"
					:key="option.value"
					:icon="downloadClientTypeIcon(option.value)"
					:label="option.label"
					@click="pickType(option.value as DownloadClientType)"
				/>
			</div>
			<div v-else>
				<SField
					:label="t('pages.settings.downloadClients.name', 'Name')"
					:error="nameError"
					control-id="dc-name"
				>
					<SInput
						id="dc-name"
						v-model="clientName"
						:invalid="!!nameError"
					/>
				</SField>
				<div class="field-grid">
					<SSwitch
						v-model="clientEnable"
						:label="t('pages.settings.downloadClients.enable', 'Enable')"
					/>
					<SField
						:label="t('pages.settings.downloadClients.priority', 'Priority')"
						:hint="t('pages.settings.downloadClients.priorityHint', '1-50, lower is tried first')"
						control-id="dc-priority"
					>
						<SInput
							id="dc-priority"
							type="number"
							:model-value="String(clientPriority)"
							@update:model-value="clientPriority = Number($event) || 1"
						/>
					</SField>
				</div>

				<SchemaForm
					v-model="settingsDraft"
					:fields="normalizedFields"
					:field-errors="schemaFieldErrors"
					:provider-type="selectedType"
				/>

				<div class="field-grid">
					<SSwitch
						v-model="removeCompleted"
						:label="t('pages.settings.downloadClients.removeCompletedDownloads', 'Remove completed downloads')"
					/>
					<SSwitch
						v-model="removeFailed"
						:label="t('pages.settings.downloadClients.removeFailedDownloads', 'Remove failed downloads')"
					/>
				</div>

				<SField
					:label="t('pages.settings.downloadClients.tags', 'Tags')"
					control-id="dc-tags"
				>
					<TagPicker
						v-model:tag-ids="tagIds"
						control-id="dc-tags"
					/>
				</SField>
			</div>

			<template #footer>
				<SButton
					variant="secondary"
					@click="dialogOpen = false"
				>
					{{ t('pages.settings.downloadClients.cancel', 'Cancel') }}
				</SButton>
				<SButton
					v-if="selectedType"
					variant="secondary"
					:loading="testing"
					@click="testConnection"
				>
					{{ t('pages.settings.downloadClients.testConnection', 'Test connection') }}
				</SButton>
				<SButton
					v-if="selectedType"
					variant="primary"
					:loading="saving"
					@click="save"
				>
					{{ editingId === null ? t('pages.settings.downloadClients.add', 'Add download client') : t('pages.settings.downloadClients.saveChanges', 'Save changes') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteTargetOpen"
			:title="t('pages.settings.downloadClients.deleteClient', 'Delete download client')"
		>
			<p v-if="deleteTarget">
				{{ t('pages.settings.downloadClients.confirmDelete', { name: deleteTarget.name }, 'Delete &quot;{name}&quot;? This cannot be undone.') }}
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
					{{ t('pages.settings.downloadClients.cancel', 'Cancel') }}
				</SButton>
				<SButton
					variant="danger"
					:loading="deleting"
					@click="doDelete"
				>
					{{ t('pages.settings.downloadClients.deleteClient', 'Delete download client') }}
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

.provider-grid {
	display: grid;
	grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
	gap: 12px;
}
</style>
