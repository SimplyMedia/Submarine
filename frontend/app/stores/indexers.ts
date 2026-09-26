import { defineStore } from 'pinia'
import { toApiError, useApi } from '~/composables/useApi'
import type { components } from '~/types/api'

export type IndexerDto = components['schemas']['IndexerDto']
export type IndexerRequest = components['schemas']['IndexerRequest']
export type IndexerCategoryDto = components['schemas']['IndexerCategoryDto']
export type IndexerSchemaResponse = components['schemas']['IndexerSchemaResponse']
export type IndexerBulkRequest = components['schemas']['IndexerBulkRequest']

/**
 * Shared state for the indexers area: the configured indexer list (edited
 * from /indexers, filtered from /indexers/search), the standard category
 * tree, and the implementation/Cardigann settings schema used by the add
 * dialog. Each loads once; `refreshIndexers` re-fetches after a mutation.
 */
export const useIndexersStore = defineStore('indexers', () => {
	const indexers = ref<IndexerDto[]>([])
	const categories = ref<IndexerCategoryDto[]>([])
	const schema = ref<IndexerSchemaResponse | null>(null)
	const loading = ref(false)
	const loaded = ref(false)
	const loadError = ref('')
	let inFlight: Promise<void> | null = null

	async function refreshIndexers() {
		const api = useApi()
		const result = await api.GET('/api/v1/indexers', { params: { query: { PageSize: 250 } } })
		indexers.value = result.data?.items ?? []
		return result.data !== undefined
	}

	async function load(force = false) {
		if (loaded.value && !force) {
			return
		}
		if (inFlight) {
			return inFlight
		}
		loading.value = true
		loadError.value = ''
		const api = useApi()
		inFlight = (async () => {
			const [indexersOk, categoriesResult, schemaResult] = await Promise.all([
				refreshIndexers(),
				api.GET('/api/v1/indexer-categories'),
				api.GET('/api/v1/indexers/schema'),
			])
			categories.value = categoriesResult.data ?? []
			schema.value = schemaResult.data ?? null
			if (indexersOk && categoriesResult.data && schemaResult.data) {
				loaded.value = true
			}
			else {
				loadError.value = 'Could not load indexers. Check your connection and try again.'
			}
		})()
		try {
			await inFlight
		}
		finally {
			inFlight = null
			loading.value = false
		}
	}

	function indexerName(id: number | null | undefined): string {
		if (id == null) {
			return 'None'
		}
		return indexers.value.find(indexer => indexer.id === id)?.name ?? `Indexer ${id}`
	}

	async function createIndexer(body: IndexerRequest): Promise<IndexerDto> {
		const api = useApi()
		const result = await api.POST('/api/v1/indexers', { body })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		await refreshIndexers()
		return result.data
	}

	async function updateIndexer(id: number, body: IndexerRequest): Promise<IndexerDto> {
		const api = useApi()
		const result = await api.PUT('/api/v1/indexers/{id}', { params: { path: { id } }, body })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		await refreshIndexers()
		return result.data
	}

	async function deleteIndexer(id: number) {
		const api = useApi()
		const result = await api.DELETE('/api/v1/indexers/{id}', { params: { path: { id } } })
		if (!result.response.ok) {
			throw toApiError(result.error, result.response)
		}
		await refreshIndexers()
	}

	async function bulkUpdate(body: IndexerBulkRequest) {
		const api = useApi()
		const result = await api.PUT('/api/v1/indexers/bulk', { body })
		if (!result.response.ok) {
			throw toApiError(result.error, result.response)
		}
		await refreshIndexers()
	}

	async function bulkDelete(ids: number[]) {
		const api = useApi()
		const result = await api.DELETE('/api/v1/indexers/bulk', { body: { ids } })
		if (!result.response.ok) {
			throw toApiError(result.error, result.response)
		}
		await refreshIndexers()
	}

	return {
		indexers,
		categories,
		schema,
		loading,
		loaded,
		loadError,
		load,
		refreshIndexers,
		indexerName,
		createIndexer,
		updateIndexer,
		deleteIndexer,
		bulkUpdate,
		bulkDelete,
	}
})
