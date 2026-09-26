<script setup lang="ts">
import { toApiError, useApi } from '~/composables/useApi'
import { useDirtyForm } from '~/composables/useDirtyForm'
import type { components } from '~/types/api'

type IndexerConfigResource = components['schemas']['IndexerConfigResource']
type NumberField = keyof IndexerConfigResource

definePageMeta({ layout: 'default' })
useHead({ title: 'Indexers' })

const api = useApi()
const { toast } = useToast()

const loading = ref(true)
const loadError = ref('')
const saving = ref(false)
const draft = ref<IndexerConfigResource | null>(null)
const dirty = useDirtyForm(draft)

async function load() {
	loading.value = true
	loadError.value = ''
	const result = await api.GET('/api/v1/config/indexer')
	if (result.data) {
		dirty.markSaved(result.data)
	}
	else {
		loadError.value = 'Could not load indexer settings. Check your connection and try again.'
	}
	loading.value = false
}

onMounted(load)

function setNumber(field: NumberField, value: string) {
	if (!draft.value) {
		return
	}
	draft.value[field] = (value === '' ? 0 : Number(value)) as never
}

async function save() {
	if (!draft.value) {
		return
	}
	saving.value = true
	const result = await api.PUT('/api/v1/config/indexer', { body: draft.value })
	saving.value = false
	if (!result.data) {
		toast({ title: 'Could not save', description: toApiError(result.error, result.response).message, tone: 'danger' })
		return
	}
	dirty.markSaved(result.data)
	toast({ title: 'Saved', tone: 'ok' })
}
</script>

<template>
	<div>
		<SPageHeader title="Indexers" />

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

		<template v-else-if="draft">
			<SSection title="Indexer behavior">
				<div class="field-grid">
					<SField
						label="RSS sync interval (minutes)"
						hint="Minutes between RSS syncs, 0 disables the sync."
						control-id="rss-sync-interval"
					>
						<SInput
							id="rss-sync-interval"
							type="number"
							:model-value="String(draft.rssSyncIntervalMinutes)"
							@update:model-value="setNumber('rssSyncIntervalMinutes', $event)"
						/>
					</SField>
					<SField
						label="Minimum age (minutes)"
						hint="Minimum age in minutes for usenet releases."
						control-id="minimum-age"
					>
						<SInput
							id="minimum-age"
							type="number"
							:model-value="String(draft.minimumAgeMinutes)"
							@update:model-value="setNumber('minimumAgeMinutes', $event)"
						/>
					</SField>
					<SField
						label="Retention (days)"
						hint="Retention in days required for usenet releases, 0 disables."
						control-id="retention-days"
					>
						<SInput
							id="retention-days"
							type="number"
							:model-value="String(draft.retentionDays)"
							@update:model-value="setNumber('retentionDays', $event)"
						/>
					</SField>
					<SField
						label="Maximum size (MB)"
						hint="Maximum release size in MB, 0 disables."
						control-id="maximum-size"
					>
						<SInput
							id="maximum-size"
							type="number"
							:model-value="String(draft.maximumSizeMb)"
							@update:model-value="setNumber('maximumSizeMb', $event)"
						/>
					</SField>
					<SField
						label="Availability delay (days)"
						hint="Days a movie must be past its release date before grabbing, 0 disables."
						control-id="availability-delay"
					>
						<SInput
							id="availability-delay"
							type="number"
							:model-value="String(draft.availabilityDelayDays)"
							@update:model-value="setNumber('availabilityDelayDays', $event)"
						/>
					</SField>
				</div>
			</SSection>

			<SSection title="Hardcoded subtitles">
				<SSwitch
					v-model="draft.allowHardcodedSubs"
					label="Allow releases reporting hardcoded subtitles"
				/>
				<SField
					v-if="!draft.allowHardcodedSubs"
					label="Whitelisted release groups"
					hint="Comma separated release groups allowed to have hardcoded subtitles."
					control-id="hardcoded-subs-whitelist"
				>
					<SInput
						id="hardcoded-subs-whitelist"
						v-model="draft.whitelistedHardcodedSubs"
					/>
				</SField>
			</SSection>

			<SettingsSaveBar
				:dirty="dirty.isDirty.value"
				:saving="saving"
				@save="save"
				@discard="dirty.revert()"
			/>
		</template>

		<SSection title="Indexer management">
			<p class="management-copy">
				Add and manage indexers, run searches and view stats from the Indexers area.
			</p>
			<SButton
				variant="secondary"
				@click="navigateTo('/indexers')"
			>
				Go to indexers
			</SButton>
		</SSection>
	</div>
</template>

<style scoped>
.field-grid {
	display: grid;
	grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
	gap: 16px;
}

.management-copy {
	color: var(--fg-muted);
	margin-bottom: 12px;
}
</style>
