import { defineStore } from 'pinia'
import { toApiError, useApi } from '~/composables/useApi'
import { useEvents } from '~/composables/useEvents'
import type { components } from '~/types/api'

export type QueueItem = components['schemas']['QueueItemDto']
export type QueueStatus = components['schemas']['QueueStatusDto']
export type PendingRelease = components['schemas']['PendingReleaseDto']

/**
 * Queue state shared by the Activity > Queue page and the dashboard's
 * compact queue panel. `QueueUpdatedEvent` carries no payload (see
 * Submarine.Core.Events.QueueUpdatedEvent) — it is only a signal to refetch,
 * so `watchQueue` just re-runs `load` on every event while it's active.
 */
export const useActivityStore = defineStore('activity', () => {
	const items = ref<QueueItem[]>([])
	const status = ref<QueueStatus | null>(null)
	const pendingReleases = ref<PendingRelease[]>([])
	const loading = ref(false)
	const loadError = ref('')

	async function load(pageSize = 250) {
		loading.value = true
		try {
			const api = useApi()
			const [itemsResult, statusResult] = await Promise.all([
				api.GET('/api/v1/queue', { params: { query: { PageSize: pageSize } } }),
				api.GET('/api/v1/queue/status'),
			])
			loadError.value = itemsResult.data && statusResult.data ? '' : 'Could not load the queue. Check your connection and try again.'
			items.value = itemsResult.data?.items ?? []
			status.value = statusResult.data ?? null
		}
		finally {
			loading.value = false
		}
	}

	async function loadPendingReleases() {
		const api = useApi()
		const result = await api.GET('/api/v1/pending-releases', { params: { query: { PageSize: 100 } } })
		pendingReleases.value = result.data?.items ?? []
	}

	async function removeItem(id: number, options: { removeFromClient?: boolean, blocklist?: boolean, skipRedownload?: boolean }) {
		const api = useApi()
		const result = await api.DELETE('/api/v1/queue/{id}', {
			params: {
				path: { id },
				query: { RemoveFromClient: options.removeFromClient, Blocklist: options.blocklist, SkipRedownload: options.skipRedownload },
			},
		})
		if (!result.response.ok) {
			throw toApiError(result.error, result.response)
		}
		await load()
	}

	async function bulkRemove(ids: number[], options: { removeFromClient?: boolean, blocklist?: boolean, skipRedownload?: boolean }) {
		const api = useApi()
		const result = await api.DELETE('/api/v1/queue/bulk', {
			body: { ids, removeFromClient: options.removeFromClient ?? null, blocklist: options.blocklist ?? null, skipRedownload: options.skipRedownload ?? null },
		})
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		await load()
	}

	async function importNow(id: number) {
		const api = useApi()
		const result = await api.POST('/api/v1/queue/{id}/import', { params: { path: { id } } })
		if (!result.response.ok) {
			throw toApiError(result.error, result.response)
		}
		await load()
	}

	async function retry(id: number) {
		const api = useApi()
		const result = await api.POST('/api/v1/queue/grab/{id}', { params: { path: { id } } })
		if (!result.response.ok) {
			throw toApiError(result.error, result.response)
		}
		await load()
	}

	async function removePendingRelease(id: number) {
		const api = useApi()
		const result = await api.DELETE('/api/v1/pending-releases/{id}', { params: { path: { id } } })
		if (!result.response.ok) {
			throw toApiError(result.error, result.response)
		}
		await loadPendingReleases()
	}

	/** Subscribes to QueueUpdatedEvent while the caller is mounted; call the returned stop function on unmount. */
	function watchQueue(): () => void {
		const events = useEvents()
		return events.on('QueueUpdatedEvent', () => {
			void load()
		})
	}

	return {
		items,
		status,
		pendingReleases,
		loading,
		loadError,
		load,
		loadPendingReleases,
		removeItem,
		bulkRemove,
		importNow,
		retry,
		removePendingRelease,
		watchQueue,
	}
})
