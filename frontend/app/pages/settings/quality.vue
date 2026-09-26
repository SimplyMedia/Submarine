<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { toApiError, useApi } from '~/composables/useApi'
import { useDirtyForm } from '~/composables/useDirtyForm'
import type { components } from '~/types/api'

type QualityDefinitionResource = components['schemas']['QualityDefinitionResource']
type QualityDefinitionUpdate = components['schemas']['QualityDefinitionUpdate']
type QualityDefinitionImportResult = components['schemas']['QualityDefinitionImportResult']
type SizeField = 'minSizeMbPerMinute' | 'maxSizeMbPerMinute' | 'preferredSizeMbPerMinute'

definePageMeta({ layout: 'default' })
const { t } = useI18n()
useHead({ title: t('pages.settings.quality.title') })

const api = useApi()
const { toast } = useToast()

const loading = ref(true)
const saving = ref(false)
const loadError = ref('')
const draft = ref<QualityDefinitionResource[]>([])
const dirty = useDirtyForm(draft)

async function load() {
	loadError.value = ''
	const result = await api.GET('/api/v1/quality-definitions')
	if (result.data) {
		dirty.markSaved(result.data)
	}
	else {
		loadError.value = t('pages.settings.quality.loadFailed')
	}
}

onMounted(async () => {
	loading.value = true
	await load()
	loading.value = false
})

function onSizeBlur(row: QualityDefinitionResource, field: SizeField, event: Event) {
	const raw = (event.target as HTMLInputElement).value
	row[field] = raw === '' ? null : Number(raw)
}

async function save() {
	saving.value = true
	const body: QualityDefinitionUpdate[] = draft.value.map(row => ({
		id: row.id,
		minSizeMbPerMinute: row.minSizeMbPerMinute,
		maxSizeMbPerMinute: row.maxSizeMbPerMinute,
		preferredSizeMbPerMinute: row.preferredSizeMbPerMinute,
	}))
	const result = await api.PUT('/api/v1/quality-definitions', { body })
	if (!result.response.ok) {
		toast({ title: t('pages.settings.quality.saveFailed'), description: toApiError(result.error, result.response).message, tone: 'danger' })
		saving.value = false
		return
	}
	const refreshed = await api.GET('/api/v1/quality-definitions')
	saving.value = false
	if (refreshed.data) {
		dirty.markSaved(refreshed.data)
	}
	toast({ title: t('pages.settings.quality.saved'), tone: 'ok' })
}

const columns = [
	{ key: 'title', label: t('pages.settings.quality.titleColumn') },
	{ key: 'minSizeMbPerMinute', label: t('pages.settings.quality.minimumSize'), align: 'right' as const },
	{ key: 'preferredSizeMbPerMinute', label: t('pages.settings.quality.preferredSize'), align: 'right' as const },
	{ key: 'maxSizeMbPerMinute', label: t('pages.settings.quality.maximumSize'), align: 'right' as const },
]

// --- Import ------------------------------------------------------------------
const importOpen = ref(false)
const importText = ref('')
const importError = ref('')
const importing = ref(false)

function openImport() {
	importText.value = ''
	importError.value = ''
	importOpen.value = true
}

async function runImport() {
	importError.value = ''
	let parsed: unknown
	try {
		parsed = JSON.parse(importText.value)
	}
	catch {
		importError.value = t('pages.settings.quality.invalidJson')
		return
	}
	importing.value = true
	const result = await api.POST('/api/v1/quality-definitions/import', { body: parsed })
	importing.value = false
	if (!result.data) {
		importError.value = toApiError(result.error, result.response).message
		return
	}
	const imported = result.data as QualityDefinitionImportResult
	toast({
		title: imported.updated.length === 1 ? t('pages.settings.quality.updatedOne', { count: imported.updated.length }) : t('pages.settings.quality.updatedMany', { count: imported.updated.length }),
		description: imported.skipped.length > 0 ? t('pages.settings.quality.skippedUnrecognised', { values: imported.skipped.join(', ') }) : undefined,
		tone: 'ok',
	})
	importOpen.value = false
	loading.value = true
	await load()
	loading.value = false
}
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.settings.quality.title')">
			<template #actions>
				<SButton
					variant="secondary"
					@click="openImport"
				>
					{{ t('pages.settings.quality.import') }}
				</SButton>
			</template>
		</SPageHeader>

		<SEmptyState
			v-if="loadError"
			:message="loadError"
			icon="lucide:alert-triangle"
		>
			<template #action>
				<SButton @click="load">
					{{ t('pages.settings.quality.retry') }}
				</SButton>
			</template>
		</SEmptyState>
		<SSpinner v-else-if="loading" />

		<template v-else>
			<SSection :title="t('pages.settings.quality.definitions')">
				<STable
					:columns="columns"
					:rows="draft"
					:row-key="(row) => row.id"
				>
					<template #cell-minSizeMbPerMinute="{ row }">
						<input
							type="number"
							class="s-input size-input"
							:value="row.minSizeMbPerMinute ?? ''"
							@blur="onSizeBlur(row, 'minSizeMbPerMinute', $event)"
						>
					</template>
					<template #cell-preferredSizeMbPerMinute="{ row }">
						<input
							type="number"
							class="s-input size-input"
							:value="row.preferredSizeMbPerMinute ?? ''"
							@blur="onSizeBlur(row, 'preferredSizeMbPerMinute', $event)"
						>
					</template>
					<template #cell-maxSizeMbPerMinute="{ row }">
						<input
							type="number"
							class="s-input size-input"
							:value="row.maxSizeMbPerMinute ?? ''"
							@blur="onSizeBlur(row, 'maxSizeMbPerMinute', $event)"
						>
					</template>
					<template #empty>
						<SEmptyState :message="t('pages.settings.quality.emptyState')" />
					</template>
				</STable>
			</SSection>

			<SettingsSaveBar
				:dirty="dirty.isDirty.value"
				:saving="saving"
				@save="save"
				@discard="dirty.revert()"
			/>
		</template>

		<SDialog
			v-model="importOpen"
			:title="t('pages.settings.quality.importDialogTitle')"
			:description="t('pages.settings.quality.importDialogDescription')"
		>
			<SField
				:label="t('pages.settings.quality.trashJson')"
				:error="importError"
			>
				<STextarea
					v-model="importText"
					:rows="10"
					:invalid="!!importError"
					:placeholder="t('pages.settings.quality.jsonPlaceholder')"
				/>
			</SField>
			<template #footer>
				<SButton
					variant="secondary"
					@click="importOpen = false"
				>
					{{ t('pages.settings.quality.cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="importing"
					@click="runImport"
				>
					{{ t('pages.settings.quality.import') }}
				</SButton>
			</template>
		</SDialog>
	</div>
</template>

<style scoped>
.size-input {
	width: 90px;
	text-align: right;
}
</style>
