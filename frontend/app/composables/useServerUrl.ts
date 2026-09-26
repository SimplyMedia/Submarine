/** Configured base path (e.g. "/submarine"), trimmed of trailing slashes; "" for the root. */
export function baseUrl(): string {
	const config = useRuntimeConfig()
	return (config.app.baseURL || '/').replace(/\/+$/, '')
}

/**
 * Polls `/_status/ready` once a second until the server responds or one
 * minute elapses. Used after actions that restart the process (restore,
 * restart) to know when it is safe to reload the page.
 */
export async function pollReady(): Promise<boolean> {
	for (let attempt = 0; attempt < 60; attempt++) {
		const { promise, resolve } = Promise.withResolvers()
		setTimeout(resolve, 1000)
		await promise
		try {
			const response = await fetch(`${baseUrl()}/_status/ready`)
			if (response.ok) {
				return true
			}
		}
		catch {
			// Not up yet, keep polling.
		}
	}
	return false
}
