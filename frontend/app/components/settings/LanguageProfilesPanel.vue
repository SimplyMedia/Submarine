<script setup lang="ts">
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useReferenceStore } from '~/stores/reference'
import { languageLabel, languageOptions } from '~/utils/settings-labels'
import type { components } from '~/types/api'

type LanguageProfileResource = components['schemas']['LanguageProfileResource']
type Language = components['schemas']['Language']

const api = useApi()
const reference = useReferenceStore()
const { toast } = useToast()

const loading = ref(true)
const loadError = computed(() => reference.loadError)

const editorOpen = ref(false)
const editingId = ref<number | null>(null)
const name = ref('')
const languages = ref<Language[]>([])
const cutoff = ref<Language | ''>('')
const upgradeAllowed = ref(true)
const addLanguage = ref('')
const nameError = ref('')
const saving = ref(false)

const deleteTarget = ref<LanguageProfileResource | null>(null)
const deleting = ref(false)
const deleteError = ref('')

const deleteTargetOpen = computed({
	get: () => deleteTarget.value !== null,
	set: (value: boolean) => {
		if (!value) {
			deleteTarget.value = null
		}
	},
})

async function load() {
	loading.value = true
	await reference.load()
	loading.value = false
}

void load()

const availableLanguages = computed(() => languageOptions.filter(option => !languages.value.includes(option.value as Language)))

function addSelectedLanguage() {
	if (!addLanguage.value) {
		return
	}
	languages.value = [...languages.value, addLanguage.value as Language]
	if (!cutoff.value) {
		cutoff.value = addLanguage.value as Language
	}
	addLanguage.value = ''
}

function openCreate() {
	editingId.value = null
	name.value = ''
	languages.value = ['ENGLISH']
	cutoff.value = 'ENGLISH'
	upgradeAllowed.value = true
	nameError.value = ''
	editorOpen.value = true
}

function openEdit(profile: LanguageProfileResource) {
	editingId.value = profile.id
	name.value = profile.name
	languages.value = [...profile.languages]
	cutoff.value = profile.cutoff
	upgradeAllowed.value = profile.upgradeAllowed
	nameError.value = ''
	editorOpen.value = true
}

async function save() {
	nameError.value = ''
	if (languages.value.length === 0) {
		nameError.value = 'Add at least one language'
		return
	}
	saving.value = true
	const body = {
		name: name.value,
		languages: languages.value,
		cutoff: (cutoff.value || languages.value[0]!) as Language,
		upgradeAllowed: upgradeAllowed.value,
	}
	try {
		const result = editingId.value === null
			? await api.POST('/api/v1/language-profiles', { body })
			: await api.PUT('/api/v1/language-profiles/{id}', { params: { path: { id: editingId.value } }, body })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: 'Saved', tone: 'ok' })
		editorOpen.value = false
		await reference.load(true)
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		nameError.value = apiError.fieldErrors.name?.[0] ?? apiError.message
	}
	finally {
		saving.value = false
	}
}

function confirmDelete(profile: LanguageProfileResource) {
	deleteError.value = ''
	deleteTarget.value = profile
}

async function doDelete() {
	if (!deleteTarget.value) {
		return
	}
	deleting.value = true
	deleteError.value = ''
	const result = await api.DELETE('/api/v1/language-profiles/{id}', { params: { path: { id: deleteTarget.value.id } } })
	if (!result.response.ok) {
		deleteError.value = toApiError(result.error, result.response).message
		deleting.value = false
		return
	}
	toast({ title: 'Language profile deleted', tone: 'ok' })
	deleteTarget.value = null
	deleting.value = false
	await reference.load(true)
}

const columns = [
	{ key: 'name', label: 'Name' },
	{ key: 'languages', label: 'Languages' },
	{ key: 'cutoff', label: 'Cutoff' },
	{ key: 'actions', label: '', align: 'right' as const },
]
</script>

<template>
	<div>
		<div class="panel-actions">
			<SButton
				variant="primary"
				@click="openCreate"
			>
				Add language profile
			</SButton>
		</div>

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
			:rows="reference.languageProfiles"
			:row-key="(row) => row.id"
		>
			<template #cell-languages="{ row }">
				{{ row.languages.map(l => languageLabel(l)).join(', ') }}
			</template>
			<template #cell-cutoff="{ row }">
				{{ languageLabel(row.cutoff) }}
			</template>
			<template #cell-actions="{ row }">
				<SDropdownMenu
					:items="[
						{ label: 'Edit', icon: 'lucide:pencil', onSelect: () => openEdit(row) },
						{ label: 'Delete', icon: 'lucide:trash-2', danger: true, onSelect: () => confirmDelete(row) },
					]"
				>
					<template #trigger>
						<SIconButton label="Profile actions">
							<Icon
								name="lucide:more-horizontal"
								aria-hidden="true"
							/>
						</SIconButton>
					</template>
				</SDropdownMenu>
			</template>
			<template #empty>
				<SEmptyState message="No language profiles yet. Add one to control which languages are wanted.">
					<template #action>
						<SButton
							variant="primary"
							@click="openCreate"
						>
							Add language profile
						</SButton>
					</template>
				</SEmptyState>
			</template>
		</STable>

		<SDialog
			v-model="editorOpen"
			:title="editingId === null ? 'Add language profile' : 'Edit language profile'"
		>
			<SField
				label="Name"
				:error="nameError"
				control-id="lang-profile-name"
			>
				<SInput
					id="lang-profile-name"
					v-model="name"
					:invalid="!!nameError"
				/>
			</SField>

			<SField label="Add a language">
				<div class="add-row">
					<SSelect
						v-model="addLanguage"
						:options="availableLanguages"
						placeholder="Choose a language"
					/>
					<SButton
						variant="secondary"
						:disabled="!addLanguage"
						@click="addSelectedLanguage"
					>
						Add
					</SButton>
				</div>
			</SField>

			<SField label="Wanted languages, best first">
				<ReorderList
					v-model="languages"
					removable
					:item-key="(item) => item"
				>
					<template #default="{ item }">
						{{ languageLabel(item) }}
					</template>
				</ReorderList>
			</SField>

			<SField
				label="Cutoff"
				hint="Upgrades stop once this language is reached"
				control-id="lang-cutoff"
			>
				<SSelect
					v-model="cutoff"
					control-id="lang-cutoff"
					:options="languages.map(l => ({ value: l, label: languageLabel(l) }))"
				/>
			</SField>

			<SSwitch
				v-model="upgradeAllowed"
				label="Allow upgrades beyond the cutoff"
			/>

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
			title="Delete language profile"
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
	</div>
</template>

<style scoped>
.panel-actions {
	display: flex;
	justify-content: flex-end;
	margin-bottom: 16px;
}

.add-row {
	display: flex;
	gap: 8px;
}
</style>
