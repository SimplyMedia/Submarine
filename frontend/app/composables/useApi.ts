import createClient from 'openapi-fetch'
import type { Middleware } from 'openapi-fetch'
import type { paths } from '~/types/api'

/** Error carrying RFC 9457 ProblemDetails fields for forms and toasts. */
export class ApiError extends Error {
	readonly status: number
	readonly fieldErrors: Record<string, string[]>

	constructor(message: string, status: number, fieldErrors: Record<string, string[]> = {}) {
		super(message)
		this.name = 'ApiError'
		this.status = status
		this.fieldErrors = fieldErrors
	}
}

type ProblemBody = { title?: string, detail?: string, errors?: Record<string, string[]> }

/**
 * Turn an openapi-fetch `error` value into an ApiError. Bodies parsed from
 * application/problem+json arrive as objects; anything else falls back to
 * the HTTP status text.
 */
export function toApiError(error: unknown, response?: Response): ApiError {
	if (error instanceof ApiError) {
		return error
	}
	const status = response?.status ?? 0
	if (error && typeof error === 'object') {
		const problem = error as ProblemBody
		// A FluentValidation 400 carries per-field `errors` and a raw "Validation
		// failed: -- Name: ... Severity: Error" dump in `detail`; prefer `title`
		// ("One or more validation errors occurred.") in that case. Otherwise
		// `detail` carries the specific message (e.g. a 409 conflict's "in use by" list).
		const message = (problem.errors ? problem.title : undefined) || problem.detail || problem.title || response?.statusText || 'Request failed'
		return new ApiError(message, status, problem.errors ?? {})
	}
	const text = typeof error === 'string' && error ? error : response?.statusText || 'Request failed'
	return new ApiError(text, status)
}

/**
 * Typed client for the Submarine API. Paths are the full generated paths:
 *
 *   const api = useApi()
 *   const { data, error, response } = await api.GET('/api/v1/auth/me')
 *
 * Cookies ride along (`credentials: 'include'`). A 401 redirects to /login
 * with the current path so the user lands back where they started.
 */
export function useApi() {
	const base = baseUrl()

	const client = createClient<paths>({
		baseUrl: base || '/',
		credentials: 'include',
	})

	const redirectOnUnauthorized: Middleware = {
		onResponse({ request, response }) {
			if (response.status !== 401 || !import.meta.client) {
				return
			}
			// The auth store probes this endpoint to detect AuthMethod NONE before a
			// session exists; a 401 there just means "not signed in", already handled
			// by the route middleware, so it must not trigger a second hard redirect.
			if (new URL(request.url).pathname.endsWith('/api/v1/system/status')) {
				return
			}
			// Runs outside the Nuxt instance, so navigate the window directly.
			const path = window.location.pathname
			if (path.endsWith('/login') || path.endsWith('/setup')) {
				return
			}
			const redirect = path + window.location.search
			window.location.assign(`${base}/login?redirect=${encodeURIComponent(redirect)}`)
		},
	}
	client.use(redirectOnUnauthorized)

	return client
}
