<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { toApiError, useApi } from '~/composables/useApi'
import { useReferenceStore } from '~/stores/reference'
import type { components } from '~/types/api'

type Rule = components['schemas']['AutoTaggingRuleDto']
type Specification = components['schemas']['AutoTaggingSpecification']
type SpecificationType = components['schemas']['AutoTaggingSpecificationType']

interface RuleForm {
	name: string
	enable: boolean
	removeTagsAutomatically: boolean
	specifications: Specification[]
	tagIds: number[]
}

const api = useApi()
const reference = useReferenceStore()
const { toast } = useToast()
const rules = ref<Rule[]>([])
const loading = ref(true)
const loadError = ref('')
const editorOpen = ref(false)
const editingId = ref<number | null>(null)
const form = ref<RuleForm>(blankForm())
const formError = ref('')
const saving = ref(false)
const deleteTarget = ref<Rule | null>(null)
const deleting = ref(false)
const deleteTargetOpen = computed({
	get: () => deleteTarget.value !== null,
	set: (open: boolean) => { if (!open) deleteTarget.value = null },
})

const { t } = useI18n()
const types: { value: SpecificationType, label: string }[] = [
	{ value: 'GENRE', label: t('components.settings.AutoTaggingRulesPanel.types.genre') },
	{ value: 'ROOT_FOLDER', label: t('components.settings.AutoTaggingRulesPanel.types.rootFolder') },
	{ value: 'SERIES_TYPE', label: t('components.settings.AutoTaggingRulesPanel.types.seriesType') },
	{ value: 'STATUS', label: t('components.settings.AutoTaggingRulesPanel.types.status') },
	{ value: 'YEAR', label: t('components.settings.AutoTaggingRulesPanel.types.year') },
	{ value: 'QUALITY_PROFILE', label: t('components.settings.AutoTaggingRulesPanel.types.qualityProfile') },
	{ value: 'MONITORED', label: t('components.settings.AutoTaggingRulesPanel.types.monitored') },
	{ value: 'NETWORK_OR_STUDIO', label: t('components.settings.AutoTaggingRulesPanel.types.networkOrStudio') },
	{ value: 'ORIGINAL_LANGUAGE', label: t('components.settings.AutoTaggingRulesPanel.types.originalLanguage') },
	{ value: 'KEYWORD', label: t('components.settings.AutoTaggingRulesPanel.types.keyword') },
]

function blankForm(): RuleForm {
	return { name: '', enable: true, removeTagsAutomatically: false, specifications: [], tagIds: [] }
}

function defaultValue(type: SpecificationType): unknown {
	switch (type) {
		case 'GENRE':
		case 'NETWORK_OR_STUDIO':
		case 'ORIGINAL_LANGUAGE':
		case 'KEYWORD': return []
		case 'MONITORED': return true
		default: return ''
	}
}

function addSpecification() {
	form.value.specifications.push({ name: '', type: 'GENRE', negate: false, required: true, value: defaultValue('GENRE') })
}

function changeType(index: number, type: SpecificationType) {
	const specification = form.value.specifications[index]
	if (specification) {
		form.value.specifications[index] = { ...specification, type, value: defaultValue(type) as Specification['value'] }
	}
}

function valueText(value: unknown): string {
	if (Array.isArray(value)) return value.join(', ')
	return value == null ? '' : String(value)
}

function setTextValue(specification: Specification, input: string) {
	if (['GENRE', 'NETWORK_OR_STUDIO', 'ORIGINAL_LANGUAGE', 'KEYWORD'].includes(specification.type)) {
		specification.value = input.split(',').map(value => value.trim()).filter(Boolean)
	}
	else if (specification.type === 'ROOT_FOLDER' || specification.type === 'QUALITY_PROFILE') {
		specification.value = input === '' ? null : Number(input)
	}
	else {
		specification.value = input
	}
}

function openCreate() {
	form.value = blankForm()
	editingId.value = null
	formError.value = ''
	editorOpen.value = true
}

function openEdit(rule: Rule) {
	form.value = {
		name: rule.name,
		enable: rule.enable,
		removeTagsAutomatically: rule.removeTagsAutomatically,
		specifications: rule.specifications.map(specification => ({ ...specification })),
		tagIds: reference.tags.filter(tag => rule.tags.includes(tag.label)).map(tag => tag.id),
	}
	editingId.value = rule.id
	formError.value = ''
	editorOpen.value = true
}

async function load() {
	loading.value = true
	loadError.value = ''
	const result = await api.GET('/api/v1/auto-tagging')
	if (!result.data) loadError.value = t('components.settings.AutoTaggingRulesPanel.loadError')
	rules.value = result.data ?? []
	loading.value = false
}

async function save() {
	formError.value = ''
	saving.value = true
	const body = {
		name: form.value.name,
		enable: form.value.enable,
		removeTagsAutomatically: form.value.removeTagsAutomatically,
		specifications: form.value.specifications,
		tags: form.value.tagIds.map(id => reference.tags.find(tag => tag.id === id)?.label).filter((label): label is string => !!label),
	}
	const result = editingId.value === null
		? await api.POST('/api/v1/auto-tagging', { body })
		: await api.PUT('/api/v1/auto-tagging/{id}', { params: { path: { id: editingId.value } }, body })
	saving.value = false
	if (!result.data) {
		formError.value = toApiError(result.error, result.response).message
		return
	}
	toast({ title: t(editingId.value === null ? 'components.settings.AutoTaggingRulesPanel.added' : 'components.settings.AutoTaggingRulesPanel.saved'), tone: 'ok' })
	editorOpen.value = false
	await load()
}

async function removeRule() {
	if (!deleteTarget.value) return
	deleting.value = true
	const result = await api.DELETE('/api/v1/auto-tagging/{id}', { params: { path: { id: deleteTarget.value.id } } })
	deleting.value = false
	if (!result.response.ok) {
		toast({ title: t('components.settings.AutoTaggingRulesPanel.couldNotDelete'), description: toApiError(result.error, result.response).message, tone: 'danger' })
		return
	}
	toast({ title: t('components.settings.AutoTaggingRulesPanel.deleted'), tone: 'ok' })
	deleteTarget.value = null
	await load()
}

void Promise.all([reference.load(), load()])
</script>

<template>
	<SSection :title="$t('components.settings.AutoTaggingRulesPanel.title')">
		<div class="section-actions">
			<p class="section-copy">
				{{ $t('components.settings.AutoTaggingRulesPanel.description') }}
			</p>
			<SButton
				variant="secondary"
				@click="openCreate"
			>
				{{ $t('components.settings.AutoTaggingRulesPanel.addRule') }}
			</SButton>
		</div>
		<SEmptyState
			v-if="loadError"
			:message="loadError"
			icon="lucide:alert-triangle"
		>
			<template #action>
				<SButton @click="load">
					{{ $t('components.settings.AutoTaggingRulesPanel.retry') }}
				</SButton>
			</template>
		</SEmptyState>
		<SSpinner v-else-if="loading" />
		<SEmptyState
			v-else-if="rules.length === 0"
			:message="$t('components.settings.AutoTaggingRulesPanel.emptyMessage')"
		/>
		<div
			v-else
			class="rule-list"
		>
			<article
				v-for="rule in rules"
				:key="rule.id"
				class="rule-row"
			>
				<div class="rule-details">
					<div class="rule-heading">
						<h3>{{ rule.name }}</h3>
						<SBadge :tone="rule.enable ? 'ok' : 'neutral'">
							{{ rule.enable ? $t('components.settings.AutoTaggingRulesPanel.enabled') : $t('components.settings.AutoTaggingRulesPanel.disabled') }}
						</SBadge>
					</div>
					<p>
						{{ t('components.settings.AutoTaggingRulesPanel.specificationCount', { count: rule.specifications.length }, rule.specifications.length) }}
						· {{ rule.tags.length ? rule.tags.join(', ') : t('components.settings.AutoTaggingRulesPanel.noTags') }}
					</p>
					<p
						v-if="rule.removeTagsAutomatically"
						class="rule-note"
					>
						{{ $t('components.settings.AutoTaggingRulesPanel.removeTagsNote') }}
					</p>
				</div>
				<div class="row-actions">
					<SButton
						variant="secondary"
						@click="openEdit(rule)"
					>
						{{ $t('components.settings.AutoTaggingRulesPanel.edit') }}
					</SButton>
					<SIconButton
						:label="$t('components.settings.AutoTaggingRulesPanel.deleteRule')"
						@click="deleteTarget = rule"
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
		:title="editingId === null ? $t('components.settings.AutoTaggingRulesPanel.addRuleTitle') : $t('components.settings.AutoTaggingRulesPanel.editRuleTitle')"
		wide
	>
		<SField
			:label="$t('components.settings.AutoTaggingRulesPanel.name')"
			control-id="auto-tagging-name"
			:error="formError"
		>
			<SInput
				id="auto-tagging-name"
				v-model="form.name"
				:invalid="!!formError"
			/>
		</SField>
		<div class="rule-switches">
			<SSwitch
				v-model="form.enable"
				:label="$t('components.settings.AutoTaggingRulesPanel.enableRule')"
			/>
			<SSwitch
				v-model="form.removeTagsAutomatically"
				:label="$t('components.settings.AutoTaggingRulesPanel.removeTagsAutomatically')"
			/>
		</div>
		<SSection :title="$t('components.settings.AutoTaggingRulesPanel.specifications')">
			<p class="section-copy">
				{{ $t('components.settings.AutoTaggingRulesPanel.specificationsHint') }}
			</p>
			<div class="spec-list">
				<div
					v-for="(specification, index) in form.specifications"
					:key="index"
					class="spec-row"
				>
					<SInput
						v-model="specification.name"
						class="spec-name"
						:aria-label="$t('components.settings.AutoTaggingRulesPanel.specificationName')"
						:placeholder="$t('components.settings.AutoTaggingRulesPanel.name')"
					/>
					<SSelect
						:model-value="specification.type"
						:options="types"
						@update:model-value="changeType(index, $event as SpecificationType)"
					/>
					<template v-if="specification.type === 'ROOT_FOLDER'">
						<SSelect
							:model-value="String(specification.value ?? '')"
							:options="reference.rootFolders.map(folder => ({ value: String(folder.id), label: folder.path }))"
							@update:model-value="setTextValue(specification, $event ?? '')"
						/>
					</template>
					<template v-else-if="specification.type === 'QUALITY_PROFILE'">
						<SSelect
							:model-value="String(specification.value ?? '')"
							:options="reference.qualityProfiles.map(profile => ({ value: String(profile.id), label: profile.name }))"
							@update:model-value="setTextValue(specification, $event ?? '')"
						/>
					</template>
					<SCheckbox
						v-else-if="specification.type === 'MONITORED'"
						:model-value="specification.value !== false"
						:label="$t('components.settings.AutoTaggingRulesPanel.monitored')"
						@update:model-value="specification.value = $event"
					/>
					<div
						v-else-if="specification.type === 'YEAR'"
						class="year-range"
					>
						<SInput
							type="number"
							:placeholder="$t('components.settings.AutoTaggingRulesPanel.from')"
							:model-value="String((specification.value as { min?: number | null } | null)?.min ?? '')"
							@update:model-value="specification.value = { ...(specification.value as object), min: $event === '' ? null : Number($event) }"
						/>
						<SInput
							type="number"
							:placeholder="$t('components.settings.AutoTaggingRulesPanel.to')"
							:model-value="String((specification.value as { max?: number | null } | null)?.max ?? '')"
							@update:model-value="specification.value = { ...(specification.value as object), max: $event === '' ? null : Number($event) }"
						/>
					</div>
					<SInput
						v-else
						:model-value="valueText(specification.value)"
						:placeholder="['GENRE', 'NETWORK_OR_STUDIO', 'ORIGINAL_LANGUAGE', 'KEYWORD'].includes(specification.type) ? $t('components.settings.AutoTaggingRulesPanel.commaSeparatedValues') : $t('components.settings.AutoTaggingRulesPanel.matchValue')"
						@update:model-value="setTextValue(specification, $event)"
					/>
					<SCheckbox
						v-model="specification.negate"
						:label="$t('components.settings.AutoTaggingRulesPanel.negate')"
					/>
					<SCheckbox
						v-model="specification.required"
						:label="$t('components.settings.AutoTaggingRulesPanel.required')"
					/>
					<SIconButton
						:label="$t('components.settings.AutoTaggingRulesPanel.removeSpecification')"
						@click="form.specifications.splice(index, 1)"
					>
						<Icon
							name="lucide:x"
							aria-hidden="true"
						/>
					</SIconButton>
				</div>
			</div>
			<SButton
				variant="secondary"
				@click="addSpecification"
			>
				{{ $t('components.settings.AutoTaggingRulesPanel.addSpecification') }}
			</SButton>
		</SSection>
		<SField :label="$t('components.settings.AutoTaggingRulesPanel.tags')">
			<TagPicker v-model:tag-ids="form.tagIds" />
		</SField>
		<template #footer>
			<SButton
				variant="secondary"
				@click="editorOpen = false"
			>
				{{ $t('components.settings.AutoTaggingRulesPanel.cancel') }}
			</SButton>
			<SButton
				variant="primary"
				:loading="saving"
				@click="save"
			>
				{{ $t('components.settings.AutoTaggingRulesPanel.saveChanges') }}
			</SButton>
		</template>
	</SDialog>

	<SDialog
		v-model="deleteTargetOpen"
		:title="$t('components.settings.AutoTaggingRulesPanel.deleteRuleTitle')"
	>
		<p v-if="deleteTarget">
			{{ $t('components.settings.AutoTaggingRulesPanel.confirmDelete', { name: deleteTarget.name }) }}
		</p>
		<template #footer>
			<SButton
				variant="secondary"
				@click="deleteTarget = null"
			>
				{{ $t('components.settings.AutoTaggingRulesPanel.cancel') }}
			</SButton>
			<SButton
				variant="danger"
				:loading="deleting"
				@click="removeRule"
			>
				{{ $t('components.settings.AutoTaggingRulesPanel.delete') }}
			</SButton>
		</template>
	</SDialog>
</template>

<style scoped>
.section-copy {
	margin: 0;
	color: var(--fg-muted);
	font-size: 0.875rem;
}

.section-actions {
	display: flex;
	align-items: center;
	justify-content: space-between;
	gap: 16px;
	margin-bottom: 16px;
}

.rule-list {
	display: grid;
}

.rule-row {
	display: flex;
	align-items: center;
	justify-content: space-between;
	gap: 16px;
	padding: 14px 0;
	border-bottom: 1px solid var(--line);
}

.rule-row:first-child {
	padding-top: 0;
}

.rule-heading,
.row-actions {
	display: flex;
	align-items: center;
	gap: 10px;
}

.rule-heading h3 {
	margin: 0;
	font-size: 0.95rem;
	font-weight: 500;
}

.rule-details p {
	margin: 4px 0 0;
	color: var(--fg-muted);
	font-size: 0.8125rem;
}

.rule-details .rule-note {
	color: var(--accent);
}

.rule-switches {
	display: grid;
	gap: 12px;
}

.spec-list {
	display: grid;
	gap: 8px;
	margin-bottom: 12px;
}

.spec-row {
	display: grid;
	grid-template-columns: minmax(110px, 1fr) minmax(130px, 1fr) minmax(150px, 1.2fr) auto auto auto;
	align-items: center;
	gap: 8px;
}

.year-range {
	display: grid;
	grid-template-columns: 1fr 1fr;
	gap: 8px;
}

@media (max-width: 900px) {
	.spec-row {
		grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
	}

	.spec-row > :nth-child(n + 3) {
		min-width: 0;
	}
}

@media (max-width: 600px) {
	.section-actions,
	.rule-row {
		align-items: flex-start;
	}

	.section-actions {
		flex-direction: column;
	}

	.rule-heading {
		align-items: flex-start;
		flex-direction: column;
	}
}
</style>
