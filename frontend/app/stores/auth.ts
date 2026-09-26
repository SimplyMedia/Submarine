import { defineStore } from 'pinia'
import { useApi, toApiError } from '~/composables/useApi'

export interface AuthUser {
	id: number
	username: string
}

export const useAuthStore = defineStore('auth', () => {
	const user = ref<AuthUser | null>(null)
	const needsSetup = ref<boolean | null>(null)
	const bootstrapped = ref(false)
	/** AuthMethod NONE authenticates every request anonymously, so there is no user row to load. */
	const authMethodNone = ref(false)
	const isAuthenticated = computed(() => user.value !== null || authMethodNone.value)

	async function bootstrap(onPublicRoute = false) {
		if (bootstrapped.value) {
			return
		}
		bootstrapped.value = true
		const api = useApi()
		const status = await api.GET('/api/v1/setup/status')
		if (!status.data) {
			return
		}
		needsSetup.value = status.data.needsSetup
		authMethodNone.value = status.data.authMethod === 'NONE'
		if (status.data.needsSetup || onPublicRoute || authMethodNone.value) {
			return
		}
		const me = await api.GET('/api/v1/auth/me')
		if (me.data) {
			user.value = me.data
		}
	}

	async function login(username: string, password: string, rememberMe: boolean) {
		const api = useApi()
		const result = await api.POST('/api/v1/auth/login', {
			body: { username, password, rememberMe },
		})
		if (result.data) {
			user.value = result.data
			needsSetup.value = false
		}
		else {
			throw toApiError(result.error, result.response)
		}
	}

	async function setup(username: string, password: string) {
		const api = useApi()
		const result = await api.POST('/api/v1/setup', {
			body: { username, password },
		})
		if (result.data) {
			needsSetup.value = false
			user.value = result.data
		}
		else {
			throw toApiError(result.error, result.response)
		}
	}

	async function logout() {
		const api = useApi()
		await api.POST('/api/v1/auth/logout')
		user.value = null
	}

	return { user, needsSetup, bootstrapped, authMethodNone, isAuthenticated, bootstrap, login, setup, logout }
})
