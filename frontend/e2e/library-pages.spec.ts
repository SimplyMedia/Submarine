import path from 'node:path'
import { expect, test } from '@playwright/test'
import { ensureLibrary, signIn } from './setup'

test.skip(!process.env.E2E_BASE_URL, 'Set E2E_BASE_URL to a running Submarine API to run e2e')

const COLLECTION_MOVIE_IDS = [27205, 27206, 27207]

async function ensureMovie(request: Parameters<typeof ensureLibrary>[0], tmdbId: number) {
	const list = await request.get('/api/v1/movies', { params: { PageSize: 200 } })
	const movies = (await list.json() as { items: { tmdbId: number }[] }).items
	if (movies.some(movie => movie.tmdbId === tmdbId)) return
	const roots = await request.get('/api/v1/root-folders')
	const folders = await roots.json() as { id: number, mediaKind: string }[]
	let root = folders.find(folder => folder.mediaKind === 'MOVIES')
	if (!root) {
		const created = await request.post('/api/v1/root-folders', { data: { path: path.resolve('.e2e/media/movies'), mediaKind: 'MOVIES' } })
		expect(created.ok(), await created.text()).toBe(true)
		root = await created.json() as { id: number, mediaKind: string }
	}
	const profiles = await request.get('/api/v1/quality-profiles')
	const qualityProfiles = (await profiles.json() as { items: { id: number }[] }).items
	const languages = await request.get('/api/v1/language-profiles')
	const languageProfiles = (await languages.json() as { items: { id: number }[] }).items
	const lookup = await request.get('/api/v1/movies/lookup', { params: { term: `tmdb:${tmdbId}` } })
	const movie = (await lookup.json() as { tmdbId: number, title: string }[])[0]!
	const added = await request.post('/api/v1/movies', { data: {
		tmdbId: movie.tmdbId,
		title: movie.title,
		rootFolderId: root.id,
		minimumAvailability: 'RELEASED',
		monitored: true,
		versions: [{ name: 'Main', qualityProfileId: qualityProfiles[0]!.id, languageProfileId: languageProfiles[0]!.id, rootFolderId: root.id }],
	} })
	expect(added.ok(), await added.text()).toBe(true)
}


test('collections: list a metadata-backed collection and add its missing movies', async ({ page }) => {
	for (const tmdbId of COLLECTION_MOVIE_IDS) {
		const list = await page.request.get('/api/v1/movies', { params: { PageSize: 200 } })
		const movies = (await list.json() as { items: { id: number, tmdbId: number }[] }).items
		for (const movie of movies.filter(item => item.tmdbId === tmdbId)) {
			const removed = await page.request.delete(`/api/v1/movies/${movie.id}`)
			expect(removed.ok(), await removed.text()).toBe(true)
		}
	}
	await ensureMovie(page.request, COLLECTION_MOVIE_IDS[0])
	await signIn(page, '/movies/collections')
	const collectionLink = page.getByRole('link', { name: /Abyss collection/ })
	await expect(collectionLink).toBeVisible()
	await collectionLink.click()
	await expect(page.getByRole('heading', { name: 'Abyss collection' })).toBeVisible()
	await expect(page.getByText('2 movies from this collection are not in the library yet.')).toBeVisible()
	await page.getByRole('button', { name: 'Add missing', exact: true }).click()
	const dialog = page.getByRole('dialog', { name: 'Add missing movies' })
	await dialog.getByRole('button', { name: 'Add missing movies', exact: true }).click()
	await expect(page.locator('.s-toast-title', { hasText: '2 movies added' })).toBeVisible()
	await expect(page.getByText('3 movies in the library')).toBeVisible()
	const refreshed = await page.request.get('/api/v1/movies', { params: { PageSize: 200 } })
	const owned = (await refreshed.json() as { items: { tmdbId: number }[] }).items.map(movie => movie.tmdbId)
	expect(COLLECTION_MOVIE_IDS.every(tmdbId => owned.includes(tmdbId))).toBe(true)
})

test('calendar: switch week and month views and open the tokenized iCal feed', async ({ page }) => {
	await ensureLibrary(page.request)
	await signIn(page, '/calendar')
	await expect(page.getByRole('heading', { name: 'Calendar' })).toBeVisible()
	await page.getByRole('button', { name: 'Week', exact: true }).click()
	await expect(page.locator('.calendar-grid-week')).toBeVisible()
	await page.getByRole('button', { name: 'Month', exact: true }).click()
	await expect(page.locator('.calendar-grid-week')).toHaveCount(0)
	await expect(page.locator('.calendar-grid')).toBeVisible()
	await page.getByRole('button', { name: 'iCal feed' }).click()
	const feedUrl = await page.getByLabel('iCal feed URL').inputValue()
	expect(feedUrl).toMatch(/\/api\/v1\/calendar\/feed\.ics\?token=.+/)
	const feed = await page.request.get(feedUrl)
	expect(feed.ok(), await feed.text()).toBe(true)
	expect(feed.headers()['content-type']).toContain('text/calendar')
	expect(await feed.text()).toContain('BEGIN:VCALENDAR')
})

test('wanted: show missing and cutoff lists and queue a selected search', async ({ page }) => {
	await ensureLibrary(page.request)
	await signIn(page, '/wanted/missing')
	await expect(page.getByRole('heading', { name: 'Missing' })).toBeVisible()
	await expect(page.getByRole('row').filter({ hasText: 'Harbour Lights' }).first()).toBeVisible()
	const missingRow = page.getByRole('row').filter({ hasText: 'Harbour Lights' }).first()
	await missingRow.getByRole('checkbox').check()
	await page.getByRole('button', { name: 'Search selected' }).click()
	await expect(page.locator('.s-toast-title', { hasText: /search/i })).toBeVisible()
	await page.goto('/wanted/cutoff')
	await expect(page.getByRole('heading', { name: 'Cut off' })).toBeVisible()
	const cutoffContent = page.getByRole('table').or(page.getByText('Nothing is waiting for a quality upgrade right now.')).first()
	await expect(cutoffContent).toBeVisible()
})

test('rename preview: open the series preview from its detail page', async ({ page }) => {
	const { seriesId } = await ensureLibrary(page.request)
	const response = await page.request.get('/api/v1/rename', { params: { seriesId } })
	const expected = await response.json() as { existingPath: string, newPath: string }[]
	await signIn(page, `/series/${seriesId}`)
	await page.getByRole('button', { name: 'Actions' }).click()
	await page.getByRole('menuitem', { name: 'Preview renames' }).click()
	const preview = page.getByRole('dialog', { name: 'Preview renames' })
	if (expected.length === 0) {
		await expect(preview.getByText('Every file already matches the naming format.')).toBeVisible()
		await expect(preview.getByRole('button', { name: 'Rename 0 file(s)' })).toBeDisabled()
	}
	else {
		await expect(preview.getByRole('columnheader', { name: 'Current' })).toBeVisible()
		await expect(preview.getByRole('columnheader', { name: 'New' })).toBeVisible()
		const row = preview.getByRole('row').filter({ hasText: expected[0]!.existingPath })
		await expect(row).toContainText(expected[0]!.newPath)
	}
})

test('tags: create, rename, and delete a tag', async ({ page }) => {
	const tagName = 'E2E Library Tag'
	const renamed = `${tagName} renamed`
	const list = await page.request.get('/api/v1/tags')
	for (const tag of (await list.json() as { id: number, label: string }[]).filter(item => item.label === tagName || item.label === renamed)) {
		const deleted = await page.request.delete(`/api/v1/tags/${tag.id}`)
		expect(deleted.ok(), await deleted.text()).toBe(true)
	}
	await signIn(page, '/settings/tags')
	await page.getByRole('button', { name: 'Add tag' }).first().click()
	let dialog = page.getByRole('dialog', { name: 'Add tag' })
	await dialog.getByLabel('Label').fill(tagName)
	await dialog.getByRole('button', { name: 'Add tag', exact: true }).click()
	await expect(page.getByRole('row').filter({ hasText: tagName })).toBeVisible()
	const row = page.getByRole('row').filter({ hasText: tagName })
	await row.getByRole('button', { name: 'Tag actions' }).click()
	await page.getByRole('menuitem', { name: 'Rename' }).click()
	dialog = page.getByRole('dialog', { name: 'Rename tag' })
	await dialog.getByLabel('Label').fill(renamed)
	await dialog.getByRole('button', { name: 'Save changes' }).click()
	await expect(page.getByRole('row').filter({ hasText: renamed })).toBeVisible()
	await page.getByRole('row').filter({ hasText: renamed }).getByRole('button', { name: 'Tag actions' }).click()
	await page.getByRole('menuitem', { name: 'Delete' }).click()
	await page.getByRole('dialog', { name: 'Delete tag' }).getByRole('button', { name: 'Delete tag' }).click()
})
