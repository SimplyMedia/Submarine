import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useActivityStore } from '~/stores/activity'

const client = vi.hoisted(() => ({
	GET: vi.fn(),
	POST: vi.fn(),
	DELETE: vi.fn(),
}))

const eventHandlers = vi.hoisted(() => new Map<string, (payload: unknown) => void>())

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

vi.mock('~/composables/useEvents', () => ({
	useEvents: () => ({
		state: { value: 'connected' },
		on: (type: string, handler: (payload: unknown) => void) => {
			eventHandlers.set(type, handler)
			return () => eventHandlers.delete(type)
		},
	}),
}))

function ok(body: unknown) {
	return { data: body, error: undefined, response: new Response(null, { status: 200 }) }
}

const item = (overrides: Partial<Record<string, unknown>> = {}) => ({
	id: 1,
	title: 'Harbour.Lights.S02E06.1080p.WEB.H264-GROUP',
	seriesTitle: 'Harbour Lights',
	movieTitle: null,
	seriesId: 5,
	episodeIds: [12],
	movieId: null,
	protocol: 'BITTORRENT',
	status: 'DOWNLOADING',
	trackedDownloadState: 'DOWNLOADING',
	size: 1000,
	sizeLeft: 400,
	statusMessages: [],
	indexer: 'Stub Torznab',
	downloadClient: 'Blackhole',
	added: '2026-01-01T00:00:00Z',
	...overrides,
})

describe('activity store', () => {
	beforeEach(() => {
		setActivePinia(createPinia())
		eventHandlers.clear()
		client.GET.mockReset()
		client.POST.mockReset()
		client.DELETE.mockReset()
	})

	it('loads queue items and status together', async () => {
		client.GET.mockImplementation((path: string) => {
			if (path === '/api/v1/queue') {
				return Promise.resolve(ok({ items: [item()], page: 1, pageSize: 250, totalCount: 1 }))
			}
			if (path === '/api/v1/queue/status') {
				return Promise.resolve(ok({ total: 1, errors: 0, warnings: 0, unknown: 0 }))
			}
			throw new Error(`unexpected GET ${path}`)
		})

		const store = useActivityStore()
		await store.load()

		expect(store.items).toHaveLength(1)
		expect(store.items[0]!.title).toBe('Harbour.Lights.S02E06.1080p.WEB.H264-GROUP')
		expect(store.status?.total).toBe(1)
	})

	it('watchQueue re-runs load every time a QueueUpdatedEvent fires, since the event carries no payload', async () => {
		client.GET.mockImplementation((path: string) => {
			if (path === '/api/v1/queue') {
				return Promise.resolve(ok({ items: [item({ sizeLeft: 200 })], page: 1, pageSize: 250, totalCount: 1 }))
			}
			return Promise.resolve(ok({ total: 1, errors: 0, warnings: 0, unknown: 0 }))
		})

		const store = useActivityStore()
		const stop = store.watchQueue()
		expect(client.GET).not.toHaveBeenCalled()

		eventHandlers.get('QueueUpdatedEvent')?.(undefined)
		await vi.waitFor(() => expect(store.items).toHaveLength(1))
		expect(store.items[0]!.sizeLeft).toBe(200)

		stop()
		expect(eventHandlers.has('QueueUpdatedEvent')).toBe(false)
	})

	it('removeItem sends the removal options as query params and reloads the queue', async () => {
		client.GET.mockImplementation(() => Promise.resolve(ok({ items: [], page: 1, pageSize: 250, totalCount: 0 })))
		client.DELETE.mockResolvedValue({ data: undefined, error: undefined, response: new Response(null, { status: 204 }) })

		const store = useActivityStore()
		await store.removeItem(1, { removeFromClient: true, blocklist: true, skipRedownload: false })

		expect(client.DELETE).toHaveBeenCalledWith('/api/v1/queue/{id}', {
			params: {
				path: { id: 1 },
				query: { RemoveFromClient: true, Blocklist: true, SkipRedownload: false },
			},
		})
	})
})
