import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAuthStore } from '~/stores/auth'

const client = vi.hoisted(() => ({
	GET: vi.fn(),
	POST: vi.fn(),
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

describe('auth store', () => {
	beforeEach(() => {
		setActivePinia(createPinia())
		client.GET.mockReset()
		client.POST.mockReset()
	})

	it('bootstraps an existing session from setup status and me', async () => {
		client.GET.mockImplementation(async (path: string) => {
			if (path === '/api/v1/setup/status') {
				return ok({ needsSetup: false })
			}
			return ok({ id: 7, username: 'admin' })
		})

		const auth = useAuthStore()
		await auth.bootstrap(false)

		expect(auth.needsSetup).toBe(false)
		expect(auth.user).toEqual({ id: 7, username: 'admin' })
		expect(client.GET).toHaveBeenCalledWith('/api/v1/auth/me')
	})

	it('skips the me probe on public routes so anonymous pages stay quiet', async () => {
		client.GET.mockResolvedValue(ok({ needsSetup: false }))

		const auth = useAuthStore()
		await auth.bootstrap(true)

		expect(auth.needsSetup).toBe(false)
		expect(auth.user).toBeNull()
		expect(client.GET).not.toHaveBeenCalledWith('/api/v1/auth/me')
	})

	it('bootstraps into setup mode when no users exist', async () => {
		client.GET.mockResolvedValue(ok({ needsSetup: true }))

		const auth = useAuthStore()
		await auth.bootstrap()

		expect(auth.needsSetup).toBe(true)
		expect(auth.user).toBeNull()
		expect(client.GET).not.toHaveBeenCalledWith('/api/v1/auth/me')
	})

	it('logs in with remember me and stores the user', async () => {
		client.POST.mockResolvedValue(ok({ id: 1, username: 'admin' }))

		const auth = useAuthStore()
		await auth.login('admin', 'secret', true)

		expect(client.POST).toHaveBeenCalledWith('/api/v1/auth/login', {
			body: { username: 'admin', password: 'secret', rememberMe: true },
		})
		expect(auth.user).toEqual({ id: 1, username: 'admin' })
	})

	it('surfaces the ProblemDetails title when login is rejected', async () => {
		client.POST.mockResolvedValue(failure(401, { title: 'Invalid username or password' }))

		const auth = useAuthStore()
		await expect(auth.login('admin', 'wrong', false))
			.rejects.toThrow('Invalid username or password')
		expect(auth.user).toBeNull()
	})

	it('clears the user on logout', async () => {
		client.POST.mockResolvedValue({ data: undefined, error: undefined, response: new Response(null, { status: 204 }) })

		const auth = useAuthStore()
		auth.user = { id: 1, username: 'admin' }
		await auth.logout()

		expect(client.POST).toHaveBeenCalledWith('/api/v1/auth/logout')
		expect(auth.user).toBeNull()
	})
})
