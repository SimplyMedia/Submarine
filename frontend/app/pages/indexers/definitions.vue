<script setup lang="ts">
import { toApiError, useApi } from '~/composables/useApi'
import { useCommandsStore } from '~/stores/commands'
import { navChildren } from '~/navigation'
import type { components } from '~/types/api'

type IndexerDefinitionDto = components['schemas']['IndexerDefinitionDto']

definePageMeta({ layout: 'default' })
useHead({ title: 'Definitions' })

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
		loadError.value = 'Could not load definitions. Check your connection and try again.'
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
		toast({ title: 'Syncing definitions', tone: 'ok' })
	}
	catch (error) {
		toast({ title: 'Could not sync definitions', description: toApiError(error).message, tone: 'danger' })
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
	{ key: 'name', label: 'Name' },
	{ key: 'type', label: 'Type' },
	{ key: 'language', label: 'Language' },
	{ key: 'protocol', label: 'Protocol' },
	{ key: 'installed', label: 'Status' },
]

onMounted(() => {
	void load()
})
</script>

<template>
	<div>
		<SPageHeader title="Definitions">
			<template #actions>
				<SButton
					variant="primary"
					:loading="syncing"
					@click="sync"
				>
					Sync definitions
				</SButton>
			</template>
		</SPageHeader>
		<SubNav
			label="Indexers"
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
			placeholder="Search definitions"
			class="definitions-search"
		/>

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
		<STable
			v-else
			:columns="columns"
			:rows="filtered"
			:row-key="(row) => row.id"
		>
			<template #empty>
				<SEmptyState message="No definitions found. Sync to pull the latest set." />
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
					{{ row.installed ? 'In use' : 'Available' }}
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
