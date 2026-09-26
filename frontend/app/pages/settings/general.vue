<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useDirtyForm } from '~/composables/useDirtyForm'
import { indexerProxyTypeOptions } from '~/utils/indexer-labels'
import { authenticationRequiredOptions, authMethodOptions, certificateValidationOptions } from '~/utils/settings-labels'
import type { components } from '~/types/api'

type GeneralConfig = components['schemas']['GeneralConfig']
type UserDto = components['schemas']['UserDto']

definePageMeta({ layout: 'default' })
const { t } = useI18n()
useHead({ title: t('pages.settings.general.title') })

const api = useApi()
const system = useSystemStore()
const { toast } = useToast()

const logLevelOptions = ['Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal'].map(value => ({ value, label: t(`pages.settings.general.logLevels.${value.toLowerCase()}`) }))
// FlareSolverr is a per-indexer proxy kind only, not a valid outbound proxy for every request.
const outboundProxyTypeOptions = indexerProxyTypeOptions.filter(option => option.value !== 'FLARESOLVERR').map(option => ({ ...option, label: t(option.label, option.label) }))

// ---- General config ----

const loading = ref(true)
const saving = ref(false)
const draft = ref<GeneralConfig>({})
const { isDirty, markSaved, revert, patchSnapshot } = useDirtyForm(draft)

async function load() {
	loading.value = true
	const result = await api.GET('/api/v1/config/general')
	if (result.data) {
		markSaved(result.data)
	}
	loading.value = false
}
void load()

async function save() {
	saving.value = true
	try {
		const body: GeneralConfig = {
			authMethod: draft.value.authMethod,
			authenticationRequired: draft.value.authenticationRequired,
			trustedProxies: draft.value.trustedProxies,
			feedToken: draft.value.feedToken,
			urlBase: draft.value.urlBase,
			instanceName: draft.value.instanceName,
			logLevel: draft.value.logLevel,
			branch: draft.value.branch,
			certificateValidation: draft.value.certificateValidation,
			proxyEnabled: draft.value.proxyEnabled,
			proxyType: draft.value.proxyType,
			proxyHost: draft.value.proxyHost,
			proxyPort: draft.value.proxyPort,
			proxyUsername: draft.value.proxyUsername,
			proxyPassword: draft.value.proxyPassword,
			proxyBypassFilter: draft.value.proxyBypassFilter,
			proxyBypassLocalAddresses: draft.value.proxyBypassLocalAddresses,
			backupFolder: draft.value.backupFolder,
			backupIntervalDays: draft.value.backupIntervalDays,
			backupRetention: draft.value.backupRetention,
			applicationUrl: draft.value.applicationUrl,
		}
		const result = await api.PUT('/api/v1/config/general', { body })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		markSaved(result.data)
		if (system.status) {
			system.status.instanceName = result.data.instanceName ?? 'Submarine'
		}
		toast({ title: t('pages.settings.general.saved'), tone: 'ok' })
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		toast({ title: t('pages.settings.general.saveFailed'), tone: 'danger', description: apiError.message })
	}
	finally {
		saving.value = false
	}
}

async function copyApiKey() {
	await navigator.clipboard.writeText(draft.value.apiKey ?? '')
	toast({ title: t('pages.settings.general.copied'), tone: 'ok' })
}

async function copyFeedToken() {
	await navigator.clipboard.writeText(draft.value.feedToken ?? '')
	toast({ title: t('pages.settings.general.copied'), tone: 'ok' })
}

const regenerateOpen = ref(false)
const regenerating = ref(false)

async function regenerateApiKey() {
	regenerating.value = true
	try {
		const result = await api.POST('/api/v1/config/general/api-key')
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		patchSnapshot({ apiKey: result.data.apiKey })
		regenerateOpen.value = false
		toast({ title: t('pages.settings.general.apiKeyRegenerated'), tone: 'ok' })
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		toast({ title: t('pages.settings.general.apiKeyRegenerateFailed'), tone: 'danger', description: apiError.message })
	}
	finally {
		regenerating.value = false
	}
}

// ---- Users ----

const users = ref<UserDto[]>([])
const usersLoading = ref(true)

async function loadUsers() {
	usersLoading.value = true
	const result = await api.GET('/api/v1/users')
	if (result.data) {
		users.value = result.data.items
	}
	usersLoading.value = false
}
void loadUsers()

const usersColumns = [
	{ key: 'username', label: t('pages.settings.general.username') },
	{ key: 'actions', label: '', align: 'right' as const },
]

const addUserOpen = ref(false)
const newUsername = ref('')
const newPassword = ref('')
const newPasswordConfirm = ref('')
const addUserError = ref('')
const addingUser = ref(false)

function openAddUser() {
	newUsername.value = ''
	newPassword.value = ''
	newPasswordConfirm.value = ''
	addUserError.value = ''
	addUserOpen.value = true
}

async function addUser() {
	addUserError.value = ''
	if (newPassword.value !== newPasswordConfirm.value) {
		addUserError.value = t('pages.settings.general.passwordMismatch')
		return
	}
	addingUser.value = true
	try {
		const result = await api.POST('/api/v1/users', { body: { username: newUsername.value, password: newPassword.value } })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: t('pages.settings.general.userAdded'), tone: 'ok' })
		addUserOpen.value = false
		await loadUsers()
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		addUserError.value = apiError.fieldErrors.username?.[0] ?? apiError.fieldErrors.password?.[0] ?? apiError.message
	}
	finally {
		addingUser.value = false
	}
}

const renameTarget = ref<UserDto | null>(null)
const renameValue = ref('')
const renameError = ref('')
const renaming = ref(false)

const renameTargetOpen = computed({
	get: () => renameTarget.value !== null,
	set: (value: boolean) => {
		if (!value) {
			renameTarget.value = null
		}
	},
})

function openRename(user: UserDto) {
	renameTarget.value = user
	renameValue.value = user.username
	renameError.value = ''
}

async function doRename() {
	if (!renameTarget.value) {
		return
	}
	renameError.value = ''
	renaming.value = true
	try {
		const result = await api.PUT('/api/v1/users/{id}', {
			params: { path: { id: renameTarget.value.id } },
			body: { username: renameValue.value, password: null },
		})
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: t('pages.settings.general.userRenamed'), tone: 'ok' })
		renameTarget.value = null
		await loadUsers()
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		renameError.value = apiError.fieldErrors.username?.[0] ?? apiError.message
	}
	finally {
		renaming.value = false
	}
}

const passwordTarget = ref<UserDto | null>(null)
const passwordValue = ref('')
const passwordConfirm = ref('')
const passwordError = ref('')
const changingPassword = ref(false)

const passwordTargetOpen = computed({
	get: () => passwordTarget.value !== null,
	set: (value: boolean) => {
		if (!value) {
			passwordTarget.value = null
		}
	},
})

function openChangePassword(user: UserDto) {
	passwordTarget.value = user
	passwordValue.value = ''
	passwordConfirm.value = ''
	passwordError.value = ''
}

async function doChangePassword() {
	if (!passwordTarget.value) {
		return
	}
	passwordError.value = ''
	if (passwordValue.value !== passwordConfirm.value) {
		passwordError.value = t('pages.settings.general.passwordMismatch')
		return
	}
	changingPassword.value = true
	try {
		const result = await api.PUT('/api/v1/users/{id}', {
			params: { path: { id: passwordTarget.value.id } },
			body: { username: null, password: passwordValue.value },
		})
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: t('pages.settings.general.passwordChanged'), tone: 'ok' })
		passwordTarget.value = null
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		passwordError.value = apiError.fieldErrors.password?.[0] ?? apiError.message
	}
	finally {
		changingPassword.value = false
	}
}

const deleteTarget = ref<UserDto | null>(null)
const deleteError = ref('')
const deleting = ref(false)

const deleteTargetOpen = computed({
	get: () => deleteTarget.value !== null,
	set: (value: boolean) => {
		if (!value) {
			deleteTarget.value = null
		}
	},
})

function confirmDeleteUser(user: UserDto) {
	deleteTarget.value = user
	deleteError.value = ''
}

async function doDeleteUser() {
	if (!deleteTarget.value) {
		return
	}
	deleting.value = true
	deleteError.value = ''
	const result = await api.DELETE('/api/v1/users/{id}', { params: { path: { id: deleteTarget.value.id } } })
	if (!result.response.ok) {
		deleteError.value = toApiError(result.error, result.response).message
		deleting.value = false
		return
	}
	toast({ title: t('pages.settings.general.userDeleted'), tone: 'ok' })
	deleteTarget.value = null
	deleting.value = false
	await loadUsers()
}
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.settings.general.title')" />

		<SSection :title="t('pages.settings.general.title')">
			<SSpinner v-if="loading" />
			<div
				v-else
				class="settings-form"
			>
				<SField
					:label="t('pages.settings.general.authentication')"
					control-id="general-auth-method"
				>
					<SSelect
						v-model="draft.authMethod"
						control-id="general-auth-method"
						:options="authMethodOptions.map(option => ({ ...option, label: t(option.label, option.label) }))"
					/>
				</SField>
				<SField
					:label="t('pages.settings.general.instanceName')"
					control-id="general-instance-name"
				>
					<SInput
						id="general-instance-name"
						v-model="draft.instanceName"
					/>
				</SField>
				<SField
					:label="t('pages.settings.general.urlBase')"
					:hint="t('pages.settings.general.urlBaseHint')"
					control-id="general-url-base"
				>
					<SInput
						id="general-url-base"
						v-model="draft.urlBase"
					/>
				</SField>
				<SField
					:label="t('pages.settings.general.applicationUrl')"
					:hint="t('pages.settings.general.applicationUrlHint')"
					control-id="general-application-url"
				>
					<SInput
						id="general-application-url"
						v-model="draft.applicationUrl"
					/>
				</SField>
				<SField
					:label="t('pages.settings.general.logLevel')"
					control-id="general-log-level"
				>
					<SSelect
						v-model="draft.logLevel"
						control-id="general-log-level"
						:options="logLevelOptions"
					/>
				</SField>
				<SField
					:label="t('pages.settings.general.branch')"
					control-id="general-branch"
				>
					<SInput
						id="general-branch"
						v-model="draft.branch"
					/>
				</SField>
				<SField
					:label="t('pages.settings.general.apiKey')"
					:hint="t('pages.settings.general.apiKeyHint')"
					control-id="general-api-key"
				>
					<div class="key-row">
						<SInput
							id="general-api-key"
							:model-value="draft.apiKey"
							disabled
						/>
						<SIconButton
							:label="t('pages.settings.general.copyApiKey')"
							@click="copyApiKey"
						>
							<Icon
								name="lucide:copy"
								aria-hidden="true"
							/>
						</SIconButton>
						<SButton
							variant="secondary"
							@click="regenerateOpen = true"
						>
							{{ t('pages.settings.general.regenerate') }}
						</SButton>
					</div>
				</SField>
				<SField
					:label="t('pages.settings.general.feedToken')"
					:hint="t('pages.settings.general.feedTokenHint')"
					control-id="general-feed-token"
				>
					<div class="key-row">
						<SInput
							id="general-feed-token"
							:model-value="draft.feedToken"
							disabled
						/>
						<SIconButton
							:label="t('pages.settings.general.copyFeedToken')"
							@click="copyFeedToken"
						>
							<Icon
								name="lucide:copy"
								aria-hidden="true"
							/>
						</SIconButton>
					</div>
				</SField>
			</div>
		</SSection>

		<SSection :title="t('pages.settings.general.security')">
			<SSpinner v-if="loading" />
			<div
				v-else
				class="settings-form"
			>
				<SField
					:label="t('pages.settings.general.authenticationRequired')"
					:hint="t('pages.settings.general.authenticationRequiredHint')"
					control-id="general-auth-required"
				>
					<SSelect
						v-model="draft.authenticationRequired"
						control-id="general-auth-required"
						:options="authenticationRequiredOptions.map(option => ({ ...option, label: t(option.label, option.label) }))"
					/>
				</SField>
				<SField
					:label="t('pages.settings.general.trustedProxies')"
					:hint="t('pages.settings.general.trustedProxiesHint')"
					control-id="general-trusted-proxies"
				>
					<SInput
						id="general-trusted-proxies"
						v-model="draft.trustedProxies"
					/>
				</SField>
				<SField
					:label="t('pages.settings.general.certificateValidation')"
					:hint="t('pages.settings.general.certificateValidationHint')"
					control-id="general-cert-validation"
				>
					<SSelect
						v-model="draft.certificateValidation"
						control-id="general-cert-validation"
						:options="certificateValidationOptions.map(option => ({ ...option, label: t(option.label, option.label) }))"
					/>
				</SField>
			</div>
		</SSection>

		<SSection :title="t('pages.settings.general.proxy')">
			<SSpinner v-if="loading" />
			<div
				v-else
				class="settings-form"
			>
				<SSwitch
					v-model="draft.proxyEnabled"
					:label="t('pages.settings.general.useOutboundProxy')"
				/>
				<template v-if="draft.proxyEnabled">
					<SField
						:label="t('pages.settings.general.type')"
						control-id="general-proxy-type"
					>
						<SSelect
							v-model="draft.proxyType"
							control-id="general-proxy-type"
							:options="outboundProxyTypeOptions"
						/>
					</SField>
					<div class="field-grid">
						<SField
							:label="t('pages.settings.general.host')"
							control-id="general-proxy-host"
						>
							<SInput
								id="general-proxy-host"
								v-model="draft.proxyHost"
							/>
						</SField>
						<SField
							:label="t('pages.settings.general.port')"
							control-id="general-proxy-port"
						>
							<SInput
								id="general-proxy-port"
								type="number"
								:model-value="String(draft.proxyPort)"
								@update:model-value="draft!.proxyPort = Number($event) || 0"
							/>
						</SField>
					</div>
					<div class="field-grid">
						<SField
							:label="t('pages.settings.general.username')"
							control-id="general-proxy-username"
						>
							<SInput
								id="general-proxy-username"
								:model-value="draft.proxyUsername ?? ''"
								@update:model-value="draft!.proxyUsername = $event"
							/>
						</SField>
						<SField
							:label="t('pages.settings.general.password')"
							control-id="general-proxy-password"
						>
							<SInput
								id="general-proxy-password"
								type="password"
								:model-value="draft.proxyPassword ?? ''"
								@update:model-value="draft!.proxyPassword = $event"
							/>
						</SField>
					</div>
					<SField
						:label="t('pages.settings.general.bypassFilter')"
						:hint="t('pages.settings.general.bypassFilterHint')"
						control-id="general-proxy-bypass"
					>
						<SInput
							id="general-proxy-bypass"
							v-model="draft.proxyBypassFilter"
						/>
					</SField>
					<SSwitch
						v-model="draft.proxyBypassLocalAddresses"
						:label="t('pages.settings.general.bypassLocalAddresses')"
					/>
				</template>
			</div>
		</SSection>

		<SSection :title="t('pages.settings.general.backup')">
			<SSpinner v-if="loading" />
			<div
				v-else
				class="settings-form"
			>
				<SField
					:label="t('pages.settings.general.backupFolder')"
					:hint="t('pages.settings.general.backupFolderHint')"
					control-id="general-backup-folder"
				>
					<SInput
						id="general-backup-folder"
						v-model="draft.backupFolder"
					/>
				</SField>
				<div class="field-grid">
					<SField
						:label="t('pages.settings.general.backupInterval')"
						control-id="general-backup-interval"
					>
						<SInput
							id="general-backup-interval"
							type="number"
							:model-value="String(draft.backupIntervalDays)"
							@update:model-value="draft!.backupIntervalDays = Number($event) || 0"
						/>
					</SField>
					<SField
						:label="t('pages.settings.general.backupRetention')"
						control-id="general-backup-retention"
					>
						<SInput
							id="general-backup-retention"
							type="number"
							:model-value="String(draft.backupRetention)"
							@update:model-value="draft!.backupRetention = Number($event) || 0"
						/>
					</SField>
				</div>
			</div>
		</SSection>

		<SSection :title="t('pages.settings.general.users')">
			<SSpinner v-if="usersLoading" />
			<STable
				v-else
				:columns="usersColumns"
				:rows="users"
				:row-key="(row) => row.id"
			>
				<template #cell-actions="{ row }">
					<SDropdownMenu
						:items="[
							{ label: t('pages.settings.general.changePassword'), icon: 'lucide:key-round', onSelect: () => openChangePassword(row) },
							{ label: t('pages.settings.general.rename'), icon: 'lucide:pencil', onSelect: () => openRename(row) },
							{ label: t('pages.settings.general.delete'), icon: 'lucide:trash-2', danger: true, onSelect: () => confirmDeleteUser(row) },
						]"
					>
						<template #trigger>
							<SIconButton :label="t('pages.settings.general.userActions')">
								<Icon
									name="lucide:more-horizontal"
									aria-hidden="true"
								/>
							</SIconButton>
						</template>
					</SDropdownMenu>
				</template>
				<template #empty>
					<SEmptyState :message="t('pages.settings.general.usersEmpty')">
						<template #action>
							<SButton
								variant="primary"
								@click="openAddUser"
							>
								{{ t('pages.settings.general.addUser') }}
							</SButton>
						</template>
					</SEmptyState>
				</template>
			</STable>
			<div
				v-if="!usersLoading && users.length > 0"
				class="users-actions"
			>
				<SButton
					variant="primary"
					@click="openAddUser"
				>
					{{ t('pages.settings.general.addUser') }}
				</SButton>
			</div>
		</SSection>

		<SettingsSaveBar
			:dirty="isDirty"
			:saving="saving"
			@save="save"
			@discard="revert"
		/>

		<SDialog
			v-model="regenerateOpen"
			:title="t('pages.settings.general.regenerateApiKeyQuestion')"
		>
			<p>{{ t('pages.settings.general.regenerateApiKeyWarning') }}</p>
			<template #footer>
				<SButton
					variant="secondary"
					:disabled="regenerating"
					@click="regenerateOpen = false"
				>
					{{ t('pages.settings.general.cancel') }}
				</SButton>
				<SButton
					variant="danger"
					:loading="regenerating"
					@click="regenerateApiKey"
				>
					{{ t('pages.settings.general.regenerate') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="addUserOpen"
			:title="t('pages.settings.general.addUser')"
		>
			<SField
				:label="t('pages.settings.general.username')"
				control-id="new-user-username"
			>
				<SInput
					id="new-user-username"
					v-model="newUsername"
				/>
			</SField>
			<SField
				:label="t('pages.settings.general.password')"
				control-id="new-user-password"
			>
				<SInput
					id="new-user-password"
					v-model="newPassword"
					type="password"
				/>
			</SField>
			<SField
				:label="t('pages.settings.general.confirmPassword')"
				:error="addUserError"
				control-id="new-user-password-confirm"
			>
				<SInput
					id="new-user-password-confirm"
					v-model="newPasswordConfirm"
					type="password"
					:invalid="!!addUserError"
				/>
			</SField>
			<template #footer>
				<SButton
					variant="secondary"
					@click="addUserOpen = false"
				>
					{{ t('pages.settings.general.cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="addingUser"
					@click="addUser"
				>
					{{ t('pages.settings.general.addUser') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="renameTargetOpen"
			:title="t('pages.settings.general.renameUser')"
		>
			<SField
				:label="t('pages.settings.general.username')"
				:error="renameError"
				control-id="rename-user-username"
			>
				<SInput
					id="rename-user-username"
					v-model="renameValue"
					:invalid="!!renameError"
				/>
			</SField>
			<template #footer>
				<SButton
					variant="secondary"
					:disabled="renaming"
					@click="renameTarget = null"
				>
					{{ t('pages.settings.general.cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="renaming"
					@click="doRename"
				>
					{{ t('pages.settings.general.saveChanges') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="passwordTargetOpen"
			:title="t('pages.settings.general.changePassword')"
		>
			<SField
				:label="t('pages.settings.general.newPassword')"
				control-id="change-password-value"
			>
				<SInput
					id="change-password-value"
					v-model="passwordValue"
					type="password"
				/>
			</SField>
			<SField
				:label="t('pages.settings.general.confirmNewPassword')"
				:error="passwordError"
				control-id="change-password-confirm"
			>
				<SInput
					id="change-password-confirm"
					v-model="passwordConfirm"
					type="password"
					:invalid="!!passwordError"
				/>
			</SField>
			<template #footer>
				<SButton
					variant="secondary"
					:disabled="changingPassword"
					@click="passwordTarget = null"
				>
					{{ t('pages.settings.general.cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="changingPassword"
					@click="doChangePassword"
				>
					{{ t('pages.settings.general.saveChanges') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteTargetOpen"
			:title="t('pages.settings.general.deleteUser')"
		>
			<p v-if="deleteTarget">
				{{ t('pages.settings.general.deleteUserConfirm', { username: deleteTarget.username }) }}
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
					{{ t('pages.settings.general.cancel') }}
				</SButton>
				<SButton
					variant="danger"
					:loading="deleting"
					@click="doDeleteUser"
				>
					{{ t('pages.settings.general.deleteUser') }}
				</SButton>
			</template>
		</SDialog>
	</div>
</template>

<style scoped>
.settings-form {
	display: grid;
	gap: 16px;
	max-width: 480px;
}

.field-grid {
	display: grid;
	grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
	gap: 16px;
}

.key-row {
	display: flex;
	align-items: center;
	gap: 8px;
}

.key-row .s-input {
	flex: 1;
	min-width: 0;
}

.users-actions {
	display: flex;
	justify-content: flex-end;
	margin-top: 16px;
}
</style>
