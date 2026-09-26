import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useCommandsStore } from '~/stores/commands'

const client = vi.hoisted(() => ({
	GET: vi.fn(),
	POST: vi.fn(),
	DELETE: vi.fn(),
}))

vi.mock('~/composables/useApi', () => ({
	useApi: () => client,
	toApiError: (error: unknown, response?: Response) => {
		const problem = error as { title?: string } | undefined
		const failure = new Error(problem?.title ?? response?.statusText ?? 'Request failed')
		Object.assign(failure, { status: response?.status ?? 0 })
		return failure
	},
	ApiError: class ApiError extends Error {},
}))

function ok(body: unknown) {
	return { data: body, error: undefined, response: new Response(null, { status: 200 }) }
}

function failure(status: number, body: unknown) {
	return { data: undefined, error: body, response: new Response(null, { status }) }
}

describe('commands store', () => {
	beforeEach(() => {
		setActivePinia(createPinia())
		client.GET.mockReset()
		client.POST.mockReset()
		client.DELETE.mockReset()
	})

	it('splits commands into active and history buckets, newest first', () => {
		const store = useCommandsStore()
		store.upsert({ id: 1, name: 'RssSync', status: 'RUNNING' })
		store.upsert({ id: 2, name: 'Backup', status: 'QUEUED' })
		store.upsert({ id: 3, name: 'HealthCheck', status: 'COMPLETED' })
		store.upsert({ id: 4, name: 'IndexerDefinitionSync', status: 'FAILED' })

		expect(store.active.map(c => c.id)).toEqual([2, 1])
		expect(store.history.map(c => c.id)).toEqual([4, 3])
	})

	it('upserts by id instead of duplicating rows', () => {
		const store = useCommandsStore()
		store.upsert({ id: 1, name: 'RssSync', status: 'QUEUED', progress: 0 })
		store.upsert({ id: 1, name: 'RssSync', status: 'RUNNING', progress: 40 })

		expect(store.commands).toHaveLength(1)
		expect(store.commands[0]?.status).toBe('RUNNING')
		expect(store.commands[0]?.progress).toBe(40)
	})

	it('merges a CommandUpdated event payload into the tracked command', () => {
		const store = useCommandsStore()
		store.upsert({ id: 5, name: 'Backup', status: 'QUEUED', progress: 0 })
		store.applyUpdate({
			commandId: 5,
			name: 'Backup',
			status: 'RUNNING',
			progress: 50,
			message: 'Creating backup',
			startedAt: '2026-01-01T00:00:00Z',
			endedAt: null,
		})

		expect(store.commands[0]?.status).toBe('RUNNING')
		expect(store.commands[0]?.progress).toBe(50)
		expect(store.commands[0]?.message).toBe('Creating backup')
	})

	it('marks a command cancelled locally after a successful cancel call', async () => {
		const store = useCommandsStore()
		store.upsert({ id: 7, name: 'RssSync', status: 'RUNNING' })
		client.DELETE.mockResolvedValue(ok(undefined))

		await store.cancel(7)

		expect(store.commands[0]?.status).toBe('CANCELLED')
	})

	it('throws the ProblemDetails message when cancel fails', async () => {
		const store = useCommandsStore()
		store.upsert({ id: 8, name: 'RssSync', status: 'RUNNING' })
		client.DELETE.mockResolvedValue(failure(409, { title: 'Command already completed' }))

		await expect(store.cancel(8)).rejects.toThrow('Command already completed')
	})

	it('enqueues a command and tracks it', async () => {
		const store = useCommandsStore()
		client.POST.mockResolvedValue(ok({ id: 9, name: 'CheckForUpdate', status: 'QUEUED' }))

		const command = await store.enqueue('CheckForUpdate')

		expect(command.id).toBe(9)
		expect(store.commands.find(c => c.id === 9)?.status).toBe('QUEUED')
	})
})
