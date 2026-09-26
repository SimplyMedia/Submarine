import { defineStore } from 'pinia'
import type { components } from '~/types/api'

export type SystemStatus = components['schemas']['SystemStatusDto']
export type HealthIssue = components['schemas']['HealthIssueDto']

export const useSystemStore = defineStore('system', () => {
	const status = ref<SystemStatus | null>(null)
	/** Populated by the health slice; empty until then. */
	const healthIssues = ref<HealthIssue[]>([])

	async function loadStatus() {
		const api = useApi()
		const result = await api.GET('/api/v1/system/status')
		if (result.data) {
			status.value = result.data
		}
	}

	async function loadHealth() {
		const api = useApi()
		const result = await api.GET('/api/v1/health')
		if (result.data) {
			healthIssues.value = result.data
		}
	}

	return { status, healthIssues, loadStatus, loadHealth }
})
