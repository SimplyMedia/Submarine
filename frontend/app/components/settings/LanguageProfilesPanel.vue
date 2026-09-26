<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useReferenceStore } from '~/stores/reference'
import { languageLabel, languageOptions } from '~/utils/settings-labels'
import type { components } from '~/types/api'

type LanguageProfileResource = components['schemas']['LanguageProfileResource']
type Language = components['schemas']['Language']

const api = useApi()
const reference = useReferenceStore()
const { toast } = useToast()
const { t } = useI18n()

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

const availableLanguages = computed(() => languageOptions
	.filter(option => !languages.value.includes(option.value as Language))
	.map(option => ({ ...option, label: t(option.label) })))

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
		nameError.value = t('components.settings.LanguageProfilesPanel.addAtLeastOneLanguage')
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
		toast({ title: t('components.settings.LanguageProfilesPanel.saved'), tone: 'ok' })
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
	toast({ title: t('components.settings.LanguageProfilesPanel.deleted'), tone: 'ok' })
	deleteTarget.value = null
	deleting.value = false
	await reference.load(true)
}

const columns = [
	{ key: 'name', label: t('components.settings.LanguageProfilesPanel.name') },
	{ key: 'languages', label: t('components.settings.LanguageProfilesPanel.languages') },
	{ key: 'cutoff', label: t('components.settings.LanguageProfilesPanel.cutoff') },
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
				{{ $t('components.settings.LanguageProfilesPanel.addLanguageProfile') }}
			</SButton>
		</div>

		<SEmptyState
			v-if="loadError"
			:message="loadError"
			icon="lucide:alert-triangle"
		>
			<template #action>
				<SButton @click="load">
					{{ $t('components.settings.LanguageProfilesPanel.retry') }}
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
				{{ row.languages.map(l => t(languageLabel(l))).join(', ') }}
			</template>
			<template #cell-cutoff="{ row }">
				{{ t(languageLabel(row.cutoff)) }}
			</template>
			<template #cell-actions="{ row }">
				<SDropdownMenu
					:items="[
						{ label: t('components.settings.LanguageProfilesPanel.edit'), icon: 'lucide:pencil', onSelect: () => openEdit(row) },
						{ label: t('components.settings.LanguageProfilesPanel.delete'), icon: 'lucide:trash-2', danger: true, onSelect: () => confirmDelete(row) },
					]"
				>
					<template #trigger>
						<SIconButton :label="$t('components.settings.LanguageProfilesPanel.profileActions')">
							<Icon
								name="lucide:more-horizontal"
								aria-hidden="true"
							/>
						</SIconButton>
					</template>
				</SDropdownMenu>
			</template>
			<template #empty>
				<SEmptyState :message="$t('components.settings.LanguageProfilesPanel.emptyMessage')">
					<template #action>
						<SButton
							variant="primary"
							@click="openCreate"
						>
							{{ $t('components.settings.LanguageProfilesPanel.addLanguageProfile') }}
						</SButton>
					</template>
				</SEmptyState>
			</template>
		</STable>

		<SDialog
			v-model="editorOpen"
			:title="editingId === null ? $t('components.settings.LanguageProfilesPanel.addLanguageProfile') : $t('components.settings.LanguageProfilesPanel.editLanguageProfile')"
		>
			<SField
				:label="$t('components.settings.LanguageProfilesPanel.name')"
				:error="nameError"
				control-id="lang-profile-name"
			>
				<SInput
					id="lang-profile-name"
					v-model="name"
					:invalid="!!nameError"
				/>
			</SField>

			<SField :label="$t('components.settings.LanguageProfilesPanel.addLanguage')">
				<div class="add-row">
					<SSelect
						v-model="addLanguage"
						:options="availableLanguages"
						:placeholder="$t('components.settings.LanguageProfilesPanel.chooseLanguage')"
					/>
					<SButton
						variant="secondary"
						:disabled="!addLanguage"
						@click="addSelectedLanguage"
					>
						{{ $t('components.settings.LanguageProfilesPanel.add') }}
					</SButton>
				</div>
			</SField>

			<SField :label="$t('components.settings.LanguageProfilesPanel.wantedLanguages')">
				<ReorderList
					v-model="languages"
					removable
					:item-key="(item) => item"
				>
					<template #default="{ item }">
						{{ t(languageLabel(item)) }}
					</template>
				</ReorderList>
			</SField>

			<SField
				:label="$t('components.settings.LanguageProfilesPanel.cutoff')"
				:hint="$t('components.settings.LanguageProfilesPanel.cutoffHint')"
				control-id="lang-cutoff"
			>
				<SSelect
					v-model="cutoff"
					control-id="lang-cutoff"
					:options="languages.map(l => ({ value: l, label: t(languageLabel(l)) }))"
				/>
			</SField>

			<SSwitch
				v-model="upgradeAllowed"
				:label="$t('components.settings.LanguageProfilesPanel.allowUpgrades')"
			/>

			<template #footer>
				<SButton
					variant="secondary"
					@click="editorOpen = false"
				>
					{{ $t('components.settings.LanguageProfilesPanel.cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="saving"
					@click="save"
				>
					{{ $t('components.settings.LanguageProfilesPanel.saveChanges') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteTargetOpen"
			:title="$t('components.settings.LanguageProfilesPanel.deleteTitle')"
		>
			<p v-if="deleteTarget">
				{{ $t('components.settings.LanguageProfilesPanel.confirmDelete', { name: deleteTarget.name }) }}
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
					{{ $t('components.settings.LanguageProfilesPanel.cancel') }}
				</SButton>
				<SButton
					variant="danger"
					:loading="deleting"
					@click="doDelete"
				>
					{{ $t('components.settings.LanguageProfilesPanel.delete') }}
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
