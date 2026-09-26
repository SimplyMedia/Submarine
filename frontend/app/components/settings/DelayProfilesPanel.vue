<script setup lang="ts">
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useReferenceStore } from '~/stores/reference'
import { protocolLabel, protocolOptions } from '~/utils/settings-labels'
import type { components } from '~/types/api'

type DelayProfileResource = components['schemas']['DelayProfileResource']
type Protocol = components['schemas']['Protocol']

const api = useApi()
const reference = useReferenceStore()
const { toast } = useToast()

const loading = ref(true)
const loadError = ref('')
const profiles = ref<DelayProfileResource[]>([])
const reordering = ref(false)

const editorOpen = ref(false)
const editingId = ref<number | null>(null)
const name = ref('')
const preferredProtocol = ref<Protocol>('USENET')
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
		loadError.value = 'Could not load delay profiles. Check your connection and try again.'
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
		toast({ title: 'Could not reorder', description: toApiError(result.error, result.response).message, tone: 'danger' })
		await load()
	}
}

function openCreate() {
	editingId.value = null
	name.value = ''
	preferredProtocol.value = 'USENET'
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
		toast({ title: 'Saved', tone: 'ok' })
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
	toast({ title: 'Delay profile deleted', tone: 'ok' })
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
				Add delay profile
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
		<template v-else-if="profiles.length > 0">
			<p class="hint">
				Earlier profiles match first. Reorder to change priority.
			</p>
			<ReorderList
				:model-value="profiles"
				:item-key="(item) => item.id"
				@update:model-value="reorder"
			>
				<template #default="{ item }">
					<div class="delay-row">
						<span class="delay-name">{{ item.name }}</span>
						<span class="delay-detail">{{ protocolLabel(item.preferredProtocol) }} preferred</span>
						<span class="delay-detail">Usenet {{ item.usenetDelayMinutes }}m</span>
						<span class="delay-detail">Torrent {{ item.torrentDelayMinutes }}m</span>
						<div class="delay-actions">
							<SIconButton
								label="Edit delay profile"
								@click="openEdit(item)"
							>
								<Icon
									name="lucide:pencil"
									aria-hidden="true"
								/>
							</SIconButton>
							<SIconButton
								label="Delete delay profile"
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
			message="No delay profiles yet. Add one to hold back grabs for a preferred protocol."
		>
			<template #action>
				<SButton
					variant="primary"
					@click="openCreate"
				>
					Add delay profile
				</SButton>
			</template>
		</SEmptyState>

		<SDialog
			v-model="editorOpen"
			:title="editingId === null ? 'Add delay profile' : 'Edit delay profile'"
		>
			<SField
				label="Name"
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
				label="Preferred protocol"
				control-id="delay-protocol"
			>
				<SSelect
					v-model="preferredProtocol"
					control-id="delay-protocol"
					:options="protocolOptions"
				/>
			</SField>
			<div class="field-grid">
				<SField
					label="Usenet delay (minutes)"
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
					label="Torrent delay (minutes)"
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
				label="Bypass delay when already at the highest allowed quality"
			/>
			<SSwitch
				v-model="bypassIfAboveCustomFormatScore"
				label="Bypass delay above a custom format score"
			/>
			<SField
				v-if="bypassIfAboveCustomFormatScore"
				label="Minimum custom format score"
				control-id="delay-min-score"
			>
				<SInput
					id="delay-min-score"
					type="number"
					:model-value="String(minimumCustomFormatScore)"
					@update:model-value="minimumCustomFormatScore = Number($event) || 0"
				/>
			</SField>
			<SField label="Tags">
				<TagPicker v-model:tag-ids="tagIds" />
			</SField>
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
			title="Delete delay profile"
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
