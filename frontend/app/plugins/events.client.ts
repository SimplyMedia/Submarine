import type { CommandUpdatedPayload } from '~/stores/commands'

/**
 * Wires the SignalR hub to the stores that need live updates everywhere in
 * the app (the rail task indicator, the tasks page, the health line, the
 * queue). Also reloads that state on every connect and reconnect, so events
 * missed while the hub was down are not lost.
 */
export default defineNuxtPlugin(() => {
	const events = useEvents()
	const commandsStore = useCommandsStore()
	const systemStore = useSystemStore()
	const activityStore = useActivityStore()
	const auth = useAuthStore()

	async function loadHealth() {
		// The event payload carries snapshots without row ids; refetch the
		// typed list instead of reshaping it.
		const api = useApi()
		const result = await api.GET('/api/v1/health')
		if (result.data) {
			systemStore.healthIssues = result.data
		}
	}

	// The hub needs a session, so it follows the signed-in (or anonymous-admin) state.
	watch(() => auth.isAuthenticated, (authenticated) => {
		if (authenticated) {
			events.connect()
		}
		else {
			events.disconnect()
		}
	}, { immediate: true })

	// Refetch state missed while disconnected, on the initial connect and every reconnect.
	watch(events.state, (state) => {
		if (state === 'connected') {
			void Promise.all([commandsStore.load(), loadHealth(), activityStore.load()])
		}
	})

	events.on('CommandUpdated', (payload) => {
		commandsStore.applyUpdate(payload as CommandUpdatedPayload)
	})

	events.on('HealthIssuesChangedEvent', () => {
		void loadHealth()
	})
})
