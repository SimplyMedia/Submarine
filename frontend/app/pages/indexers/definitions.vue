<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { toApiError, useApi } from '~/composables/useApi'
import { useCommandsStore } from '~/stores/commands'
import { navChildren } from '~/navigation'
import type { components } from '~/types/api'

type IndexerDefinitionDto = components['schemas']['IndexerDefinitionDto']

definePageMeta({ layout: 'default' })
const { t } = useI18n()
useHead({ title: t('pages.indexers.definitions.title') })

const api = useApi()
const commandsStore = useCommandsStore()
const { toast } = useToast()

const loading = ref(true)
const definitions = ref<IndexerDefinitionDto[]>([])
const loadError = ref('')
const search = ref('')
const syncing = ref(false)
const syncCommandId = ref<number | null>(null)

async function load() {
	loading.value = true
	loadError.value = ''
	const result = await api.GET('/api/v1/indexer-definitions')
	if (!result.data) {
		loadError.value = t('pages.indexers.definitions.loadFailed')
	}
	definitions.value = result.data ?? []
	loading.value = false
}

const filtered = computed(() => {
	const term = search.value.trim().toLowerCase()
	if (term.length === 0) {
		return definitions.value
	}
	return definitions.value.filter(def => def.name.toLowerCase().includes(term) || def.description.toLowerCase().includes(term))
})

const syncCommand = computed(() => commandsStore.commands.find(command => command.id === syncCommandId.value) ?? null)

async function sync() {
	syncing.value = true
	try {
		const result = await api.POST('/api/v1/indexer-definitions/sync')
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		commandsStore.upsert(result.data)
		syncCommandId.value = result.data.id ?? null
		toast({ title: t('pages.indexers.definitions.syncing'), tone: 'ok' })
	}
	catch (error) {
		toast({ title: t('pages.indexers.definitions.syncFailed'), description: toApiError(error).message, tone: 'danger' })
	}
	finally {
		syncing.value = false
	}
}

watch(() => syncCommand.value?.status, (status) => {
	if (status === 'COMPLETED') {
		void load()
	}
})

const columns = [
	{ key: 'name', label: t('pages.indexers.definitions.name') },
	{ key: 'type', label: t('pages.indexers.definitions.type') },
	{ key: 'language', label: t('pages.indexers.definitions.language') },
	{ key: 'protocol', label: t('pages.indexers.definitions.protocol') },
	{ key: 'installed', label: t('pages.indexers.definitions.status') },
]

onMounted(() => {
	void load()
})
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.indexers.definitions.title')">
			<template #actions>
				<SButton
					variant="primary"
					:loading="syncing"
					@click="sync"
				>
					{{ t('pages.indexers.definitions.sync') }}
				</SButton>
			</template>
		</SPageHeader>
		<SubNav
			:label="t('pages.indexers.definitions.indexersNav')"
			:items="navChildren('indexers')"
		/>

		<CommandProgress
			v-if="syncCommand"
			:command="syncCommand"
			class="definitions-sync-progress"
		/>

		<SInput
			v-model="search"
			type="search"
			:placeholder="t('pages.indexers.definitions.searchPlaceholder')"
			class="definitions-search"
		/>

		<SEmptyState
			v-if="loadError"
			:message="loadError"
			icon="lucide:alert-triangle"
		>
			<template #action>
				<SButton @click="load">
					{{ t('pages.indexers.definitions.retry') }}
				</SButton>
			</template>
		</SEmptyState>
		<SSpinner v-else-if="loading" />
		<STable
			v-else
			:columns="columns"
			:rows="filtered"
			:row-key="(row) => row.id"
		>
			<template #empty>
				<SEmptyState :message="t('pages.indexers.definitions.emptyState')" />
			</template>
			<template #cell-type="{ row }">
				<SBadge :tone="row.type === 'public' ? 'ok' : 'neutral'">
					{{ row.type }}
				</SBadge>
			</template>
			<template #cell-protocol="{ row }">
				{{ row.protocol }}
			</template>
			<template #cell-installed="{ row }">
				<SBadge :tone="row.installed ? 'info' : 'neutral'">
					{{ t(row.installed ? 'pages.indexers.definitions.inUse' : 'pages.indexers.definitions.available') }}
				</SBadge>
			</template>
		</STable>
	</div>
</template>

<style scoped>
.definitions-sync-progress {
	margin-bottom: 16px;
}

.definitions-search {
	max-width: 320px;
	margin-bottom: 16px;
}
</style>
