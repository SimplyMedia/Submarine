<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { toApiError, useApi } from '~/composables/useApi'
import { useDirtyForm } from '~/composables/useDirtyForm'
import type { components } from '~/types/api'

const { t } = useI18n()

type IndexerConfigResource = components['schemas']['IndexerConfigResource']
type NumberField = keyof IndexerConfigResource

definePageMeta({ layout: 'default' })
useHead({ title: t('pages.settings.indexers.title') })

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
		loadError.value = t('pages.settings.indexers.loadError')
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
		toast({ title: t('pages.settings.indexers.saveError'), description: toApiError(result.error, result.response).message, tone: 'danger' })
		return
	}
	dirty.markSaved(result.data)
	toast({ title: t('common.saved'), tone: 'ok' })
}
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.settings.indexers.title')" />

		<SEmptyState
			v-if="loadError"
			:message="loadError"
			icon="lucide:alert-triangle"
		>
			<template #action>
				<SButton @click="load">
					{{ t('pages.settings.indexers.retry') }}
				</SButton>
			</template>
		</SEmptyState>
		<SSpinner v-else-if="loading" />

		<template v-else-if="draft">
			<SSection :title="t('pages.settings.indexers.behavior')">
				<div class="field-grid">
					<SField
						:label="t('pages.settings.indexers.rssInterval')"
						:hint="t('pages.settings.indexers.rssIntervalHint')"
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
						:label="t('pages.settings.indexers.minimumAge')"
						:hint="t('pages.settings.indexers.minimumAgeHint')"
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
						:label="t('pages.settings.indexers.retention')"
						:hint="t('pages.settings.indexers.retentionHint')"
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
						:label="t('pages.settings.indexers.maximumSize')"
						:hint="t('pages.settings.indexers.maximumSizeHint')"
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
						:label="t('pages.settings.indexers.availabilityDelay')"
						:hint="t('pages.settings.indexers.availabilityDelayHint')"
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

			<SSection :title="t('pages.settings.indexers.hardcodedSubtitles')">
				<SSwitch
					v-model="draft.allowHardcodedSubs"
					:label="t('pages.settings.indexers.allowHardcodedSubs')"
				/>
				<SField
					v-if="!draft.allowHardcodedSubs"
					:label="t('pages.settings.indexers.whitelistedGroups')"
					:hint="t('pages.settings.indexers.whitelistedGroupsHint')"
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

		<SSection :title="t('pages.settings.indexers.management')">
			<p class="management-copy">
				{{ t('pages.settings.indexers.managementCopy') }}
			</p>
			<SButton
				variant="secondary"
				@click="navigateTo('/indexers')"
			>
				{{ t('pages.settings.indexers.goToIndexers') }}
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
