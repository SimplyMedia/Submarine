<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useApi } from '~/composables/useApi'
import type { components } from '~/types/api'

type SystemStatusDto = components['schemas']['SystemStatusDto']
type HealthIssueDto = components['schemas']['HealthIssueDto']

definePageMeta({ layout: 'default' })
const { t } = useI18n()
useHead({ title: t('pages.settings.metadata.title') })

const api = useApi()

const loading = ref(true)
const loadError = ref('')
const status = ref<SystemStatusDto | null>(null)
const issues = ref<HealthIssueDto[]>([])

async function load() {
	loading.value = true
	loadError.value = ''
	const [statusResult, healthResult] = await Promise.all([
		api.GET('/api/v1/system/status'),
		api.GET('/api/v1/health'),
	])
	if (statusResult.data) {
		status.value = statusResult.data
	}
	if (healthResult.data) {
		issues.value = healthResult.data
	}
	if (!statusResult.data || !healthResult.data) {
		loadError.value = t('pages.settings.metadata.loadFailed')
	}
	loading.value = false
}
void load()

interface ServiceRow { key: string, name: string, url: string, helpText: string }

const services = computed<ServiceRow[]>(() => [
	{
		key: 'Metadata',
		name: t('pages.settings.metadata.metadataName'),
		url: status.value?.metadataUrl ?? '',
		helpText: t('pages.settings.metadata.metadataHelp'),
	},
	{
		key: 'Mappings',
		name: t('pages.settings.metadata.mappingsName'),
		url: status.value?.mappingsUrl ?? '',
		helpText: t('pages.settings.metadata.mappingsHelp'),
	},
])

function issueFor(key: string): HealthIssueDto | undefined {
	return issues.value.find(issue => issue.source.includes(key) && (issue.type === 'WARNING' || issue.type === 'ERROR'))
}
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.settings.metadata.title')" />

		<SSection :title="t('pages.settings.metadata.title')">
			<SEmptyState
				v-if="loadError"
				:message="loadError"
				icon="lucide:alert-triangle"
			>
				<template #action>
					<SButton @click="load">
						{{ t('pages.settings.metadata.retry') }}
					</SButton>
				</template>
			</SEmptyState>
			<SSpinner v-else-if="loading" />
			<div
				v-else
				class="service-list"
			>
				<div
					v-for="service in services"
					:key="service.key"
					class="service-row"
				>
					<div class="service-info">
						<div class="service-heading">
							<span class="service-name">{{ service.name }}</span>
							<SBadge
								v-if="issueFor(service.key)"
								tone="danger"
							>
								{{ issueFor(service.key)!.message }}
							</SBadge>
							<SBadge
								v-else
								tone="ok"
							>
								{{ t('pages.settings.metadata.reachable') }}
							</SBadge>
						</div>
						<SInput
							:model-value="service.url"
							disabled
						/>
						<p class="s-field-hint">
							{{ service.helpText }}
						</p>
					</div>
				</div>
			</div>
		</SSection>
	</div>
</template>

<style scoped>
.service-list {
	display: grid;
	gap: 24px;
	max-width: 560px;
}

.service-info {
	display: grid;
	gap: 8px;
}

.service-heading {
	display: flex;
	align-items: center;
	gap: 8px;
}

.service-name {
	font-weight: 500;
}
</style>
