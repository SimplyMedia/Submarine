import type { Ref } from 'vue'
import * as signalR from '@microsoft/signalr'

export type HubState = 'connecting' | 'connected' | 'reconnecting' | 'down'

type HubHandler = (payload: unknown) => void

/** Wire shape the hub sends for every domain event: `Clients.All.SendAsync("event", { type, payload })`. */
interface HubEnvelope {
	type: string
	payload: unknown
}

let state: Ref<HubState> | null = null
let hubEndpoint: string | null = null
let connection: signalR.HubConnection | null = null
let starting = false
let retryTimer: ReturnType<typeof setTimeout> | null = null
const listeners = new Map<string, Set<HubHandler>>()

const RETRY_DELAY_MS = 10_000

function setState(value: HubState) {
	if (state) {
		state.value = value
	}
}

function dispatch(envelope: HubEnvelope) {
	const handlers = listeners.get(envelope.type)
	if (!handlers) {
		return
	}
	for (const handler of handlers) {
		handler(envelope.payload)
	}
}

let enabled = false

function scheduleRetry() {
	if (retryTimer || !enabled) {
		return
	}
	retryTimer = setTimeout(() => {
		retryTimer = null
		void startConnection()
	}, RETRY_DELAY_MS)
}

async function startConnection() {
	if (connection || starting || !hubEndpoint || !enabled) {
		return
	}
	starting = true
	setState('connecting')

	const hub = new signalR.HubConnectionBuilder()
		.withUrl(hubEndpoint, { withCredentials: true })
		.withAutomaticReconnect([0, 2000, 5000, 10_000, 30_000])
		.configureLogging(signalR.LogLevel.None)
		.build()

	hub.on('event', dispatch)

	hub.onreconnecting(() => {
		setState('reconnecting')
	})
	hub.onreconnected(() => {
		setState('connected')
	})
	hub.onclose(() => {
		setState('down')
		connection = null
		scheduleRetry()
	})

	try {
		await hub.start()
		if (!enabled) {
			setState('down')
			await hub.stop()
			return
		}
		connection = hub
		setState('connected')
	}
	catch {
		setState('down')
		scheduleRetry()
	}
	finally {
		starting = false
	}
}

/**
 * SignalR connection to /hubs/events. The server forwards every domain event
 * through a single "event" message shaped `{ type, payload }` where `type` is
 * the C# record name (e.g. `CommandUpdated`, `HealthIssuesChangedEvent`).
 *
 *   const { state, on } = useEvents()
 *   const stop = on('CommandUpdated', payload => { ... })
 */
export function useEvents() {
	state ??= useState<HubState>('hub-state', () => 'down')
	if (hubEndpoint === null) {
		hubEndpoint = `${baseUrl()}/hubs/events`
	}
	/** Open the hub once the user is signed in; the hub rejects anonymous connections. */
	function connect() {
		if (!import.meta.client || import.meta.test) {
			return
		}
		// Screenshot/e2e harnesses without a hub set this to keep the console clean.
		if ((globalThis as Record<string, unknown>).__SUBMARINE_MOCK__ === true) {
			return
		}
		enabled = true
		void startConnection()
	}

	/** Close the hub and stop retrying, for example on sign out. */
	function disconnect() {
		enabled = false
		if (retryTimer) {
			clearTimeout(retryTimer)
			retryTimer = null
		}
		const current = connection
		connection = null
		setState('down')
		void current?.stop()
	}

	function on(type: string, handler: HubHandler) {
		let handlers = listeners.get(type)
		if (!handlers) {
			handlers = new Set()
			listeners.set(type, handlers)
		}
		handlers.add(handler)
		return () => {
			handlers?.delete(handler)
		}
	}

	return { state: state as Ref<HubState>, on, connect, disconnect }
}
