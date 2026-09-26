import { defineStore } from 'pinia'
import { toApiError, useApi } from '~/composables/useApi'
import type { components } from '~/types/api'

export type Command = components['schemas']['Command']

/** Payload shape of the `CommandUpdated` domain event, see Submarine.Core.Events.CommandUpdated. */
export interface CommandUpdatedPayload {
	commandId: number
	name: string
	status: string
	progress: number
	message: string | null
	startedAt: string | null
	endedAt: string | null
}

const ACTIVE_STATUSES: Record<string, true> = { QUEUED: true, RUNNING: true }
const MAX_COMMANDS = 200

export const useCommandsStore = defineStore('commands', () => {
	const commands = ref<Command[]>([])
	const loading = ref(false)

	const active = computed(() => commands.value.filter(command => ACTIVE_STATUSES[command.status ?? '']))
	const history = computed(() =>
		commands.value.filter(command => !ACTIVE_STATUSES[command.status ?? '']).slice(0, 25),
	)

	function upsert(command: Command) {
		const index = commands.value.findIndex(existing => existing.id === command.id)
		if (index >= 0) {
			commands.value.splice(index, 1, { ...commands.value[index], ...command })
			return
		}
		commands.value = [command, ...commands.value].slice(0, MAX_COMMANDS)
	}

	/** Merge a `CommandUpdated` event payload into the tracked list. */
	function applyUpdate(payload: CommandUpdatedPayload) {
		upsert({
			id: payload.commandId,
			name: payload.name,
			status: payload.status as Command['status'],
			progress: payload.progress,
			message: payload.message,
			startedAt: payload.startedAt,
			endedAt: payload.endedAt,
		})
	}

	async function load() {
		loading.value = true
		try {
			const api = useApi()
			const result = await api.GET('/api/v1/commands', {
				params: { query: { PageSize: 100, SortKey: 'CreatedAt', SortDirection: 'descending' } },
			})
			if (result.data) {
				commands.value = result.data.items
			}
		}
		finally {
			loading.value = false
		}
	}

	async function fetchById(id: number) {
		const api = useApi()
		const result = await api.GET('/api/v1/commands/{id}', { params: { path: { id } } })
		if (result.data) {
			upsert(result.data)
		}
	}

	async function cancel(id: number) {
		const api = useApi()
		const result = await api.DELETE('/api/v1/commands/{id}', { params: { path: { id } } })
		if (result.error) {
			throw toApiError(result.error, result.response)
		}
		const existing = commands.value.find(command => command.id === id)
		if (existing) {
			upsert({ ...existing, status: 'CANCELLED' })
		}
	}

	/** Enqueue a command by registry name, e.g. 'CheckForUpdate'. */
	async function enqueue(name: string): Promise<Command> {
		const api = useApi()
		const result = await api.POST('/api/v1/commands', { body: { name } })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		upsert(result.data)
		return result.data
	}

	return { commands, loading, active, history, load, upsert, applyUpdate, fetchById, cancel, enqueue }
})
