<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useReferenceStore } from '~/stores/reference'
import { protocolLabel, protocolOptions } from '~/utils/settings-labels'
import type { components } from '~/types/api'

type DelayProfileResource = components['schemas']['DelayProfileResource']
type Protocol = components['schemas']['Protocol']

const api = useApi()
const reference = useReferenceStore()
const { toast } = useToast()
const { t } = useI18n()

const loading = ref(true)
const loadError = ref('')
const profiles = ref<DelayProfileResource[]>([])
const reordering = ref(false)

const editorOpen = ref(false)
const editingId = ref<number | null>(null)
const name = ref('')
const preferredProtocol = ref<Protocol>('USENET')
const enableUsenet = ref(true)
const enableTorrent = ref(true)
const usenetDelayMinutes = ref(0)
const torrentDelayMinutes = ref(0)
const bypassIfHighestQuality = ref(false)
const bypassIfAboveCustomFormatScore = ref(false)
const minimumCustomFormatScore = ref(0)
const tagIds = ref<number[]>([])
const nameError = ref('')
const saving = ref(false)

const deleteTarget = ref<DelayProfileResource | null>(null)
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
	const [result] = await Promise.all([api.GET('/api/v1/delay-profiles'), reference.load()])
	if (!result.data) {
		loadError.value = t('components.settings.DelayProfilesPanel.loadError')
	}
	profiles.value = result.data ?? []
	loading.value = false
}

void load()

async function reorder(next: DelayProfileResource[]) {
	profiles.value = next
	reordering.value = true
	const result = await api.PUT('/api/v1/delay-profiles/reorder', { body: next.map(p => p.id) })
	reordering.value = false
	if (!result.response.ok) {
		toast({ title: t('components.settings.DelayProfilesPanel.couldNotReorder'), description: toApiError(result.error, result.response).message, tone: 'danger' })
		await load()
	}
}

function openCreate() {
	editingId.value = null
	name.value = ''
	preferredProtocol.value = 'USENET'
	enableUsenet.value = true
	enableTorrent.value = true
	usenetDelayMinutes.value = 0
	torrentDelayMinutes.value = 0
	bypassIfHighestQuality.value = false
	bypassIfAboveCustomFormatScore.value = false
	minimumCustomFormatScore.value = 0
	tagIds.value = []
	nameError.value = ''
	editorOpen.value = true
}

function openEdit(profile: DelayProfileResource) {
	editingId.value = profile.id
	name.value = profile.name
	preferredProtocol.value = profile.preferredProtocol
	enableUsenet.value = profile.enableUsenet
	enableTorrent.value = profile.enableTorrent
	usenetDelayMinutes.value = profile.usenetDelayMinutes
	torrentDelayMinutes.value = profile.torrentDelayMinutes
	bypassIfHighestQuality.value = profile.bypassIfHighestQuality
	bypassIfAboveCustomFormatScore.value = profile.bypassIfAboveCustomFormatScore
	minimumCustomFormatScore.value = profile.minimumCustomFormatScore
	tagIds.value = [...profile.tags]
	nameError.value = ''
	editorOpen.value = true
}

async function save() {
	nameError.value = ''
	saving.value = true
	const body = {
		name: name.value,
		preferredProtocol: preferredProtocol.value,
		enableUsenet: enableUsenet.value,
		enableTorrent: enableTorrent.value,
		usenetDelayMinutes: usenetDelayMinutes.value,
		torrentDelayMinutes: torrentDelayMinutes.value,
		bypassIfHighestQuality: bypassIfHighestQuality.value,
		bypassIfAboveCustomFormatScore: bypassIfAboveCustomFormatScore.value,
		minimumCustomFormatScore: minimumCustomFormatScore.value,
		tags: tagIds.value,
	}
	try {
		const result = editingId.value === null
			? await api.POST('/api/v1/delay-profiles', { body })
			: await api.PUT('/api/v1/delay-profiles/{id}', { params: { path: { id: editingId.value } }, body })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: t('components.settings.DelayProfilesPanel.saved'), tone: 'ok' })
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

function confirmDelete(profile: DelayProfileResource) {
	deleteError.value = ''
	deleteTarget.value = profile
}

async function doDelete() {
	if (!deleteTarget.value) {
		return
	}
	deleting.value = true
	deleteError.value = ''
	const result = await api.DELETE('/api/v1/delay-profiles/{id}', { params: { path: { id: deleteTarget.value.id } } })
	if (!result.response.ok) {
		deleteError.value = toApiError(result.error, result.response).message
		deleting.value = false
		return
	}
	toast({ title: t('components.settings.DelayProfilesPanel.deleted'), tone: 'ok' })
	deleteTarget.value = null
	deleting.value = false
	await load()
}
</script>

<template>
	<div>
		<div class="panel-actions">
			<SButton
				variant="primary"
				@click="openCreate"
			>
				{{ $t('components.settings.DelayProfilesPanel.addDelayProfile') }}
			</SButton>
		</div>

		<SEmptyState
			v-if="loadError"
			:message="loadError"
			icon="lucide:alert-triangle"
		>
			<template #action>
				<SButton @click="load">
					{{ $t('components.settings.DelayProfilesPanel.retry') }}
				</SButton>
			</template>
		</SEmptyState>
		<SSpinner v-else-if="loading" />
		<template v-else-if="profiles.length > 0">
			<p class="hint">
				{{ $t('components.settings.DelayProfilesPanel.reorderHint') }}
			</p>
			<ReorderList
				:model-value="profiles"
				:item-key="(item) => item.id"
				@update:model-value="reorder"
			>
				<template #default="{ item }">
					<div class="delay-row">
						<span class="delay-name">{{ item.name }}</span>
						<span class="delay-detail">{{ t(protocolLabel(item.preferredProtocol)) }} {{ $t('components.settings.DelayProfilesPanel.preferred') }}</span>
						<span class="delay-detail">{{ $t('components.settings.DelayProfilesPanel.usenetDelay', { minutes: item.usenetDelayMinutes }) }}</span>
						<span class="delay-detail">{{ $t('components.settings.DelayProfilesPanel.torrentDelay', { minutes: item.torrentDelayMinutes }) }}</span>
						<div class="delay-actions">
							<SIconButton
								:label="t('components.settings.DelayProfilesPanel.editProfile')"
								@click="openEdit(item)"
							>
								<Icon
									name="lucide:pencil"
									aria-hidden="true"
								/>
							</SIconButton>
							<SIconButton
								:label="t('components.settings.DelayProfilesPanel.deleteProfile')"
								:disabled="item.name === 'Default'"
								@click="confirmDelete(item)"
							>
								<Icon
									name="lucide:trash-2"
									aria-hidden="true"
								/>
							</SIconButton>
						</div>
					</div>
				</template>
			</ReorderList>
		</template>
		<SEmptyState
			v-else
			:message="$t('components.settings.DelayProfilesPanel.emptyMessage')"
		>
			<template #action>
				<SButton
					variant="primary"
					@click="openCreate"
				>
					{{ $t('components.settings.DelayProfilesPanel.addDelayProfile') }}
				</SButton>
			</template>
		</SEmptyState>

		<SDialog
			v-model="editorOpen"
			:title="editingId === null ? $t('components.settings.DelayProfilesPanel.addDelayProfile') : $t('components.settings.DelayProfilesPanel.editDelayProfile')"
		>
			<SField
				:label="$t('components.settings.DelayProfilesPanel.name')"
				:error="nameError"
				control-id="delay-name"
			>
				<SInput
					id="delay-name"
					v-model="name"
					:invalid="!!nameError"
				/>
			</SField>
			<SField
				:label="$t('components.settings.DelayProfilesPanel.preferredProtocol')"
				control-id="delay-protocol"
			>
				<SSelect
					v-model="preferredProtocol"
					control-id="delay-protocol"
					:options="protocolOptions.map(option => ({ ...option, label: t(option.label) }))"
				/>
			</SField>
			<div class="field-grid">
				<SSwitch
					v-model="enableUsenet"
					:label="$t('components.settings.DelayProfilesPanel.enableUsenet')"
				/>
				<SSwitch
					v-model="enableTorrent"
					:label="$t('components.settings.DelayProfilesPanel.enableTorrent')"
				/>
			</div>
			<div class="field-grid">
				<SField
					:label="$t('components.settings.DelayProfilesPanel.usenetDelayMinutes')"
					control-id="delay-usenet"
				>
					<SInput
						id="delay-usenet"
						type="number"
						:model-value="String(usenetDelayMinutes)"
						@update:model-value="usenetDelayMinutes = Number($event) || 0"
					/>
				</SField>
				<SField
					:label="$t('components.settings.DelayProfilesPanel.torrentDelayMinutes')"
					control-id="delay-torrent"
				>
					<SInput
						id="delay-torrent"
						type="number"
						:model-value="String(torrentDelayMinutes)"
						@update:model-value="torrentDelayMinutes = Number($event) || 0"
					/>
				</SField>
			</div>
			<SSwitch
				v-model="bypassIfHighestQuality"
				:label="$t('components.settings.DelayProfilesPanel.bypassHighestQuality')"
			/>
			<SSwitch
				v-model="bypassIfAboveCustomFormatScore"
				:label="$t('components.settings.DelayProfilesPanel.bypassCustomFormatScore')"
			/>
			<SField
				v-if="bypassIfAboveCustomFormatScore"
				:label="$t('components.settings.DelayProfilesPanel.minimumCustomFormatScore')"
				control-id="delay-min-score"
			>
				<SInput
					id="delay-min-score"
					type="number"
					:model-value="String(minimumCustomFormatScore)"
					@update:model-value="minimumCustomFormatScore = Number($event) || 0"
				/>
			</SField>
			<SField :label="$t('components.settings.DelayProfilesPanel.tags')">
				<TagPicker v-model:tag-ids="tagIds" />
			</SField>
			<template #footer>
				<SButton
					variant="secondary"
					@click="editorOpen = false"
				>
					{{ $t('components.settings.DelayProfilesPanel.cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="saving"
					@click="save"
				>
					{{ $t('components.settings.DelayProfilesPanel.saveChanges') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteTargetOpen"
			:title="$t('components.settings.DelayProfilesPanel.deleteTitle')"
		>
			<p v-if="deleteTarget">
				{{ $t('components.settings.DelayProfilesPanel.confirmDelete', { name: deleteTarget.name }) }}
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
					{{ $t('components.settings.DelayProfilesPanel.cancel') }}
				</SButton>
				<SButton
					variant="danger"
					:loading="deleting"
					@click="doDelete"
				>
					{{ $t('components.settings.DelayProfilesPanel.delete') }}
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

.hint {
	font-size: 0.8125rem;
	color: var(--fg-muted);
	margin-bottom: 12px;
}

.delay-row {
	display: flex;
	align-items: center;
	gap: 16px;
	width: 100%;
}

.delay-name {
	font-weight: 500;
	flex: 1;
	min-width: 0;
}

.delay-detail {
	font-size: 0.8125rem;
	color: var(--fg-muted);
	flex: none;
}

.delay-actions {
	display: flex;
	gap: 2px;
	flex: none;
}

.field-grid {
	display: grid;
	grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
	gap: 16px;
}
</style>
