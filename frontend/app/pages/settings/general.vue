<script setup lang="ts">
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useDirtyForm } from '~/composables/useDirtyForm'
import { authMethodOptions } from '~/utils/settings-labels'
import type { components } from '~/types/api'

type GeneralConfig = components['schemas']['GeneralConfig']
type UserDto = components['schemas']['UserDto']

definePageMeta({ layout: 'default' })
useHead({ title: 'General' })

const api = useApi()
const { toast } = useToast()

const logLevelOptions = ['Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal'].map(value => ({ value, label: value }))

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
			feedToken: draft.value.feedToken,
			urlBase: draft.value.urlBase,
			instanceName: draft.value.instanceName,
			logLevel: draft.value.logLevel,
			branch: draft.value.branch,
			updateAutomatically: draft.value.updateAutomatically,
		}
		const result = await api.PUT('/api/v1/config/general', { body })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		markSaved(result.data)
		toast({ title: 'Saved', tone: 'ok' })
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		toast({ title: 'Could not save', tone: 'danger', description: apiError.message })
	}
	finally {
		saving.value = false
	}
}

async function copyApiKey() {
	await navigator.clipboard.writeText(draft.value.apiKey ?? '')
	toast({ title: 'Copied', tone: 'ok' })
}

async function copyFeedToken() {
	await navigator.clipboard.writeText(draft.value.feedToken ?? '')
	toast({ title: 'Copied', tone: 'ok' })
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
		toast({ title: 'API key regenerated', tone: 'ok' })
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		toast({ title: 'Could not regenerate API key', tone: 'danger', description: apiError.message })
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
	{ key: 'username', label: 'Username' },
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
		addUserError.value = 'Passwords do not match.'
		return
	}
	addingUser.value = true
	try {
		const result = await api.POST('/api/v1/users', { body: { username: newUsername.value, password: newPassword.value } })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: 'User added', tone: 'ok' })
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
		toast({ title: 'User renamed', tone: 'ok' })
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
		passwordError.value = 'Passwords do not match.'
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
		toast({ title: 'Password changed', tone: 'ok' })
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
	toast({ title: 'User deleted', tone: 'ok' })
	deleteTarget.value = null
	deleting.value = false
	await loadUsers()
}
</script>

<template>
	<div>
		<SPageHeader title="General" />

		<SSection title="General">
			<SSpinner v-if="loading" />
			<div
				v-else
				class="settings-form"
			>
				<SField
					label="Authentication"
					control-id="general-auth-method"
				>
					<SSelect
						v-model="draft.authMethod"
						control-id="general-auth-method"
						:options="authMethodOptions"
					/>
				</SField>
				<SField
					label="Instance name"
					control-id="general-instance-name"
				>
					<SInput
						id="general-instance-name"
						v-model="draft.instanceName"
					/>
				</SField>
				<SField
					label="URL base"
					hint="e.g. /submarine, empty for root"
					control-id="general-url-base"
				>
					<SInput
						id="general-url-base"
						v-model="draft.urlBase"
					/>
				</SField>
				<SField
					label="Log level"
					control-id="general-log-level"
				>
					<SSelect
						v-model="draft.logLevel"
						control-id="general-log-level"
						:options="logLevelOptions"
					/>
				</SField>
				<SField
					label="Branch"
					control-id="general-branch"
				>
					<SInput
						id="general-branch"
						v-model="draft.branch"
					/>
				</SField>
				<SField
					label="API key"
					hint="Used by external tools and the realtime event feed."
					control-id="general-api-key"
				>
					<div class="key-row">
						<SInput
							id="general-api-key"
							:model-value="draft.apiKey"
							disabled
						/>
						<SIconButton
							label="Copy API key"
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
							Regenerate
						</SButton>
					</div>
				</SField>
				<SField
					label="Feed token"
					hint="Read-only token for feeds like iCal."
					control-id="general-feed-token"
				>
					<div class="key-row">
						<SInput
							id="general-feed-token"
							:model-value="draft.feedToken"
							disabled
						/>
						<SIconButton
							label="Copy feed token"
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

		<SSection title="Users">
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
							{ label: 'Change password', icon: 'lucide:key-round', onSelect: () => openChangePassword(row) },
							{ label: 'Rename', icon: 'lucide:pencil', onSelect: () => openRename(row) },
							{ label: 'Delete', icon: 'lucide:trash-2', danger: true, onSelect: () => confirmDeleteUser(row) },
						]"
					>
						<template #trigger>
							<SIconButton label="User actions">
								<Icon
									name="lucide:more-horizontal"
									aria-hidden="true"
								/>
							</SIconButton>
						</template>
					</SDropdownMenu>
				</template>
				<template #empty>
					<SEmptyState message="Add a user to control who can sign in.">
						<template #action>
							<SButton
								variant="primary"
								@click="openAddUser"
							>
								Add user
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
					Add user
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
			title="Regenerate API key?"
		>
			<p>Anything using the current key will stop working.</p>
			<template #footer>
				<SButton
					variant="secondary"
					:disabled="regenerating"
					@click="regenerateOpen = false"
				>
					Cancel
				</SButton>
				<SButton
					variant="danger"
					:loading="regenerating"
					@click="regenerateApiKey"
				>
					Regenerate
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="addUserOpen"
			title="Add user"
		>
			<SField
				label="Username"
				control-id="new-user-username"
			>
				<SInput
					id="new-user-username"
					v-model="newUsername"
				/>
			</SField>
			<SField
				label="Password"
				control-id="new-user-password"
			>
				<SInput
					id="new-user-password"
					v-model="newPassword"
					type="password"
				/>
			</SField>
			<SField
				label="Confirm password"
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
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="addingUser"
					@click="addUser"
				>
					Add user
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="renameTargetOpen"
			title="Rename user"
		>
			<SField
				label="Username"
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
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="renaming"
					@click="doRename"
				>
					Save changes
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="passwordTargetOpen"
			title="Change password"
		>
			<SField
				label="New password"
				control-id="change-password-value"
			>
				<SInput
					id="change-password-value"
					v-model="passwordValue"
					type="password"
				/>
			</SField>
			<SField
				label="Confirm new password"
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
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="changingPassword"
					@click="doChangePassword"
				>
					Save changes
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteTargetOpen"
			title="Delete user"
		>
			<p v-if="deleteTarget">
				Delete "{{ deleteTarget.username }}"? This cannot be undone.
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
					@click="doDeleteUser"
				>
					Delete user
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
