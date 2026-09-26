import { defineStore } from 'pinia'
import { toApiError, useApi } from '~/composables/useApi'
import type { components } from '~/types/api'

export type LogEntry = components['schemas']['LogDto']
export type LogFile = components['schemas']['LogFileDto']

const PAGE_SIZE = 50

export const useLogsStore = defineStore('logs', () => {
	const entries = ref<LogEntry[]>([])
	const totalCount = ref(0)
	const page = ref(1)
	const level = ref('')
	const query = ref('')
	const loading = ref(false)
	const files = ref<LogFile[]>([])

	const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / PAGE_SIZE)))

	async function load() {
		loading.value = true
		try {
			const api = useApi()
			const result = await api.GET('/api/v1/logs', {
				params: {
					query: {
						Page: page.value,
						PageSize: PAGE_SIZE,
						level: level.value || undefined,
						q: query.value || undefined,
					},
				},
			})
			if (result.data) {
				entries.value = result.data.items
				totalCount.value = result.data.totalCount
			}
		}
		finally {
			loading.value = false
		}
	}

	async function clear() {
		const api = useApi()
		const result = await api.DELETE('/api/v1/logs')
		if (!result.response.ok) {
			throw toApiError(result.error, result.response)
		}
		entries.value = []
		totalCount.value = 0
		page.value = 1
	}

	async function loadFiles() {
		const api = useApi()
		const result = await api.GET('/api/v1/logs/files')
		if (result.data) {
			files.value = result.data
		}
	}

	function setFilter(nextLevel: string, nextQuery: string) {
		level.value = nextLevel
		query.value = nextQuery
		page.value = 1
	}

	function setPage(nextPage: number) {
		page.value = Math.min(Math.max(1, nextPage), totalPages.value)
	}

	return { entries, totalCount, totalPages, page, level, query, loading, files, load, clear, loadFiles, setFilter, setPage }
})
