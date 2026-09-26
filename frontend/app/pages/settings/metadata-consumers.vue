<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { humanizeEnumValue } from '~/utils/settings-labels'
import type { components } from '~/types/api'

const { t } = useI18n()

type MetadataConsumer = components['schemas']['MetadataConsumerDto']
type MetadataConsumerSchema = components['schemas']['MetadataConsumerSchemaDto']
type MetadataConsumerType = components['schemas']['MetadataConsumerType']

interface ConsumerForm {
	name: string
	type: MetadataConsumerType
	enable: boolean
	settings: Record<string, boolean>
}

definePageMeta({ layout: 'default' })
useHead({ title: t('pages.settings.metadataConsumers.title', 'Metadata consumers') })

const api = useApi()
const { toast } = useToast()
const consumers = ref<MetadataConsumer[]>([])
const schemas = ref<MetadataConsumerSchema[]>([])
const loading = ref(true)
const loadError = ref('')
const editorOpen = ref(false)
const editingId = ref<number | null>(null)
const form = ref<ConsumerForm>(blankForm())
const formError = ref('')
const saving = ref(false)
const deleteTarget = ref<MetadataConsumer | null>(null)
const deleting = ref(false)

function blankForm(type: MetadataConsumerType = 'KODI'): ConsumerForm {
	return { name: '', type, enable: true, settings: {} }
}

const selectedSchema = computed(() => schemas.value.find(schema => schema.type === form.value.type))
const deleteTargetOpen = computed({
	get: () => deleteTarget.value !== null,
	set: (open: boolean) => { if (!open) deleteTarget.value = null },
})

async function load() {
	loading.value = true
	loadError.value = ''
	const [consumerResult, schemaResult] = await Promise.all([
		api.GET('/api/v1/metadata-consumers'),
		api.GET('/api/v1/metadata-consumers/schema'),
	])
	if (!consumerResult.data || !schemaResult.data) {
		loadError.value = t('pages.settings.metadataConsumers.loadError', 'Could not load metadata consumers. Check your connection and try again.')
	}
	consumers.value = consumerResult.data ?? []
	schemas.value = schemaResult.data ?? []
	loading.value = false
}

void load()

function withSchemaDefaults(type: MetadataConsumerType, settings: Record<string, boolean> = {}) {
	const schema = schemas.value.find(item => item.type === type)
	return Object.fromEntries((schema?.fields ?? []).map(field => [field.name, settings[field.name] ?? field.default]))
}

function openCreate() {
	const type = (schemas.value[0]?.type ?? 'KODI') as MetadataConsumerType
	form.value = blankForm(type)
	form.value.settings = withSchemaDefaults(type)
	editingId.value = null
	formError.value = ''
	editorOpen.value = true
}

function openEdit(consumer: MetadataConsumer) {
	const type = consumer.type as MetadataConsumerType
	let settings: Record<string, boolean> = {}
	try {
		settings = JSON.parse(consumer.settingsJson) as Record<string, boolean>
	}
	catch {
		settings = {}
	}
	form.value = { name: consumer.name, type, enable: consumer.enable, settings: withSchemaDefaults(type, settings) }
	editingId.value = consumer.id
	formError.value = ''
	editorOpen.value = true
}

function changeType(type: MetadataConsumerType) {
	form.value.type = type
	form.value.settings = withSchemaDefaults(type)
}

async function save() {
	formError.value = ''
	saving.value = true
	const body = {
		name: form.value.name,
		type: form.value.type,
		enable: form.value.enable,
		settingsJson: JSON.stringify(form.value.settings),
	}
	try {
		const result = editingId.value === null
			? await api.POST('/api/v1/metadata-consumers', { body })
			: await api.PUT('/api/v1/metadata-consumers/{id}', { params: { path: { id: editingId.value } }, body })
		if (!result.data) throw toApiError(result.error, result.response)
		toast({ title: editingId.value === null ? t('pages.settings.metadataConsumers.added', 'Metadata consumer added') : t('pages.settings.metadataConsumers.saved', 'Metadata consumer saved'), tone: 'ok' })
		editorOpen.value = false
		await load()
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		formError.value = apiError.fieldErrors.name?.[0] ?? apiError.message
	}
	finally {
		saving.value = false
	}
}

async function removeConsumer() {
	if (!deleteTarget.value) return
	deleting.value = true
	const result = await api.DELETE('/api/v1/metadata-consumers/{id}', { params: { path: { id: deleteTarget.value.id } } })
	deleting.value = false
	if (!result.response.ok) {
		toast({ title: t('pages.settings.metadataConsumers.deleteError', 'Could not delete metadata consumer'), description: toApiError(result.error, result.response).message, tone: 'danger' })
		return
	}
	toast({ title: t('pages.settings.metadataConsumers.deleted', 'Metadata consumer deleted'), tone: 'ok' })
	deleteTarget.value = null
	await load()
}
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.settings.metadataConsumers.title', 'Metadata consumers')">
			<template #actions>
				<SButton
					variant="primary"
					:disabled="schemas.length === 0"
					@click="openCreate"
				>
					{{ t('pages.settings.metadataConsumers.addConsumer', 'Add consumer') }}
				</SButton>
			</template>
		</SPageHeader>

		<SSection :title="t('pages.settings.metadataConsumers.companionFiles', 'Companion files')">
			<p class="section-copy">
				{{ t('pages.settings.metadataConsumers.companionFilesHint', 'Choose the media centers that receive metadata files beside your media.') }}
			</p>
			<SEmptyState
				v-if="loadError"
				:message="loadError"
				icon="lucide:alert-triangle"
			>
				<template #action>
					<SButton @click="load">
						{{ t('pages.settings.metadataConsumers.retry', 'Retry') }}
					</SButton>
				</template>
			</SEmptyState>
			<SSpinner v-else-if="loading" />
			<SEmptyState
				v-else-if="consumers.length === 0"
				:message="t('pages.settings.metadataConsumers.emptyHint', 'No metadata consumers configured.')"
			>
				<template #action>
					<SButton
						variant="primary"
						:disabled="schemas.length === 0"
						@click="openCreate"
					>
						{{ t('pages.settings.metadataConsumers.addConsumer', 'Add consumer') }}
					</SButton>
				</template>
			</SEmptyState>
			<div
				v-else
				class="consumer-list"
			>
				<article
					v-for="consumer in consumers"
					:key="consumer.id"
					class="consumer-row"
				>
					<div class="consumer-copy">
						<div class="consumer-title">
							<h3>{{ consumer.name }}</h3>
							<SBadge :tone="consumer.enable ? 'ok' : 'neutral'">
								{{ consumer.enable ? t('pages.settings.metadataConsumers.enabled', 'Enabled') : t('pages.settings.metadataConsumers.disabled', 'Disabled') }}
							</SBadge>
						</div>
						<p>{{ t(`pages.settings.metadataConsumers.types.${consumer.type.toLowerCase()}`, humanizeEnumValue(consumer.type)) }}</p>
					</div>
					<div class="row-actions">
						<SButton
							variant="secondary"
							@click="openEdit(consumer)"
						>
							{{ t('pages.settings.metadataConsumers.edit', 'Edit') }}
						</SButton>
						<SIconButton
							:label="t('pages.settings.metadataConsumers.deleteConsumer', 'Delete consumer')"
							@click="deleteTarget = consumer"
						>
							<Icon
								name="lucide:trash-2"
								aria-hidden="true"
							/>
						</SIconButton>
					</div>
				</article>
			</div>
		</SSection>

		<SDialog
			v-model="editorOpen"
			:title="editingId === null ? t('pages.settings.metadataConsumers.addDialog', 'Add metadata consumer') : t('pages.settings.metadataConsumers.editDialog', 'Edit metadata consumer')"
			wide
		>
			<SField
				:label="t('pages.settings.metadataConsumers.name', 'Name')"
				control-id="consumer-name"
				:error="formError"
			>
				<SInput
					id="consumer-name"
					v-model="form.name"
					:invalid="!!formError"
				/>
			</SField>
			<SField
				:label="t('pages.settings.metadataConsumers.mediaCenter', 'Media center')"
				control-id="consumer-type"
			>
				<SSelect
					:model-value="form.type"
					:options="schemas.map(schema => ({ value: schema.type, label: t(`pages.settings.metadataConsumers.types.${schema.type.toLowerCase()}`, humanizeEnumValue(schema.type)) }))"
					:disabled="editingId !== null"
					@update:model-value="changeType($event as MetadataConsumerType)"
				/>
			</SField>
			<SSwitch
				v-model="form.enable"
				:label="t('pages.settings.metadataConsumers.enableConsumer', 'Enable this consumer')"
			/>
			<SSection :title="t('pages.settings.metadataConsumers.filesToWrite', 'Files to write')">
				<p
					v-if="selectedSchema?.fields.length"
					class="section-copy"
				>
					{{ t('pages.settings.metadataConsumers.filesToWriteHint', 'Choose which companion files to create for this media center.') }}
				</p>
				<div
					v-if="selectedSchema?.fields.length"
					class="settings-fields"
				>
					<SSwitch
						v-for="field in selectedSchema.fields"
						:key="field.name"
						:model-value="form.settings[field.name] ?? field.default"
						:label="t(`schema.${form.type}.${field.name}.label`, field.label)"
						@update:model-value="form.settings[field.name] = $event"
					/>
					<p
						v-for="field in selectedSchema.fields"
						:key="`${field.name}-hint`"
						class="field-help"
					>
						{{ t(`schema.${form.type}.${field.name}.helpText`, field.helpText ?? '') }}
					</p>
				</div>
				<p
					v-else
					class="field-help"
				>
					{{ t('pages.settings.metadataConsumers.noFileOptions', 'This media center has no file options.') }}
				</p>
			</SSection>
			<template #footer>
				<SButton
					variant="secondary"
					@click="editorOpen = false"
				>
					{{ t('pages.settings.metadataConsumers.cancel', 'Cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="saving"
					@click="save"
				>
					{{ t('pages.settings.metadataConsumers.saveChanges', 'Save changes') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteTargetOpen"
			:title="t('pages.settings.metadataConsumers.deleteDialog', 'Delete metadata consumer')"
		>
			<p v-if="deleteTarget">
				{{ t('pages.settings.metadataConsumers.confirmDelete', { name: deleteTarget.name }, 'Delete "{name}"? This cannot be undone.') }}
			</p>
			<template #footer>
				<SButton
					variant="secondary"
					@click="deleteTarget = null"
				>
					{{ t('pages.settings.metadataConsumers.cancel', 'Cancel') }}
				</SButton>
				<SButton
					variant="danger"
					:loading="deleting"
					@click="removeConsumer"
				>
					{{ t('pages.settings.metadataConsumers.delete', 'Delete') }}
				</SButton>
			</template>
		</SDialog>
	</div>
</template>

<style scoped>
.section-copy,
.field-help {
	margin: 0;
	color: var(--fg-muted);
	font-size: 0.875rem;
}

.consumer-list {
	display: grid;
}

.consumer-row {
	display: flex;
	align-items: center;
	justify-content: space-between;
	gap: 16px;
	min-height: 64px;
	padding: 12px 0;
	border-bottom: 1px solid var(--line);
}

.consumer-row:first-child {
	padding-top: 0;
}

.consumer-title,
.row-actions {
	display: flex;
	align-items: center;
	gap: 10px;
}

.consumer-title h3 {
	margin: 0;
	font-size: 0.95rem;
	font-weight: 500;
}

.consumer-copy p {
	margin: 4px 0 0;
	color: var(--fg-muted);
	font-size: 0.8125rem;
}

.settings-fields {
	display: grid;
	gap: 12px;
}

.field-help {
	margin-top: -12px;
	padding-left: 28px;
}

@media (max-width: 600px) {
	.consumer-row {
		align-items: flex-start;
	}

	.consumer-title {
		align-items: flex-start;
		flex-direction: column;
	}
}
</style>
