export default defineNuxtRouteMiddleware(async (to) => {
	if (import.meta.server) {
		return
	}

	const auth = useAuthStore()
	await auth.bootstrap(to.meta.public === true)

	const isPublic = to.meta.public === true

	if (auth.needsSetup && to.path !== '/setup') {
		return navigateTo('/setup', { replace: true })
	}
	if (auth.needsSetup === false && to.path === '/setup') {
		return navigateTo('/login', { replace: true })
	}
	if (!isPublic && !auth.isAuthenticated) {
		return navigateTo({ path: '/login', query: { redirect: to.fullPath } }, { replace: true })
	}
	if (isPublic && auth.isAuthenticated) {
		return navigateTo('/series', { replace: true })
	}
})
