<script setup lang="ts">
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useSettingsStore } from '~/stores/settings'
import { notificationTypeIcon, notificationTypeLabel, notificationTypeOptions } from '~/utils/settings-labels'
import type { components } from '~/types/api'
import type { SchemaField } from '~/types/schema-form'

type NotificationDto = components['schemas']['NotificationDto']
type NotificationType = components['schemas']['NotificationType']
type SaveNotificationRequest = components['schemas']['SaveNotificationRequest']
type TestNotificationRequest = components['schemas']['TestNotificationRequest']

interface FormState {
	name: string
	enable: boolean
	onGrab: boolean
	onImport: boolean
	onUpgrade: boolean
	onRename: boolean
	onDelete: boolean
	onHealthIssue: boolean
	onHealthRestored: boolean
	onApplicationUpdate: boolean
	onManualInteractionRequired: boolean
	includeHealthWarnings: boolean
	tags: string[]
}

type BooleanFormKey = { [K in keyof FormState]: FormState[K] extends boolean ? K : never }[keyof FormState]

const EVENT_FIELDS: ReadonlyArray<{ key: BooleanFormKey, label: string, event: string }> = [
	{ key: 'onGrab', label: 'Grab', event: 'GRAB' },
	{ key: 'onImport', label: 'Import', event: 'IMPORT' },
	{ key: 'onUpgrade', label: 'Upgrade', event: 'UPGRADE' },
	{ key: 'onRename', label: 'Rename', event: 'RENAME' },
	{ key: 'onDelete', label: 'Delete', event: 'DELETE' },
	{ key: 'onHealthIssue', label: 'Health issue', event: 'HEALTH' },
	{ key: 'onHealthRestored', label: 'Health restored', event: 'HEALTH_RESTORED' },
	{ key: 'onApplicationUpdate', label: 'Application update', event: 'APPLICATION_UPDATE' },
	{ key: 'onManualInteractionRequired', label: 'Manual interaction required', event: 'MANUAL_INTERACTION' },
]

/** Event toggles the given notification type can actually deliver, per its schema's supportedEvents. */
function eventFieldsFor(type: string) {
	const supported = settings.notificationSchemas.find(s => s.type === type)?.supportedEvents
	return supported ? EVENT_FIELDS.filter(f => supported.includes(f.event)) : EVENT_FIELDS
}

definePageMeta({ layout: 'default' })
useHead({ title: 'Connect' })

const api = useApi()
const settings = useSettingsStore()
const { toast } = useToast()

const notifications = ref<NotificationDto[]>([])
const loading = ref(true)
const loadError = ref('')

const dialogOpen = ref(false)
const editingId = ref<number | null>(null)
const selectedType = ref<string>('')
const form = ref<FormState>(defaultForm())
const settingsDraft = ref<Record<string, unknown>>({})
const tagInput = ref('')
const fieldErrors = ref<Record<string, string[]>>({})
const generalError = ref('')
const saving = ref(false)
const testing = ref(false)
const testingRowId = ref<number | null>(null)

const deleteTarget = ref<{ id: number, name: string } | null>(null)
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

function defaultForm(): FormState {
	return {
		name: '',
		enable: true,
		onGrab: false,
		onImport: true,
		onUpgrade: true,
		onRename: false,
		onDelete: false,
		onHealthIssue: false,
		onHealthRestored: false,
		onApplicationUpdate: false,
		onManualInteractionRequired: false,
		includeHealthWarnings: false,
		tags: [],
	}
}

function schemaFieldsFor(type: string): SchemaField[] {
	const schema = settings.notificationSchemas.find(s => s.type === type)
	return (schema?.fields ?? []) as unknown as SchemaField[]
}

function typeDefault(type: string): unknown {
	if (type === 'checkbox') {
		return false
	}
	if (type === 'tags') {
		return []
	}
	if (type === 'number') {
		return null
	}
	return ''
}

function defaultSettings(type: string): Record<string, unknown> {
	const result: Record<string, unknown> = {}
	for (const field of schemaFieldsFor(type)) {
		result[field.name] = field.default ?? typeDefault(field.type)
	}
	return result
}

// The backend's WebhookSettings.Headers binds to Dictionary<string,string>, but the
// schema declares `headers` as a `tags` field (chip UI of "Key:Value" strings) like every
// other tags field. Convert at the settingsJson boundary so the generic chip UI still works
// and the backend gets the shape it actually deserializes ({} instead of []).
function toBackendSettings(type: string, draft: Record<string, unknown>): Record<string, unknown> {
	const result: Record<string, unknown> = { ...draft }
	for (const field of schemaFieldsFor(type)) {
		if (field.type !== 'tags' || field.name !== 'headers') {
			continue
		}
		const entries = Array.isArray(result[field.name]) ? result[field.name] as string[] : []
		const headers: Record<string, string> = {}
		for (const entry of entries) {
			const separator = entry.indexOf(':')
			if (separator > 0) {
				headers[entry.slice(0, separator).trim()] = entry.slice(separator + 1).trim()
			}
		}
		result[field.name] = headers
	}
	return result
}

function fromBackendSettings(type: string, parsed: Record<string, unknown>): Record<string, unknown> {
	const result: Record<string, unknown> = { ...parsed }
	for (const field of schemaFieldsFor(type)) {
		if (field.type !== 'tags' || field.name !== 'headers') {
			continue
		}
		const value = result[field.name]
		if (value && typeof value === 'object' && !Array.isArray(value)) {
			result[field.name] = Object.entries(value as Record<string, string>).map(([key, val]) => `${key}:${val}`)
		}
	}
	return result
}

function eventsSummary(row: NotificationDto): string {
	const active = EVENT_FIELDS.filter(f => Boolean(row[f.key])).map(f => f.label)
	return active.length > 0 ? active.join(', ') : 'None'
}

async function load() {
	loading.value = true
	loadError.value = ''
	const [result] = await Promise.all([api.GET('/api/v1/notifications'), settings.ensureNotificationSchemas()])
	if (!result.data) {
		loadError.value = 'Could not load notifications. Check your connection and try again.'
	}
	notifications.value = result.data ?? []
	loading.value = false
}

void load()

function openCreate() {
	editingId.value = null
	selectedType.value = ''
	form.value = defaultForm()
	settingsDraft.value = {}
	tagInput.value = ''
	fieldErrors.value = {}
	generalError.value = ''
	resetTraktAuth()
	dialogOpen.value = true
}

function selectType(type: string) {
	selectedType.value = type
	settingsDraft.value = defaultSettings(type)
	resetTraktAuth()
}

type TraktAuthStatus = 'idle' | 'starting' | 'pending' | 'authorized' | 'denied' | 'expired' | 'error'

const traktAuthStatus = ref<TraktAuthStatus>('idle')
const traktUserCode = ref('')
const traktVerificationUrl = ref('')
const traktError = ref('')
let traktDeviceCode = ''
let traktPollHandle: ReturnType<typeof setTimeout> | null = null

const traktConnected = computed(() => typeof settingsDraft.value.accessToken === 'string' && settingsDraft.value.accessToken.length > 0)

function stopTraktPolling() {
	if (traktPollHandle !== null) {
		clearTimeout(traktPollHandle)
		traktPollHandle = null
	}
}

function resetTraktAuth() {
	stopTraktPolling()
	traktAuthStatus.value = 'idle'
	traktUserCode.value = ''
	traktVerificationUrl.value = ''
	traktError.value = ''
	traktDeviceCode = ''
}

onUnmounted(stopTraktPolling)

function traktDraftString(key: string): string | null {
	const value = settingsDraft.value[key]
	return typeof value === 'string' && value.length > 0 ? value : null
}

async function startTraktAuth() {
	traktAuthStatus.value = 'starting'
	traktError.value = ''
	const result = await api.POST('/api/v1/notifications/trakt/authorize', {
		body: { clientId: traktDraftString('clientId') },
	})
	if (!result.data) {
		traktAuthStatus.value = 'error'
		traktError.value = toApiError(result.error, result.response).message
		return
	}
	traktDeviceCode = result.data.deviceCode
	traktUserCode.value = result.data.userCode
	traktVerificationUrl.value = result.data.verificationUrl
	traktAuthStatus.value = 'pending'
	schedulePoll(result.data.interval)
}

function schedulePoll(interval: number) {
	traktPollHandle = setTimeout(() => void pollTraktAuth(interval), interval * 1000)
}

async function pollTraktAuth(interval: number) {
	const result = await api.POST('/api/v1/notifications/trakt/poll', {
		body: {
			clientId: traktDraftString('clientId'),
			clientSecret: traktDraftString('clientSecret'),
			deviceCode: traktDeviceCode,
		},
	})
	if (!result.data) {
		traktAuthStatus.value = 'error'
		traktError.value = toApiError(result.error, result.response).message
		return
	}
	if (result.data.status === 'AUTHORIZED') {
		settingsDraft.value = {
			...settingsDraft.value,
			accessToken: result.data.accessToken,
			refreshToken: result.data.refreshToken,
			expiresAt: result.data.expiresAt,
		}
		traktAuthStatus.value = 'authorized'
		return
	}
	if (result.data.status === 'PENDING') {
		schedulePoll(interval)
		return
	}
	traktAuthStatus.value = result.data.status === 'DENIED' ? 'denied' : 'expired'
}

function openEdit(row: NotificationDto) {
	editingId.value = row.id
	selectedType.value = row.type
	form.value = {
		name: row.name,
		enable: row.enable,
		onGrab: row.onGrab,
		onImport: row.onImport,
		onUpgrade: row.onUpgrade,
		onRename: row.onRename,
		onDelete: row.onDelete,
		onHealthIssue: row.onHealthIssue,
		onHealthRestored: row.onHealthRestored,
		onApplicationUpdate: row.onApplicationUpdate,
		onManualInteractionRequired: row.onManualInteractionRequired,
		includeHealthWarnings: row.includeHealthWarnings,
		tags: [...row.tags],
	}
	settingsDraft.value = row.settingsJson ? fromBackendSettings(row.type, JSON.parse(row.settingsJson) as Record<string, unknown>) : {}
	tagInput.value = ''
	resetTraktAuth()
	fieldErrors.value = {}
	generalError.value = ''
	dialogOpen.value = true
}

function addTagChip() {
	const value = tagInput.value.trim()
	if (value.length === 0) {
		return
	}
	if (!form.value.tags.includes(value)) {
		form.value.tags = [...form.value.tags, value]
	}
	tagInput.value = ''
}

function removeTagChip(index: number) {
	form.value.tags = form.value.tags.filter((_, i) => i !== index)
}

async function save() {
	if (!selectedType.value) {
		return
	}
	fieldErrors.value = {}
	generalError.value = ''
	saving.value = true
	try {
		const body: SaveNotificationRequest = {
			name: form.value.name,
			type: selectedType.value as NotificationType,
			enable: form.value.enable,
			settingsJson: JSON.stringify(toBackendSettings(selectedType.value, settingsDraft.value)),
			onGrab: form.value.onGrab,
			onImport: form.value.onImport,
			onUpgrade: form.value.onUpgrade,
			onRename: form.value.onRename,
			onDelete: form.value.onDelete,
			onHealthIssue: form.value.onHealthIssue,
			onHealthRestored: form.value.onHealthRestored,
			onApplicationUpdate: form.value.onApplicationUpdate,
			onManualInteractionRequired: form.value.onManualInteractionRequired,
			includeHealthWarnings: form.value.includeHealthWarnings,
			tags: form.value.tags,
		}
		const result = editingId.value === null
			? await api.POST('/api/v1/notifications', { body })
			: await api.PUT('/api/v1/notifications/{id}', { params: { path: { id: editingId.value } }, body })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: editingId.value === null ? 'Connection added' : 'Connection saved', tone: 'ok' })
		dialogOpen.value = false
		await load()
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		fieldErrors.value = apiError.fieldErrors
		generalError.value = apiError.message
	}
	finally {
		saving.value = false
	}
}

async function runTest(target: { id: number } | TestNotificationRequest): Promise<void> {
	try {
		const result = 'id' in target
			? await api.POST('/api/v1/notifications/{id}/test', { params: { path: { id: target.id } } })
			: await api.POST('/api/v1/notifications/test', { body: target })
		if (!result.response.ok) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: 'Test succeeded', tone: 'ok' })
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		toast({ title: 'Test failed', tone: 'danger', description: apiError.message })
	}
}

async function testDialog() {
	if (!selectedType.value) {
		return
	}
	testing.value = true
	if (editingId.value !== null) {
		await runTest({ id: editingId.value })
	}
	else {
		await runTest({ type: selectedType.value as NotificationType, settingsJson: JSON.stringify(toBackendSettings(selectedType.value, settingsDraft.value)) })
	}
	testing.value = false
}

async function testRow(row: NotificationDto) {
	testingRowId.value = row.id
	await runTest({ id: row.id })
	testingRowId.value = null
}

function confirmDelete(row: NotificationDto) {
	deleteError.value = ''
	deleteTarget.value = { id: row.id, name: row.name }
}

async function doDelete() {
	if (!deleteTarget.value) {
		return
	}
	deleting.value = true
	deleteError.value = ''
	const result = await api.DELETE('/api/v1/notifications/{id}', { params: { path: { id: deleteTarget.value.id } } })
	if (!result.response.ok) {
		deleteError.value = toApiError(result.error, result.response).message
		deleting.value = false
		return
	}
	toast({ title: 'Connection deleted', tone: 'ok' })
	deleteTarget.value = null
	deleting.value = false
	await load()
}

const columns = [
	{ key: 'name', label: 'Name' },
	{ key: 'type', label: 'Type' },
	{ key: 'enable', label: 'Status' },
	{ key: 'events', label: 'Events' },
	{ key: 'actions', label: '', align: 'right' as const },
]
</script>

<template>
	<div>
		<SPageHeader title="Connect">
			<template #actions>
				<SButton
					variant="primary"
					@click="openCreate"
				>
					Add connection
				</SButton>
			</template>
		</SPageHeader>

		<SSection>
			<SEmptyState
				v-if="loadError"
				:message="loadError"
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
				:columns="columns"
				:rows="notifications"
				:row-key="(row) => row.id"
			>
				<template #cell-type="{ row }">
					{{ notificationTypeLabel(row.type) }}
				</template>
				<template #cell-enable="{ row }">
					<SBadge :tone="row.enable ? 'ok' : 'neutral'">
						{{ row.enable ? 'Enabled' : 'Disabled' }}
					</SBadge>
				</template>
				<template #cell-events="{ row }">
					{{ eventsSummary(row) }}
				</template>
				<template #cell-actions="{ row }">
					<SDropdownMenu
						:items="[
							{ label: 'Edit', icon: 'lucide:pencil', onSelect: () => openEdit(row) },
							{ label: 'Test', icon: 'lucide:zap', disabled: testingRowId === row.id, onSelect: () => testRow(row) },
							{ label: 'Delete', icon: 'lucide:trash-2', danger: true, onSelect: () => confirmDelete(row) },
						]"
					>
						<template #trigger>
							<SIconButton label="Connection actions">
								<Icon
									name="lucide:more-horizontal"
									aria-hidden="true"
								/>
							</SIconButton>
						</template>
					</SDropdownMenu>
				</template>
				<template #empty>
					<SEmptyState message="Add a connection to send notifications on events">
						<template #action>
							<SButton
								variant="primary"
								@click="openCreate"
							>
								Add connection
							</SButton>
						</template>
					</SEmptyState>
				</template>
			</STable>
		</SSection>

		<SDialog
			v-model="dialogOpen"
			wide
			:title="editingId === null ? 'Add connection' : 'Edit connection'"
		>
			<template v-if="editingId === null">
				<p class="connect-subheading">
					Choose a connection type
				</p>
				<div class="connect-grid">
					<ProviderCard
						v-for="opt in notificationTypeOptions"
						:key="opt.value"
						:icon="notificationTypeIcon(opt.value)"
						:label="opt.label"
						:selected="selectedType === opt.value"
						@click="selectType(opt.value)"
					/>
				</div>
			</template>

			<template v-if="selectedType">
				<SField
					label="Name"
					:error="fieldErrors.name?.[0]"
					control-id="connect-name"
				>
					<SInput
						id="connect-name"
						v-model="form.name"
						placeholder="My webhook"
						:invalid="!!fieldErrors.name?.length"
					/>
				</SField>
				<SSwitch
					v-model="form.enable"
					label="Enable"
				/>

				<SchemaForm
					:fields="schemaFieldsFor(selectedType)"
					:model-value="settingsDraft"
					:field-errors="fieldErrors"
					@update:model-value="settingsDraft = $event"
				/>

				<template v-if="selectedType === 'TRAKT'">
					<p class="connect-subheading">
						Trakt authorization
					</p>
					<SBadge
						v-if="traktConnected"
						tone="ok"
					>
						Connected
					</SBadge>
					<template v-else>
						<SButton
							v-if="traktAuthStatus === 'idle' || traktAuthStatus === 'error'"
							variant="secondary"
							@click="startTraktAuth"
						>
							Authenticate with Trakt
						</SButton>
						<SSpinner v-else-if="traktAuthStatus === 'starting'" />
						<div v-else-if="traktAuthStatus === 'pending'">
							<p>
								Go to
								<a
									:href="traktVerificationUrl"
									target="_blank"
									rel="noopener noreferrer"
								>{{ traktVerificationUrl }}</a>
								and enter code <strong>{{ traktUserCode }}</strong>
							</p>
							<SSpinner />
						</div>
						<p
							v-else-if="traktAuthStatus === 'denied'"
							class="s-field-error"
							role="alert"
						>
							Authorization was denied. Try again.
						</p>
						<p
							v-else-if="traktAuthStatus === 'expired'"
							class="s-field-error"
							role="alert"
						>
							The code expired before it was used. Try again.
						</p>
						<p
							v-if="traktError"
							class="s-field-error"
							role="alert"
						>
							{{ traktError }}
						</p>
					</template>
				</template>

				<p class="connect-subheading">
					Notify on
				</p>
				<div class="connect-events-grid">
					<SCheckbox
						v-for="ev in eventFieldsFor(selectedType)"
						:key="ev.key"
						v-model="form[ev.key]"
						:label="ev.label"
					/>
				</div>
				<SSwitch
					v-model="form.includeHealthWarnings"
					label="Include health warnings"
				/>

				<SField label="Tags">
					<div class="connect-tags">
						<span
							v-for="(tag, index) in form.tags"
							:key="`${tag}-${index}`"
							class="connect-tag"
						>
							{{ tag }}
							<button
								type="button"
								class="connect-tag-remove"
								:aria-label="`Remove ${tag}`"
								@click="removeTagChip(index)"
							>
								<Icon
									name="lucide:x"
									aria-hidden="true"
								/>
							</button>
						</span>
						<input
							v-model="tagInput"
							type="text"
							class="connect-tag-input"
							:placeholder="form.tags.length === 0 ? 'Type and press enter' : ''"
							@keydown.enter.prevent="addTagChip"
						>
					</div>
				</SField>

				<p
					v-if="generalError"
					class="s-field-error"
					role="alert"
				>
					{{ generalError }}
				</p>
			</template>

			<template #footer>
				<SButton
					variant="secondary"
					@click="dialogOpen = false"
				>
					Cancel
				</SButton>
				<SButton
					v-if="selectedType"
					variant="secondary"
					:loading="testing"
					@click="testDialog"
				>
					Test connection
				</SButton>
				<SButton
					v-if="selectedType"
					variant="primary"
					:loading="saving"
					@click="save"
				>
					{{ editingId === null ? 'Add connection' : 'Save changes' }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteTargetOpen"
			title="Delete connection"
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
					Delete connection
				</SButton>
			</template>
		</SDialog>
	</div>
</template>

<style scoped>
.connect-subheading {
	margin: 20px 0 8px;
	font-size: 1rem;
	font-weight: 600;
	color: var(--fg);
}

.connect-grid {
	display: grid;
	grid-template-columns: repeat(auto-fill, minmax(96px, 1fr));
	gap: 8px;
}

.connect-events-grid {
	display: grid;
	grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
	gap: 8px 16px;
}

.connect-tags {
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	gap: 6px;
	padding: 6px 8px;
	border: 1px solid var(--line);
	border-radius: var(--r-control);
	background: var(--surface);
}

.connect-tag {
	display: inline-flex;
	align-items: center;
	gap: 4px;
	padding: 2px 6px 2px 8px;
	border-radius: 999px;
	background: var(--surface-2);
	font-size: 0.8125rem;
}

.connect-tag-remove {
	display: inline-flex;
	align-items: center;
	justify-content: center;
	color: var(--fg-muted);
}

.connect-tag-remove:hover {
	color: var(--fg);
}

.connect-tag-input {
	flex: 1;
	min-width: 120px;
	border: none;
	background: transparent;
	color: var(--fg);
	font-size: 0.8125rem;
	outline: none;
}
</style>
