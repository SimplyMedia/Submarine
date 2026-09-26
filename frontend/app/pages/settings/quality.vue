<script setup lang="ts">
import { toApiError, useApi } from '~/composables/useApi'
import { useDirtyForm } from '~/composables/useDirtyForm'
import type { components } from '~/types/api'

type QualityDefinitionResource = components['schemas']['QualityDefinitionResource']
type QualityDefinitionUpdate = components['schemas']['QualityDefinitionUpdate']
type QualityDefinitionImportResult = components['schemas']['QualityDefinitionImportResult']
type SizeField = 'minSizeMbPerMinute' | 'maxSizeMbPerMinute' | 'preferredSizeMbPerMinute'

definePageMeta({ layout: 'default' })
useHead({ title: 'Quality' })

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
		loadError.value = 'Could not load quality definitions. Check your connection and try again.'
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
		toast({ title: 'Could not save', description: toApiError(result.error, result.response).message, tone: 'danger' })
		saving.value = false
		return
	}
	const refreshed = await api.GET('/api/v1/quality-definitions')
	saving.value = false
	if (refreshed.data) {
		dirty.markSaved(refreshed.data)
	}
	toast({ title: 'Saved', tone: 'ok' })
}

const columns = [
	{ key: 'title', label: 'Title' },
	{ key: 'minSizeMbPerMinute', label: 'Min (MB/min)', align: 'right' as const },
	{ key: 'preferredSizeMbPerMinute', label: 'Preferred (MB/min)', align: 'right' as const },
	{ key: 'maxSizeMbPerMinute', label: 'Max (MB/min)', align: 'right' as const },
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
		importError.value = 'That is not valid JSON.'
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
		title: `Updated ${imported.updated.length} quality definition${imported.updated.length === 1 ? '' : 's'}`,
		description: imported.skipped.length > 0 ? `Skipped unrecognised: ${imported.skipped.join(', ')}` : undefined,
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
		<SPageHeader title="Quality">
			<template #actions>
				<SButton
					variant="secondary"
					@click="openImport"
				>
					Import
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
					Retry
				</SButton>
			</template>
		</SEmptyState>
		<SSpinner v-else-if="loading" />

		<template v-else>
			<SSection title="Quality definitions">
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
						<SEmptyState message="Quality definitions load automatically once the backend seeds them." />
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
			title="Import quality sizes"
			description="Paste a TRaSH Guides quality-size JSON document, or an array of quality entries."
		>
			<SField
				label="TRaSH JSON"
				:error="importError"
			>
				<STextarea
					v-model="importText"
					:rows="10"
					:invalid="!!importError"
					placeholder="{&quot;qualities&quot;: [...] }"
				/>
			</SField>
			<template #footer>
				<SButton
					variant="secondary"
					@click="importOpen = false"
				>
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="importing"
					@click="runImport"
				>
					Import
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
