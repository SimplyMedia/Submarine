<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useReferenceStore } from '~/stores/reference'
import { useSettingsStore } from '~/stores/settings'
import type { components } from '~/types/api'

type QualityProfileResource = components['schemas']['QualityProfileResource']
type QualityProfileItemResource = components['schemas']['QualityProfileItemResource']
type FormatItemResource = components['schemas']['FormatItemResource']

const api = useApi()
const reference = useReferenceStore()
const settings = useSettingsStore()
const { toast } = useToast()
const { t } = useI18n()

const TEMPLATE_NAMES = ['Any', 'SD', 'HD-720p', 'HD-1080p', 'Ultra-HD', 'HD - 720p/1080p', 'Remux-1080p', 'Remux-2160p']
const templateLabels: Record<string, string> = {
	'Any': 'components.settings.QualityProfilesPanel.templates.any',
	'SD': 'components.settings.QualityProfilesPanel.templates.sd',
	'HD-720p': 'components.settings.QualityProfilesPanel.templates.hd720p',
	'HD-1080p': 'components.settings.QualityProfilesPanel.templates.hd1080p',
	'Ultra-HD': 'components.settings.QualityProfilesPanel.templates.ultraHd',
	'HD - 720p/1080p': 'components.settings.QualityProfilesPanel.templates.hd720p1080p',
	'Remux-1080p': 'components.settings.QualityProfilesPanel.templates.remux1080p',
	'Remux-2160p': 'components.settings.QualityProfilesPanel.templates.remux2160p',
}

const loading = ref(true)
const loadError = computed(() => reference.loadError)
const templateDialogOpen = ref(false)
const creatingTemplate = ref(false)

const editorOpen = ref(false)
const editing = ref<QualityProfileResource | null>(null)
const name = ref('')
const upgradeAllowed = ref(true)
const items = ref<QualityProfileItemResource[]>([])
const cutoffKey = ref('')
const minFormatScore = ref(0)
const cutoffFormatScore = ref(0)
const minUpgradeFormatScore = ref(0)
const formatScores = ref<Record<number, number>>({})
const nameError = ref('')
const saving = ref(false)

const deleteTarget = ref<QualityProfileResource | null>(null)
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

function qualityKey(quality: { source?: string | null, resolution?: string | null }) {
	return `${quality.source ?? ''}|${quality.resolution ?? ''}`
}

async function load() {
	loading.value = true
	await Promise.all([reference.load(), settings.ensureCustomFormats()])
	loading.value = false
}

void load()

function openTemplateDialog() {
	templateDialogOpen.value = true
}

async function createFromTemplate(templateName: string) {
	creatingTemplate.value = true
	const result = await api.POST('/api/v1/quality-profiles/from-template/{name}', { params: { path: { name: templateName } } })
	creatingTemplate.value = false
	if (!result.data) {
		toast({ title: t('components.settings.QualityProfilesPanel.couldNotCreate'), description: toApiError(result.error, result.response).message, tone: 'danger' })
		return
	}
	toast({ title: t('components.settings.QualityProfilesPanel.created', { name: result.data.name }), tone: 'ok' })
	templateDialogOpen.value = false
	await reference.load(true)
}

async function cloneProfile(profile: QualityProfileResource) {
	const result = await api.POST('/api/v1/quality-profiles/{id}/clone', { params: { path: { id: profile.id } } })
	if (!result.data) {
		toast({ title: t('components.settings.QualityProfilesPanel.couldNotClone'), description: toApiError(result.error, result.response).message, tone: 'danger' })
		return
	}
	toast({ title: t('components.settings.QualityProfilesPanel.created', { name: result.data.name }), tone: 'ok' })
	await reference.load(true)
}

function openEdit(profile: QualityProfileResource) {
	editing.value = profile
	name.value = profile.name
	upgradeAllowed.value = profile.upgradeAllowed
	items.value = profile.items.map(item => ({ ...item }))
	cutoffKey.value = qualityKey(profile.items[profile.cutoff]?.quality ?? {})
	minFormatScore.value = profile.minFormatScore
	cutoffFormatScore.value = profile.cutoffFormatScore
	minUpgradeFormatScore.value = profile.minUpgradeFormatScore
	const scores: Record<number, number> = {}
	for (const item of profile.formatItems) {
		scores[item.customFormatId] = item.score
	}
	formatScores.value = scores
	nameError.value = ''
	editorOpen.value = true
}

const cutoffOptions = computed(() => items.value
	.filter(item => item.allowed)
	.map(item => ({ value: qualityKey(item.quality), label: item.quality.name })))

async function save() {
	if (!editing.value) {
		return
	}
	nameError.value = ''
	saving.value = true
	const cutoffIndex = items.value.findIndex(item => qualityKey(item.quality) === cutoffKey.value)
	const formatItems: FormatItemResource[] = Object.entries(formatScores.value)
		.filter(([, score]) => score !== 0)
		.map(([customFormatId, score]) => ({ customFormatId: Number(customFormatId), score }))
	try {
		const result = await api.PUT('/api/v1/quality-profiles/{id}', {
			params: { path: { id: editing.value.id } },
			body: {
				name: name.value,
				upgradeAllowed: upgradeAllowed.value,
				cutoff: cutoffIndex < 0 ? 0 : cutoffIndex,
				items: items.value,
				formatItems,
				minFormatScore: minFormatScore.value,
				cutoffFormatScore: cutoffFormatScore.value,
				minUpgradeFormatScore: minUpgradeFormatScore.value,
			},
		})
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: t('components.settings.QualityProfilesPanel.saved'), tone: 'ok' })
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

function confirmDelete(profile: QualityProfileResource) {
	deleteError.value = ''
	deleteTarget.value = profile
}

async function doDelete() {
	if (!deleteTarget.value) {
		return
	}
	deleting.value = true
	deleteError.value = ''
	const result = await api.DELETE('/api/v1/quality-profiles/{id}', { params: { path: { id: deleteTarget.value.id } } })
	if (!result.response.ok) {
		deleteError.value = toApiError(result.error, result.response).message
		deleting.value = false
		return
	}
	toast({ title: t('components.settings.QualityProfilesPanel.deleted'), tone: 'ok' })
	deleteTarget.value = null
	deleting.value = false
	await reference.load(true)
}

const columns = [
	{ key: 'name', label: t('components.settings.QualityProfilesPanel.name') },
	{ key: 'qualities', label: t('components.settings.QualityProfilesPanel.qualities') },
	{ key: 'formats', label: t('components.settings.QualityProfilesPanel.customFormats') },
	{ key: 'actions', label: '', align: 'right' as const },
]
</script>

<template>
	<div>
		<div class="panel-actions">
			<SButton
				variant="primary"
				@click="openTemplateDialog"
			>
				{{ $t('components.settings.QualityProfilesPanel.newFromTemplate') }}
			</SButton>
		</div>

		<SEmptyState
			v-if="loadError"
			:message="loadError"
			icon="lucide:alert-triangle"
		>
			<template #action>
				<SButton @click="load">
					{{ $t('components.settings.QualityProfilesPanel.retry') }}
				</SButton>
			</template>
		</SEmptyState>
		<SSpinner v-else-if="loading" />
		<STable
			v-else
			:columns="columns"
			:rows="reference.qualityProfiles"
			:row-key="(row) => row.id"
		>
			<template #cell-qualities="{ row }">
				{{ $t('components.settings.QualityProfilesPanel.qualityCount', { allowed: row.items.filter(i => i.allowed).length, total: row.items.length }) }}
			</template>
			<template #cell-formats="{ row }">
				{{ row.formatItems.length }}
			</template>
			<template #cell-actions="{ row }">
				<SDropdownMenu
					:items="[
						{ label: t('components.settings.QualityProfilesPanel.edit'), icon: 'lucide:pencil', onSelect: () => openEdit(row) },
						{ label: t('components.settings.QualityProfilesPanel.clone'), icon: 'lucide:copy', onSelect: () => cloneProfile(row) },
						{ label: t('components.settings.QualityProfilesPanel.delete'), icon: 'lucide:trash-2', danger: true, onSelect: () => confirmDelete(row) },
					]"
				>
					<template #trigger>
						<SIconButton :label="$t('components.settings.QualityProfilesPanel.profileActions')">
							<Icon
								name="lucide:more-horizontal"
								aria-hidden="true"
							/>
						</SIconButton>
					</template>
				</SDropdownMenu>
			</template>
			<template #empty>
				<SEmptyState :message="$t('components.settings.QualityProfilesPanel.emptyMessage')">
					<template #action>
						<SButton
							variant="primary"
							@click="openTemplateDialog"
						>
							{{ $t('components.settings.QualityProfilesPanel.newFromTemplate') }}
						</SButton>
					</template>
				</SEmptyState>
			</template>
		</STable>

		<SDialog
			v-model="templateDialogOpen"
			:title="$t('components.settings.QualityProfilesPanel.newQualityProfile')"
			:description="$t('components.settings.QualityProfilesPanel.templateDescription')"
		>
			<div class="template-grid">
				<SButton
					v-for="templateName in TEMPLATE_NAMES"
					:key="templateName"
					variant="secondary"
					:disabled="creatingTemplate"
					@click="createFromTemplate(templateName)"
				>
					{{ t(templateLabels[templateName]!) }}
				</SButton>
			</div>
		</SDialog>

		<SDialog
			v-model="editorOpen"
			:title="$t('components.settings.QualityProfilesPanel.editQualityProfile')"
			wide
		>
			<template v-if="editing">
				<SField
					:label="$t('components.settings.QualityProfilesPanel.name')"
					:error="nameError"
					control-id="profile-name"
				>
					<SInput
						id="profile-name"
						v-model="name"
						:invalid="!!nameError"
					/>
				</SField>
				<SSwitch
					v-model="upgradeAllowed"
					:label="$t('components.settings.QualityProfilesPanel.allowUpgrades')"
				/>
				<SField
					:label="$t('components.settings.QualityProfilesPanel.upgradeUntil')"
					:hint="$t('components.settings.QualityProfilesPanel.upgradeUntilHint')"
					control-id="profile-cutoff"
				>
					<SSelect
						v-model="cutoffKey"
						control-id="profile-cutoff"
						:options="cutoffOptions"
					/>
				</SField>

				<SSection :title="$t('components.settings.QualityProfilesPanel.qualities')">
					<p class="hint">
						{{ $t('components.settings.QualityProfilesPanel.reorderQualitiesHint') }}
					</p>
					<ReorderList
						v-model="items"
						:item-key="(item) => qualityKey(item.quality)"
					>
						<template #default="{ item }">
							<div class="quality-row">
								<SCheckbox
									:model-value="item.allowed"
									@update:model-value="item.allowed = $event"
								/>
								<span>{{ item.quality.name }}</span>
								<SBadge
									v-if="qualityKey(item.quality) === cutoffKey"
									tone="info"
								>
									{{ $t('components.settings.QualityProfilesPanel.cutoff') }}
								</SBadge>
							</div>
						</template>
					</ReorderList>
				</SSection>

				<SSection :title="$t('components.settings.QualityProfilesPanel.customFormatScores')">
					<div class="format-scores">
						<div
							v-for="format in settings.customFormats"
							:key="format.id"
							class="format-score-row"
						>
							<span>{{ format.name }}</span>
							<ScoreInput
								:model-value="formatScores[format.id] ?? 0"
								:aria-label="t('components.settings.QualityProfilesPanel.scoreForFormat', { name: format.name })"
								@update:model-value="formatScores[format.id] = $event"
							/>
						</div>
						<p
							v-if="settings.customFormats.length === 0"
							class="hint"
						>
							{{ $t('components.settings.QualityProfilesPanel.noCustomFormats') }}
						</p>
					</div>
					<div class="field-grid">
						<SField
							:label="$t('components.settings.QualityProfilesPanel.minimumScoreToGrab')"
							control-id="min-format-score"
						>
							<SInput
								id="min-format-score"
								type="number"
								:model-value="String(minFormatScore)"
								@update:model-value="minFormatScore = Number($event) || 0"
							/>
						</SField>
						<SField
							:label="$t('components.settings.QualityProfilesPanel.scoreToUpgradePastCutoff')"
							control-id="cutoff-format-score"
						>
							<SInput
								id="cutoff-format-score"
								type="number"
								:model-value="String(cutoffFormatScore)"
								@update:model-value="cutoffFormatScore = Number($event) || 0"
							/>
						</SField>
						<SField
							:label="$t('components.settings.QualityProfilesPanel.minimumScoreForFurtherUpgrades')"
							control-id="min-upgrade-format-score"
						>
							<SInput
								id="min-upgrade-format-score"
								type="number"
								:model-value="String(minUpgradeFormatScore)"
								@update:model-value="minUpgradeFormatScore = Number($event) || 0"
							/>
						</SField>
					</div>
				</SSection>
			</template>
			<template #footer>
				<SButton
					variant="secondary"
					@click="editorOpen = false"
				>
					{{ $t('components.settings.QualityProfilesPanel.cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="saving"
					@click="save"
				>
					{{ $t('components.settings.QualityProfilesPanel.saveChanges') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteTargetOpen"
			:title="$t('components.settings.QualityProfilesPanel.deleteTitle')"
		>
			<p v-if="deleteTarget">
				{{ $t('components.settings.QualityProfilesPanel.confirmDelete', { name: deleteTarget.name }) }}
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
					{{ $t('components.settings.QualityProfilesPanel.cancel') }}
				</SButton>
				<SButton
					variant="danger"
					:loading="deleting"
					@click="doDelete"
				>
					{{ $t('components.settings.QualityProfilesPanel.delete') }}
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

.template-grid {
	display: grid;
	grid-template-columns: repeat(2, 1fr);
	gap: 8px;
}

.hint {
	font-size: 0.8125rem;
	color: var(--fg-muted);
	margin-bottom: 12px;
}

.quality-row {
	display: flex;
	align-items: center;
	gap: 12px;
}

.format-scores {
	display: flex;
	flex-direction: column;
	gap: 8px;
	margin-bottom: 16px;
}

.format-score-row {
	display: flex;
	align-items: center;
	justify-content: space-between;
	gap: 12px;
	height: 36px;
}

.field-grid {
	display: grid;
	grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
	gap: 16px;
}
</style>
