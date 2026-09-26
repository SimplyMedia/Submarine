<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useReferenceStore } from '~/stores/reference'
import type { components } from '~/types/api'

type TagDetailDto = components['schemas']['TagDetailDto']

definePageMeta({ layout: 'default' })
const { t } = useI18n()
useHead({ title: t('pages.settings.tags.title') })

const api = useApi()
const reference = useReferenceStore()
const { toast } = useToast()

const details = ref<Record<number, TagDetailDto>>({})
const loading = ref(true)
const loadError = computed(() => reference.loadError)

const dialogOpen = ref(false)
const editingId = ref<number | null>(null)
const label = ref('')
const labelError = ref('')
const saving = ref(false)

const deleteTarget = ref<{ id: number, label: string, usage: number } | null>(null)
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

function usageOf(id: number): number {
	const detail = details.value[id]
	if (!detail) {
		return 0
	}
	return detail.seriesIds.length + detail.movieIds.length + detail.indexerIds.length
		+ detail.notificationIds.length + detail.delayProfileIds.length + detail.releaseProfileIds.length
		+ detail.importListIds.length
}

async function loadDetails() {
	const results = await Promise.all(reference.tags.map(tag => api.GET('/api/v1/tags/{id}/detail', { params: { path: { id: tag.id } } })))
	const map: Record<number, TagDetailDto> = {}
	results.forEach((result, index) => {
		if (result.data) {
			map[reference.tags[index]!.id] = result.data
		}
	})
	details.value = map
}

async function load() {
	loading.value = true
	await reference.load(true)
	await loadDetails()
	loading.value = false
}

void load()

function openCreate() {
	editingId.value = null
	label.value = ''
	labelError.value = ''
	dialogOpen.value = true
}

function openEdit(tag: { id: number, label: string }) {
	editingId.value = tag.id
	label.value = tag.label
	labelError.value = ''
	dialogOpen.value = true
}

async function save() {
	labelError.value = ''
	saving.value = true
	try {
		if (editingId.value === null) {
			const result = await api.POST('/api/v1/tags', { body: { label: label.value } })
			if (!result.data) {
				throw toApiError(result.error, result.response)
			}
			toast({ title: t('pages.settings.tags.created'), tone: 'ok' })
		}
		else {
			const result = await api.PUT('/api/v1/tags/{id}', { params: { path: { id: editingId.value } }, body: { label: label.value } })
			if (!result.data) {
				throw toApiError(result.error, result.response)
			}
			toast({ title: t('pages.settings.tags.renamed'), tone: 'ok' })
		}
		dialogOpen.value = false
		await load()
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		labelError.value = apiError.fieldErrors.label?.[0] ?? apiError.message
	}
	finally {
		saving.value = false
	}
}

function confirmDelete(tag: { id: number, label: string }) {
	deleteError.value = ''
	deleteTarget.value = { id: tag.id, label: tag.label, usage: usageOf(tag.id) }
}

async function doDelete() {
	if (!deleteTarget.value) {
		return
	}
	deleting.value = true
	deleteError.value = ''
	const result = await api.DELETE('/api/v1/tags/{id}', { params: { path: { id: deleteTarget.value.id } } })
	if (!result.response.ok) {
		deleteError.value = toApiError(result.error, result.response).message
		deleting.value = false
		return
	}
	toast({ title: t('pages.settings.tags.deleted'), tone: 'ok' })
	deleteTarget.value = null
	deleting.value = false
	await load()
}

const columns = [
	{ key: 'label', label: t('pages.settings.tags.label') },
	{ key: 'usage', label: t('pages.settings.tags.usedBy'), align: 'right' as const },
	{ key: 'actions', label: '', align: 'right' as const },
]
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.settings.tags.title')">
			<template #actions>
				<SButton
					variant="primary"
					@click="openCreate"
				>
					{{ t('pages.settings.tags.addTag') }}
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
						{{ t('pages.settings.tags.retry') }}
					</SButton>
				</template>
			</SEmptyState>
			<SSpinner v-else-if="loading" />
			<STable
				v-else
				:columns="columns"
				:rows="reference.tags"
				:row-key="(row) => row.id"
			>
				<template #cell-usage="{ row }">
					{{ usageOf(row.id) }}
				</template>
				<template #cell-actions="{ row }">
					<SDropdownMenu
						:items="[
							{ label: t('pages.settings.tags.rename'), icon: 'lucide:pencil', onSelect: () => openEdit(row) },
							{ label: t('pages.settings.tags.delete'), icon: 'lucide:trash-2', danger: true, onSelect: () => confirmDelete(row) },
						]"
					>
						<template #trigger>
							<SIconButton :label="t('pages.settings.tags.tagActions')">
								<Icon
									name="lucide:more-horizontal"
									aria-hidden="true"
								/>
							</SIconButton>
						</template>
					</SDropdownMenu>
				</template>
				<template #empty>
					<SEmptyState :message="t('pages.settings.tags.emptyState')">
						<template #action>
							<SButton
								variant="primary"
								@click="openCreate"
							>
								{{ t('pages.settings.tags.addTag') }}
							</SButton>
						</template>
					</SEmptyState>
				</template>
			</STable>
		</SSection>

		<AutoTaggingRulesPanel />

		<SDialog
			v-model="dialogOpen"
			:title="editingId === null ? t('pages.settings.tags.addTag') : t('pages.settings.tags.renameTag')"
		>
			<SField
				:label="t('pages.settings.tags.label')"
				:error="labelError"
				control-id="tag-label"
			>
				<SInput
					id="tag-label"
					v-model="label"
					:placeholder="t('pages.settings.tags.placeholder')"
					:invalid="!!labelError"
				/>
			</SField>
			<template #footer>
				<SButton
					variant="secondary"
					@click="dialogOpen = false"
				>
					{{ t('pages.settings.tags.cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="saving"
					@click="save"
				>
					{{ editingId === null ? t('pages.settings.tags.addTag') : t('pages.settings.tags.saveChanges') }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteTargetOpen"
			:title="t('pages.settings.tags.deleteTagTitle')"
		>
			<p v-if="deleteTarget && deleteTarget.usage > 0">
				{{ deleteTarget.usage === 1 ? t('pages.settings.tags.deleteUsedSingular', { label: deleteTarget.label }) : t('pages.settings.tags.deleteUsedPlural', { label: deleteTarget.label, count: deleteTarget.usage }) }}
			</p>
			<p v-else-if="deleteTarget">
				{{ t('pages.settings.tags.deleteUnused', { label: deleteTarget.label }) }}
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
					{{ t('pages.settings.tags.cancel') }}
				</SButton>
				<SButton
					variant="danger"
					:loading="deleting"
					@click="doDelete"
				>
					{{ t('pages.settings.tags.deleteTag') }}
				</SButton>
			</template>
		</SDialog>
	</div>
</template>
