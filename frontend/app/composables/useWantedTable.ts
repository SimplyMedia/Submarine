import { toApiError, useApi } from '~/composables/useApi'
import type { components } from '~/types/api'

export type WantedItem = components['schemas']['WantedItemDto']

/**
 * Shared state and actions for the missing and cutoff-unmet wanted tables:
 * paging, the monitored-only filter, row selection across pages, and the
 * search actions (per selection and search-all).
 */
export function useWantedTable(bucket: 'missing' | 'cutoff') {
	const { toast } = useToast()
	const bucketLabel = bucket === 'missing' ? 'missing' : 'cut off'
	const listPath = bucket === 'missing' ? '/api/v1/wanted/missing' as const : '/api/v1/wanted/cutoff' as const
	const searchAllPath = bucket === 'missing' ? '/api/v1/wanted/missing/search' as const : '/api/v1/wanted/cutoff/search' as const

	const items = ref<WantedItem[]>([])
	const totalCount = ref(0)
	const page = ref(1)
	const pageSize = ref(25)
	const loading = ref(true)
	const monitoredOnly = ref(true)
	const selected = ref<string[]>([])
	const searching = ref(false)
	const searchingAll = ref(false)

	const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize.value)))
	const allSelected = computed(() => items.value.length > 0 && items.value.every(row => selected.value.includes(rowKey(row))))

	watch(monitoredOnly, () => {
		page.value = 1
		void load()
	})

	function rowKey(row: WantedItem): string {
		return `${row.type}-${row.id}`
	}

	function isSelected(row: WantedItem): boolean {
		return selected.value.includes(rowKey(row))
	}

	function toggleRow(row: WantedItem, value: boolean) {
		const key = rowKey(row)
		selected.value = value ? [...selected.value.filter(k => k !== key), key] : selected.value.filter(k => k !== key)
	}

	function toggleAll(value: boolean) {
		if (!value) {
			selected.value = selected.value.filter(key => !items.value.some(row => rowKey(row) === key))
			return
		}
		const keys = items.value.map(rowKey)
		selected.value = [...new Set([...selected.value, ...keys])]
	}

	async function load() {
		loading.value = true
		const api = useApi()
		const result = await api.GET(listPath, {
			params: { query: { Page: page.value, PageSize: pageSize.value, includeUnmonitored: !monitoredOnly.value } },
		})
		if (result.data) {
			items.value = result.data.items
			totalCount.value = result.data.totalCount
		}
		else {
			toast({ title: `Could not load ${bucketLabel} items`, tone: 'danger' })
		}
		loading.value = false
	}

	function setPage(next: number) {
		page.value = Math.min(Math.max(1, next), totalPages.value)
		selected.value = []
		void load()
	}

	async function enqueue(name: string, extra: Record<string, number[]>) {
		const api = useApi()
		const result = await api.POST('/api/v1/commands', { body: { name, ...extra } })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
	}

	async function searchSelected() {
		const selectedRows = items.value.filter(isSelected)
		const episodeIds = selectedRows.filter(row => row.type === 'episode').map(row => row.id)
		const movieIds = selectedRows.filter(row => row.type === 'movie').map(row => row.id)
		if (episodeIds.length === 0 && movieIds.length === 0) {
			return
		}
		searching.value = true
		try {
			if (episodeIds.length > 0) {
				await enqueue('EpisodeSearch', { episodeIds })
			}
			if (movieIds.length > 0) {
				await enqueue('MovieSearch', { movieIds })
			}
			toast({ title: 'Search started', tone: 'ok' })
			selected.value = []
		}
		catch {
			toast({ title: 'Could not start the search', tone: 'danger' })
		}
		finally {
			searching.value = false
		}
	}

	async function searchAll() {
		searchingAll.value = true
		try {
			const api = useApi()
			const result = await api.POST(searchAllPath, {})
			if (!result.data) {
				throw toApiError(result.error, result.response)
			}
			toast({ title: `Searching all ${bucketLabel} items`, tone: 'ok' })
		}
		catch {
			toast({ title: 'Could not start the search', tone: 'danger' })
		}
		finally {
			searchingAll.value = false
		}
	}

	return {
		items,
		totalCount,
		page,
		pageSize,
		totalPages,
		loading,
		monitoredOnly,
		selected,
		searching,
		searchingAll,
		allSelected,
		rowKey,
		isSelected,
		toggleRow,
		toggleAll,
		load,
		setPage,
		searchSelected,
		searchAll,
	}
}
