import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import {
	ensureLibrary,
	ensureStubTrackerIndexer,
	signIn,
	TARGET_RELEASE_FRAGMENT,
} from './setup'

const HISTORY_FILTER_RELEASE = 'BluRay.x264-GROUP'
const BLOCKLIST_FILTER_RELEASE = 'BluRay.AVC.DTS-HD'

interface SearchRelease {
	guid: string
	title: string
	indexerId: number | null
	mappedSeriesId: number | null
	mappedMovieId: number | null
	episodeIds: number[]
	decisions: { mediaVersionId: number }[]
}

async function grabFixtureRelease(request: APIRequestContext, fragment: string): Promise<string> {
	const search = await request.get('/api/v1/search', { params: { term: 'Harbour Lights' } })
	expect(search.ok(), await search.text()).toBe(true)
	const releases = await search.json() as SearchRelease[]
	const release = releases.find(candidate => candidate.title.includes(fragment) && candidate.decisions.length > 0)
	if (!release) {
		throw new Error(`Could not find a grabbable "${fragment}" release in the search results`)
	}

	// Only clear this fixture's prior state. Other E2E scenarios may be using the tracker.
	const queue = await request.get('/api/v1/queue', { params: { PageSize: 250 } })
	expect(queue.ok(), await queue.text()).toBe(true)
	const queued = await queue.json() as { items: { id: number, title: string }[] }
	for (const item of queued.items.filter(item => item.title.includes(fragment))) {
		const removed = await request.delete(`/api/v1/queue/${item.id}`, {
			params: { removeFromClient: true, blocklist: false, skipRedownload: true },
		})
		expect(removed.ok(), await removed.text()).toBe(true)
	}

	const blocklist = await request.get('/api/v1/blocklist', { params: { PageSize: 250 } })
	expect(blocklist.ok(), await blocklist.text()).toBe(true)
	const blocked = await blocklist.json() as { items: { id: number, releaseTitle: string }[] }
	for (const item of blocked.items.filter(item => item.releaseTitle.includes(fragment))) {
		const removed = await request.delete(`/api/v1/blocklist/${item.id}`)
		expect(removed.ok(), await removed.text()).toBe(true)
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
	return release.title
}

async function blocklistFixtureThroughUi(page: Page, title: string) {
	await page.goto('/activity/queue')
	const row = page.getByRole('row').filter({ hasText: title }).first()
	await expect(row).toBeVisible()
	await row.getByRole('button', { name: 'Queue item actions' }).click()
	await page.getByRole('menuitem', { name: 'Remove' }).click()
	const dialog = page.getByRole('dialog', { name: 'Remove from queue' })
	await expect(dialog).toBeVisible()
	await dialog.getByLabel('Add to blocklist').check()
	await dialog.getByLabel('Skip redownload').check()
	await dialog.getByRole('button', { name: 'Remove', exact: true }).click()
	await expect(dialog).toBeHidden()
	await expect(row).toBeHidden()
}

// The smoke suite drives a real API (dev proxy target or E2E_BASE_URL).
// Without it the suite compiles but skips.
test.skip(!process.env.E2E_BASE_URL, 'Set E2E_BASE_URL to a running Submarine API to run e2e')

test('activity: a grabbed release shows in the queue, then removing it blocklists it and logs history', async ({ page }) => {
	await signIn(page)
	await ensureLibrary(page.request)
	await ensureStubTrackerIndexer(page.request)

	await grabFixtureRelease(page.request, TARGET_RELEASE_FRAGMENT)

	await page.goto('/activity/queue')
	await expect(page.getByRole('heading', { level: 1, name: 'Queue' })).toBeVisible()
	const queueRow = page.getByRole('row').filter({ hasText: TARGET_RELEASE_FRAGMENT }).first()
	await expect(queueRow).toBeVisible()

	await queueRow.getByRole('button', { name: 'Queue item actions' }).click()
	await page.getByRole('menuitem', { name: 'Remove' }).click()
	const removeDialog = page.getByRole('dialog', { name: 'Remove from queue' })
	await expect(removeDialog).toBeVisible()
	await removeDialog.getByLabel('Add to blocklist').check()
	// Prevents an automatic re-search from grabbing a different release right after, which would
	// leave the queue non-empty for the next run in an unpredictable way.
	await removeDialog.getByLabel('Skip redownload').check()
	await removeDialog.getByRole('button', { name: 'Remove', exact: true }).click()
	await expect(removeDialog).toBeHidden()
	await expect(queueRow).toBeHidden()

	await page.goto('/activity/blocklist')
	await expect(page.getByRole('heading', { level: 1, name: 'Blocklist' })).toBeVisible()
	await expect(page.getByRole('row').filter({ hasText: TARGET_RELEASE_FRAGMENT }).first()).toBeVisible()

	await page.goto('/activity/history')
	await expect(page.getByRole('heading', { level: 1, name: 'History' })).toBeVisible()
	const historyRow = page.getByRole('row').filter({ hasText: TARGET_RELEASE_FRAGMENT }).first()
	await expect(historyRow).toBeVisible()
	await expect(historyRow.getByText('Grabbed')).toBeVisible()
})

test('queue: removing an item without blocklisting leaves it searchable', async ({ page }) => {
	await signIn(page)
	await ensureLibrary(page.request)
	await ensureStubTrackerIndexer(page.request)
	await grabFixtureRelease(page.request, TARGET_RELEASE_FRAGMENT)

	await page.goto('/activity/queue')
	const queueRow = page.getByRole('row').filter({ hasText: TARGET_RELEASE_FRAGMENT }).first()
	await expect(queueRow).toBeVisible()
	await queueRow.getByRole('button', { name: 'Queue item actions' }).click()
	await page.getByRole('menuitem', { name: 'Remove' }).click()

	const removeDialog = page.getByRole('dialog', { name: 'Remove from queue' })
	await expect(removeDialog).toBeVisible()
	await expect(removeDialog.getByLabel('Add to blocklist')).not.toBeChecked()
	await removeDialog.getByLabel('Skip redownload').check()
	await removeDialog.getByRole('button', { name: 'Remove', exact: true }).click()
	await expect(removeDialog).toBeHidden()
	await expect(queueRow).toBeHidden()

	const blocklistResponse = await page.request.get('/api/v1/blocklist', { params: { PageSize: 200 } })
	expect(blocklistResponse.ok(), await blocklistResponse.text()).toBe(true)
	const blocklist = await blocklistResponse.json() as { items: { releaseTitle: string }[] }
	expect(blocklist.items.some(item => item.releaseTitle.includes(TARGET_RELEASE_FRAGMENT))).toBe(false)
	await page.goto('/indexers/search')
	await page.getByPlaceholder('Release title, e.g. Harbour Lights S02E06').fill('Harbour Lights')
	await page.getByRole('button', { name: 'Search', exact: true }).click()
	const searchable = page.getByRole('row').filter({ hasText: TARGET_RELEASE_FRAGMENT })
	await expect(searchable).toBeVisible()
	await expect(searchable.getByRole('button', { name: 'Grab' })).toBeEnabled()
})
test('history: event type and source title filters narrow matching activity', async ({ page }) => {
	await signIn(page)
	await ensureLibrary(page.request)
	await ensureStubTrackerIndexer(page.request)
	const title = await grabFixtureRelease(page.request, HISTORY_FILTER_RELEASE)

	// Leave the grabbed history intact without leaving a queue item for other scenarios.
	const queue = await page.request.get('/api/v1/queue', { params: { PageSize: 250 } })
	expect(queue.ok(), await queue.text()).toBe(true)
	const queued = await queue.json() as { items: { id: number, title: string }[] }
	for (const item of queued.items.filter(item => item.title === title)) {
		const removed = await page.request.delete(`/api/v1/queue/${item.id}`, {
			params: { removeFromClient: true, blocklist: false, skipRedownload: true },
		})
		expect(removed.ok(), await removed.text()).toBe(true)
	}

	await page.goto('/activity/history')
	await expect(page.getByRole('heading', { level: 1, name: 'History' })).toBeVisible()
	await page.getByRole('combobox').click()
	await page.getByRole('option', { name: 'Grabbed' }).click()
	await page.getByPlaceholder('Search source title').fill(title)

	const matchingRows = page.getByRole('row').filter({ hasText: title })
	await expect(matchingRows.first()).toBeVisible()
	await expect(matchingRows.first().getByText('Grabbed')).toBeVisible()
	await expect(page.getByRole('row').filter({ has: page.getByText('Failed', { exact: true }) }).filter({ hasText: title })).toHaveCount(0)

	await page.getByPlaceholder('Search source title').fill('no matching activity title')
	await expect(page.getByText('No history yet.')).toBeVisible()
})

test('blocklist: select only the fixture entry and remove it', async ({ page }) => {
	await signIn(page)
	await ensureLibrary(page.request)
	await ensureStubTrackerIndexer(page.request)
	const title = await grabFixtureRelease(page.request, BLOCKLIST_FILTER_RELEASE)
	await blocklistFixtureThroughUi(page, title)

	const beforeDeleteResponse = await page.request.get('/api/v1/blocklist', { params: { PageSize: 250 } })
	expect(beforeDeleteResponse.ok(), await beforeDeleteResponse.text()).toBe(true)
	const beforeDelete = await beforeDeleteResponse.json() as { items: { id: number, releaseTitle: string }[] }
	const unrelatedIds = beforeDelete.items.filter(item => item.releaseTitle !== title).map(item => item.id)
	await page.goto('/activity/blocklist')
	await expect(page.getByRole('heading', { level: 1, name: 'Blocklist' })).toBeVisible()
	const row = page.getByRole('row').filter({ hasText: title }).first()
	await expect(row).toBeVisible()
	await row.getByRole('checkbox', { name: `Select ${title}` }).check()
	await expect(page.getByRole('button', { name: 'Remove selected (1)' })).toBeEnabled()
	await page.getByRole('button', { name: 'Remove selected (1)' }).click()
	await expect(row).toBeHidden()

	const response = await page.request.get('/api/v1/blocklist', { params: { PageSize: 250 } })
	expect(response.ok(), await response.text()).toBe(true)
	const remainingIds = new Set((await response.json() as { items: { id: number }[] }).items.map(item => item.id))
	for (const id of unrelatedIds) {
		expect(remainingIds.has(id)).toBe(true)
	}
})
