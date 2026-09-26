<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import type { components } from '~/types/api'

type ReleaseProfileResource = components['schemas']['ReleaseProfileResource']

const api = useApi()
const { toast } = useToast()
const { t } = useI18n()

const loading = ref(true)
const loadError = ref('')
const profiles = ref<ReleaseProfileResource[]>([])
const indexerOptions = ref<Array<{ value: string, label: string }>>([])

const editorOpen = ref(false)
const editingId = ref<number | null>(null)
const name = ref('')
const enabled = ref(true)
const required = ref<string[]>([])
const ignored = ref<string[]>([])
const indexerId = ref('ALL')
const tagIds = ref<number[]>([])
const requiredTerm = ref('')
const ignoredTerm = ref('')
const nameError = ref('')
const saving = ref(false)

const deleteTarget = ref<ReleaseProfileResource | null>(null)
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
	loadError.value = ''
	const [profilesResult, indexersResult] = await Promise.all([
		api.GET('/api/v1/release-profiles', { params: { query: { PageSize: 200 } } }),
		api.GET('/api/v1/indexers', { params: { query: { PageSize: 200 } } }),
	])
	if (!profilesResult.data) {
		loadError.value = t('components.settings.ReleaseProfilesPanel.loadError')
	}
	profiles.value = profilesResult.data?.items ?? []
	indexerOptions.value = (indexersResult.data?.items ?? []).map(indexer => ({ value: String(indexer.id), label: indexer.name }))
	loading.value = false
}

void load()

function indexerName(id: number | null) {
	if (id == null) {
		return t('components.settings.ReleaseProfilesPanel.allIndexers')
	}
	return indexerOptions.value.find(option => option.value === String(id))?.label ?? t('components.settings.ReleaseProfilesPanel.indexerNumber', { number: id })
}

function openCreate() {
	editingId.value = null
	name.value = ''
	enabled.value = true
	required.value = []
	ignored.value = []
	indexerId.value = 'ALL'
	tagIds.value = []
	requiredTerm.value = ''
	ignoredTerm.value = ''
	nameError.value = ''
	editorOpen.value = true
}

function openEdit(profile: ReleaseProfileResource) {
	editingId.value = profile.id
	name.value = profile.name
	enabled.value = profile.enabled
	required.value = [...profile.required]
	ignored.value = [...profile.ignored]
	indexerId.value = profile.indexerId == null ? 'ALL' : String(profile.indexerId)
	tagIds.value = [...profile.tags]
	requiredTerm.value = ''
	ignoredTerm.value = ''
	nameError.value = ''
	editorOpen.value = true
}

function addRequired() {
	const value = requiredTerm.value.trim()
	if (value.length === 0) {
		return
	}
	required.value = [...required.value, value]
	requiredTerm.value = ''
}

function addIgnored() {
	const value = ignoredTerm.value.trim()
	if (value.length === 0) {
		return
	}
	ignored.value = [...ignored.value, value]
	ignoredTerm.value = ''
}

async function save() {
	nameError.value = ''
	saving.value = true
	const body = {
		name: name.value,
		enabled: enabled.value,
		required: required.value,
		ignored: ignored.value,
		indexerId: indexerId.value === 'ALL' ? null : Number(indexerId.value),
		tags: tagIds.value,
	}
	try {
		const result = editingId.value === null
			? await api.POST('/api/v1/release-profiles', { body })
			: await api.PUT('/api/v1/release-profiles/{id}', { params: { path: { id: editingId.value } }, body })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: t('components.settings.ReleaseProfilesPanel.saved'), tone: 'ok' })
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

function confirmDelete(profile: ReleaseProfileResource) {
	deleteError.value = ''
	deleteTarget.value = profile
}

async function doDelete() {
	if (!deleteTarget.value) {
		return
	}
	deleting.value = true
	deleteError.value = ''
	const result = await api.DELETE('/api/v1/release-profiles/{id}', { params: { path: { id: deleteTarget.value.id } } })
	if (!result.response.ok) {
		deleteError.value = toApiError(result.error, result.response).message
		deleting.value = false
		return
	}
	toast({ title: t('components.settings.ReleaseProfilesPanel.deleted'), tone: 'ok' })
	deleteTarget.value = null
	deleting.value = false
	await load()
}

const columns = [
	{ key: 'name', label: t('components.settings.ReleaseProfilesPanel.name') },
	{ key: 'enabled', label: t('components.settings.ReleaseProfilesPanel.enabled') },
	{ key: 'required', label: t('components.settings.ReleaseProfilesPanel.required'), align: 'right' as const },
	{ key: 'ignored', label: t('components.settings.ReleaseProfilesPanel.ignored'), align: 'right' as const },
	{ key: 'indexer', label: t('components.settings.ReleaseProfilesPanel.indexer') },
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
				{{ $t('components.settings.ReleaseProfilesPanel.addProfile') }}
			</SButton>
		</div>

		<SEmptyState
			v-if="loadError"
			:message="loadError"
			icon="lucide:alert-triangle"
		>
			<template #action>
				<SButton @click="load">
					{{ $t('components.settings.ReleaseProfilesPanel.retry') }}
				</SButton>
			</template>
		</SEmptyState>
		<SSpinner v-else-if="loading" />
		<STable
			v-else
			:columns="columns"
			:rows="profiles"
			:row-key="(row) => row.id"
		>
			<template #cell-enabled="{ row }">
				<SBadge :tone="row.enabled ? 'ok' : 'neutral'">
					{{ row.enabled ? $t('components.settings.ReleaseProfilesPanel.enabled') : $t('components.settings.ReleaseProfilesPanel.disabled') }}
				</SBadge>
			</template>
			<template #cell-required="{ row }">
				{{ row.required.length }}
			</template>
			<template #cell-ignored="{ row }">
				{{ row.ignored.length }}
			</template>
			<template #cell-indexer="{ row }">
				{{ indexerName(row.indexerId) }}
			</template>
			<template #cell-actions="{ row }">
				<SDropdownMenu
					:items="[
						{ label: t('components.settings.ReleaseProfilesPanel.edit'), icon: 'lucide:pencil', onSelect: () => openEdit(row) },
						{ label: t('components.settings.ReleaseProfilesPanel.delete'), icon: 'lucide:trash-2', danger: true, onSelect: () => confirmDelete(row) },
					]"
				>
					<template #trigger>
						<SIconButton :label="$t('components.settings.ReleaseProfilesPanel.profileActions')">
							<Icon
								name="lucide:more-horizontal"
								aria-hidden="true"
							/>
						</SIconButton>
					</template>
				</SDropdownMenu>
			</template>
			<template #empty>
				<SEmptyState :message="$t('components.settings.ReleaseProfilesPanel.emptyMessage')">
					<template #action>
						<SButton
							variant="primary"
							@click="openCreate"
						>
							{{ $t('components.settings.ReleaseProfilesPanel.addProfile') }}
						</SButton>
					</template>
				</SEmptyState>
			</template>
		</STable>

		<SDialog
			v-model="editorOpen"
			:title="editingId === null ? $t('components.settings.ReleaseProfilesPanel.addProfile') : $t('components.settings.ReleaseProfilesPanel.editProfile')"
		>
			<SField
				:label="$t('components.settings.ReleaseProfilesPanel.name')"
				:error="nameError"
				control-id="release-profile-name"
			>
				<SInput
					id="release-profile-name"
					v-model="name"
					:invalid="!!nameError"
				/>
			</SField>
			<SSwitch
				v-model="enabled"
				:label="$t('components.settings.ReleaseProfilesPanel.active')"
			/>

			<SField
				:label="$t('components.settings.ReleaseProfilesPanel.requiredTerms')"
				:hint="$t('components.settings.ReleaseProfilesPanel.requiredTermsHint')"
			>
				<div class="term-list">
					<span
						v-for="(term, index) in required"
						:key="`req-${index}`"
						class="term-chip"
					>
						{{ term }}
						<button
							type="button"
							:aria-label="$t('components.settings.ReleaseProfilesPanel.removeTerm', { term })"
							@click="required = required.filter((_, i) => i !== index)"
						>
							<Icon
								name="lucide:x"
								aria-hidden="true"
							/>
						</button>
					</span>
					<input
						v-model="requiredTerm"
						type="text"
						class="term-input"
						:placeholder="$t('components.settings.ReleaseProfilesPanel.typePressEnter')"
						@keydown.enter.prevent="addRequired"
					>
				</div>
			</SField>

			<SField
				:label="$t('components.settings.ReleaseProfilesPanel.ignoredTerms')"
				:hint="$t('components.settings.ReleaseProfilesPanel.ignoredTermsHint')"
			>
				<div class="term-list">
					<span
						v-for="(term, index) in ignored"
						:key="`ign-${index}`"
						class="term-chip"
					>
						{{ term }}
						<button
							type="button"
							:aria-label="$t('components.settings.ReleaseProfilesPanel.removeTerm', { term })"
							@click="ignored = ignored.filter((_, i) => i !== index)"
						>
							<Icon
								name="lucide:x"
								aria-hidden="true"
							/>
						</button>
					</span>
					<input
						v-model="ignoredTerm"
						type="text"
						class="term-input"
						:placeholder="$t('components.settings.ReleaseProfilesPanel.typePressEnter')"
						@keydown.enter.prevent="addIgnored"
					>
				</div>
			</SField>

			<SField
				:label="$t('components.settings.ReleaseProfilesPanel.restrictToIndexer')"
				control-id="release-profile-indexer"
			>
				<SSelect
					v-model="indexerId"
					control-id="release-profile-indexer"
					:options="[{ value: 'ALL', label: t('components.settings.ReleaseProfilesPanel.allIndexers') }, ...indexerOptions]"
				/>
			</SField>

			<SField :label="$t('components.settings.ReleaseProfilesPanel.tags')">
				<TagPicker v-model:tag-ids="tagIds" />
			</SField>

			<template #footer>
				<SButton
					variant="secondary"
					@click="editorOpen = false"
				>
					{{ $t('components.settings.ReleaseProfilesPanel.cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="saving"
					@click="save"
				>
					{{ $t('components.settings.ReleaseProfilesPanel.saveChanges') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteTargetOpen"
			:title="$t('components.settings.ReleaseProfilesPanel.deleteTitle')"
		>
			<p v-if="deleteTarget">
				{{ $t('components.settings.ReleaseProfilesPanel.confirmDelete', { name: deleteTarget.name }) }}
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
					{{ $t('components.settings.ReleaseProfilesPanel.cancel') }}
				</SButton>
				<SButton
					variant="danger"
					:loading="deleting"
					@click="doDelete"
				>
					{{ $t('components.settings.ReleaseProfilesPanel.delete') }}
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

.term-list {
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	gap: 6px;
	min-height: var(--control-h);
	padding: 6px 8px;
	border: 1px solid var(--line-strong);
	border-radius: var(--r-control);
	background: var(--surface);
}

.term-chip {
	display: inline-flex;
	align-items: center;
	gap: 4px;
	height: 24px;
	padding: 0 6px 0 10px;
	border-radius: 999px;
	background: var(--surface-2);
	font-size: 0.8125rem;
}

.term-chip button {
	display: inline-flex;
	align-items: center;
	justify-content: center;
	width: 16px;
	height: 16px;
	border-radius: 999px;
	color: var(--fg-muted);
	cursor: pointer;
}

.term-chip button:hover {
	background: var(--line);
	color: var(--fg);
}

.term-input {
	flex: 1;
	min-width: 140px;
	height: 24px;
	border: none;
	background: transparent;
	color: var(--fg);
	font: inherit;
	font-size: 0.875rem;
}

.term-input:focus {
	outline: none;
}
</style>
