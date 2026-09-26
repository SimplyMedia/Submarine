import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useLogsStore } from '~/stores/logs'

const client = vi.hoisted(() => ({
	GET: vi.fn(),
	DELETE: vi.fn(),
}))

vi.mock('~/composables/useApi', () => ({
	useApi: () => client,
}))

function ok(body: unknown) {
	return { data: body, error: undefined, response: new Response(null, { status: 200 }) }
}

describe('logs store', () => {
	beforeEach(() => {
		setActivePinia(createPinia())
		client.GET.mockReset()
		client.DELETE.mockReset()
	})

	it('computes total pages from the page size and total count', async () => {
		const store = useLogsStore()
		client.GET.mockResolvedValue(ok({ items: [], page: 1, pageSize: 50, totalCount: 120 }))

		await store.load()

		expect(store.totalPages).toBe(3)
	})

	it('resets to page 1 when filters change', () => {
		const store = useLogsStore()
		store.setPage(3)
		store.setFilter('Error', 'timeout')

		expect(store.page).toBe(1)
		expect(store.level).toBe('Error')
		expect(store.query).toBe('timeout')
	})

	it('clamps setPage within the known range', async () => {
		const store = useLogsStore()
		client.GET.mockResolvedValue(ok({ items: [], page: 1, pageSize: 50, totalCount: 100 }))
		await store.load()

		store.setPage(0)
		expect(store.page).toBe(1)

		store.setPage(99)
		expect(store.page).toBe(2)
	})

	it('clears entries and resets to page 1 after clearing', async () => {
		const store = useLogsStore()
		client.GET.mockResolvedValue(ok({ items: [{ id: 1, time: '2026-01-01T00:00:00Z', level: 'Error', logger: 'x', message: 'boom', exception: null }], page: 1, pageSize: 50, totalCount: 1 }))
		await store.load()
		client.DELETE.mockResolvedValue({ response: new Response(null, { status: 204 }) })

		await store.clear()

		expect(store.entries).toEqual([])
		expect(store.totalCount).toBe(0)
		expect(store.page).toBe(1)
	})
})
