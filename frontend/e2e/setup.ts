import path from 'node:path'
import type { APIRequestContext, Page } from '@playwright/test'
import { expect } from '@playwright/test'

/**
 * Shared sign-in and idempotent library/indexer setup for the specs that need real data
 * (indexers.spec.ts, activity.spec.ts). Each helper checks for existing state before creating
 * anything, so the suite stays safe to run repeatedly against the same stack.
 */

export const E2E_USERNAME = 'submarine-e2e'
export const E2E_PASSWORD = 'submarine-e2e-password'

export const STUB_TRACKER_NAME = 'Stub tracker'
export const STUB_TRACKER_BASE_URL = `http://localhost:${process.env.E2E_MOCK_PORT_BASE ? Number(process.env.E2E_MOCK_PORT_BASE) + 2 : 5300}`
export const HARBOUR_LIGHTS_TVDB_ID = 121361
/** Substring unique to the release both specs grab (see e2e/mock-torznab.mjs). */
export const TARGET_RELEASE_FRAGMENT = 'H264-GROUP'

export async function signIn(page: Page, entryPath = '/') {
	await page.goto(entryPath)
	// The SPA decides where to go after it loads; wait for that decision.
	await page.getByRole('heading', { level: 1 }).first().waitFor()
	if (page.url().includes('/setup')) {
		await page.getByLabel('Username').fill(E2E_USERNAME)
		await page.getByLabel('Password').fill(E2E_PASSWORD)
		await page.getByRole('button', { name: 'Create account' }).click()
	}
	else if (page.url().includes('/login')) {
		await page.getByLabel('Username').fill(E2E_USERNAME)
		await page.getByLabel('Password').fill(E2E_PASSWORD)
		await page.getByRole('button', { name: 'Sign in' }).click()
	}
	await expect(page).not.toHaveURL(/\/(login|setup)/)
}

/** Ensures the shared library root folder exists, returning its id. */
export async function ensureRootFolder(request: APIRequestContext): Promise<number> {
	const folderPath = path.resolve('.e2e/media/tv')
	const list = await request.get('/api/v1/root-folders')
	const folders = await list.json() as { id: number, path: string }[]
	const existing = folders.find(folder => folder.path === folderPath)
	if (existing) {
		return existing.id
	}
	const created = await request.post('/api/v1/root-folders', { data: { path: folderPath, mediaKind: 'SERIES' } })
	expect(created.ok(), await created.text()).toBe(true)
	return (await created.json() as { id: number }).id
}

/** Ensures a torrent blackhole download client exists so grabs have somewhere to land. */
export async function ensureDownloadClient(request: APIRequestContext): Promise<void> {
	const list = await request.get('/api/v1/download-clients', { params: { PageSize: 200 } })
	const { items } = await list.json() as { items: { type: string }[] }
	if (items.some(client => client.type === 'TORRENT_BLACKHOLE')) {
		return
	}
	const created = await request.post('/api/v1/download-clients', {
		data: {
			name: 'Blackhole',
			type: 'TORRENT_BLACKHOLE',
			enable: true,
			priority: 1,
			settings: {
				torrentFolder: path.resolve('.e2e/downloads/torrents'),
				watchFolder: path.resolve('.e2e/downloads/watch'),
			},
			removeCompleted: false,
			removeFailed: false,
			tagIds: [],
		},
	})
	expect(created.ok(), await created.text()).toBe(true)
}

/** Ensures the Harbour Lights series (from the mock metadata service) is in the library, returning its id. */
export async function ensureSeries(request: APIRequestContext, rootFolderId: number): Promise<number> {
	const list = await request.get('/api/v1/series', { params: { PageSize: 200 } })
	const { items } = await list.json() as { items: { id: number, tvdbId: number }[] }
	const existing = items.find(series => series.tvdbId === HARBOUR_LIGHTS_TVDB_ID)
	if (existing) {
		return existing.id
	}

	const [qualityProfiles, languageProfiles] = await Promise.all([
		request.get('/api/v1/quality-profiles', { params: { PageSize: 50 } }).then(r => r.json()) as Promise<{ items: { id: number }[] }>,
		request.get('/api/v1/language-profiles', { params: { PageSize: 50 } }).then(r => r.json()) as Promise<{ items: { id: number }[] }>,
	])

	const created = await request.post('/api/v1/series', {
		data: {
			tvdbId: HARBOUR_LIGHTS_TVDB_ID,
			metadataProvider: 'TVDB',
			rootFolderId,
			versions: [{ qualityProfileId: qualityProfiles.items[0]!.id, languageProfileId: languageProfiles.items[0]!.id }],
		},
	})
	expect(created.ok(), await created.text()).toBe(true)
	const detail = await created.json() as { series: { id: number } }
	return detail.series.id
}

/** Ensures the stub Torznab tracker is registered. indexers.spec.ts covers adding it through the UI; this is the
 *  fallback so activity.spec.ts can grab a release even when it runs first (files run alphabetically). */
export async function ensureStubTrackerIndexer(request: APIRequestContext): Promise<void> {
	const list = await request.get('/api/v1/indexers', { params: { PageSize: 200 } })
	const { items } = await list.json() as { items: { name: string }[] }
	if (items.some(indexer => indexer.name === STUB_TRACKER_NAME)) {
		return
	}
	const created = await request.post('/api/v1/indexers', {
		data: {
			name: STUB_TRACKER_NAME,
			implementation: 'TORZNAB',
			definitionId: null,
			protocol: 'BITTORRENT',
			baseUrl: STUB_TRACKER_BASE_URL,
			settings: {
				baseUrl: STUB_TRACKER_BASE_URL,
				apiPath: '/api',
				apiKey: null,
				categories: null,
				animeCategories: null,
				additionalParameters: null,
				minimumSeeders: 1,
				seedCriteria: null,
			},
			enableRss: true,
			enableAutomaticSearch: true,
			enableInteractiveSearch: true,
			priority: 25,
			downloadClientId: null,
			proxyId: null,
			categories: [],
			animeCategories: [],
			minimumSeeders: 1,
			seedRatio: null,
			seedTimeMinutes: null,
			seasonPackSeedTimeMinutes: null,
			animeStandardFormatSearch: false,
			tagIds: [],
		},
	})
	expect(created.ok(), await created.text()).toBe(true)
}

/** Root folder, download client and library series every scenario in this suite needs. */
export async function ensureLibrary(request: APIRequestContext): Promise<{ rootFolderId: number, seriesId: number }> {
	const rootFolderId = await ensureRootFolder(request)
	await ensureDownloadClient(request)
	const seriesId = await ensureSeries(request, rootFolderId)
	return { rootFolderId, seriesId }
}

interface ReleaseResource {
	guid: string
	title: string
	indexerId: number | null
	mappedSeriesId: number | null
	mappedMovieId: number | null
	episodeIds: number[]
	decisions: { mediaVersionId: number }[]
}

/**
 * Clears the queue and blocklist so the target release is grabbable again. Earlier viewport runs
 * leave it queued or blocklisted, and both make the decision engine reject a new grab.
 */
export async function resetGrabState(request: APIRequestContext): Promise<void> {
	const queue = await (await request.get('/api/v1/queue', { params: { PageSize: 250 } })).json() as { items: { id: number }[] }
	for (const item of queue.items) {
		const removed = await request.delete(`/api/v1/queue/${item.id}`, { params: { removeFromClient: true, blocklist: false, skipRedownload: true } })
		expect(removed.ok(), await removed.text()).toBe(true)
	}
	const blocklist = await (await request.get('/api/v1/blocklist', { params: { PageSize: 250 } })).json() as { items: { id: number }[] }
	for (const item of blocklist.items) {
		const removed = await request.delete(`/api/v1/blocklist/${item.id}`)
		expect(removed.ok(), await removed.text()).toBe(true)
	}
}

/** Grabs the target release directly through the API, mirroring what the search page's Grab button sends. */
export async function grabTargetRelease(request: APIRequestContext): Promise<void> {
	const search = await request.get('/api/v1/search', { params: { term: 'Harbour Lights' } })
	expect(search.ok(), await search.text()).toBe(true)
	const releases = await search.json() as ReleaseResource[]
	const release = releases.find(candidate => candidate.title.includes(TARGET_RELEASE_FRAGMENT) && candidate.decisions.length > 0)
	if (!release) {
		throw new Error(`Could not find a grabbable "${TARGET_RELEASE_FRAGMENT}" release in the search results`)
	}
	const grabbed = await request.post('/api/v1/releases/grab', {
		data: {
			guid: release.guid,
			indexerId: release.indexerId,
			mediaVersionId: release.decisions[0]!.mediaVersionId,
			seriesId: release.mappedSeriesId,
			episodeIds: release.episodeIds.length > 0 ? release.episodeIds : null,
			movieId: release.mappedMovieId,
			qualitySource: null,
			qualityResolution: null,
			languages: null,
			override: false,
		},
	})
	expect(grabbed.ok(), await grabbed.text()).toBe(true)
}
