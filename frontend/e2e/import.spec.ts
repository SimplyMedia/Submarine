import { mkdirSync, rmSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { expect, test } from '@playwright/test'
import { ensureLibrary, ensureRootFolder, signIn } from './setup'

// The smoke suite drives a real API (dev proxy target or E2E_BASE_URL).
// Without it the suite compiles but skips.
test.skip(!process.env.E2E_BASE_URL, 'Set E2E_BASE_URL to a running Submarine API to run e2e')

const TRAWLER_TVDB_ID = 73739
const ABYSS_TMDB_ID = 27205
const downloadsFolder = path.resolve('.e2e/downloads')
const seriesFolder = path.join(path.resolve('.e2e/media/tv'), 'The Trawler (2018)')
const movieFolder = path.join(path.resolve('.e2e/media/movies'), 'Into the Abyss (2020)')

test.afterEach(async ({ page }) => {
	rmSync(path.join(downloadsFolder, 'Harbour.Lights.S02E07.1080p.WEB.H264-GROUP.mkv'), { force: true })
	rmSync(seriesFolder, { recursive: true, force: true })
	rmSync(movieFolder, { recursive: true, force: true })

	const seriesResponse = await page.request.get('/api/v1/series', { params: { PageSize: 200 } })
	if (seriesResponse.ok()) {
		const series = (await seriesResponse.json() as { items: { id: number, tvdbId: number }[] }).items
		const harbour = series.find(item => item.tvdbId === 121361)
		if (harbour) {
			const episodesResponse = await page.request.get('/api/v1/episodes', { params: { seriesId: harbour.id, seasonNumber: 2, includeFiles: true } })
			if (episodesResponse.ok()) {
				const episodes = await episodesResponse.json() as { episodeNumber: number, files: { id: number }[] }[]
				for (const file of episodes.find(episode => episode.episodeNumber === 7)?.files ?? []) {
					await page.request.delete(`/api/v1/episode-files/${file.id}`)
				}
			}
		}
		for (const item of series.filter(item => item.tvdbId === TRAWLER_TVDB_ID)) {
			await page.request.delete(`/api/v1/series/${item.id}`)
		}
	}
	const moviesResponse = await page.request.get('/api/v1/movies', { params: { PageSize: 200 } })
	if (moviesResponse.ok()) {
		const movies = (await moviesResponse.json() as { items: { id: number, tmdbId: number }[] }).items
		for (const item of movies.filter(item => item.tmdbId === ABYSS_TMDB_ID)) {
			await page.request.delete(`/api/v1/movies/${item.id}`)
		}
	}
})

test('manual import: matches a dropped file to an existing series episode and imports it', async ({ page }) => {
	const { seriesId } = await ensureLibrary(page.request)

	const folder = downloadsFolder
	mkdirSync(folder, { recursive: true })
	const fileName = 'Harbour.Lights.S02E07.1080p.WEB.H264-GROUP.mkv'
	writeFileSync(path.join(folder, fileName), Buffer.alloc(1024 * 1024))

	// Idempotent: clear any episode file left over from a previous run of this spec.
	const episodes = await (await page.request.get('/api/v1/episodes', { params: { seriesId, seasonNumber: 2, includeFiles: true } })).json() as { episodeNumber: number, files: { id: number }[] }[]
	const previous = episodes.find(episode => episode.episodeNumber === 7)
	for (const file of previous?.files ?? []) {
		await page.request.delete(`/api/v1/episode-files/${file.id}`)
	}

	await signIn(page)
	await page.goto('/activity/import')
	await expect(page.getByRole('heading', { level: 1, name: 'Manual import' })).toBeVisible()

	await page.getByRole('textbox', { name: 'Folder' }).fill(folder)
	await page.getByRole('button', { name: 'Scan folder' }).click()

	const row = page.getByRole('row').filter({ hasText: fileName })
	await expect(row).toBeVisible()

	await row.getByRole('button', { name: 'Pick a series', exact: true }).click()
	await page.getByRole('dialog').getByRole('button', { name: 'Harbour Lights', exact: false }).click()

	await row.getByRole('checkbox', { name: `Select ${fileName}` }).check()
	await page.getByRole('button', { name: /Import selected/ }).click()

	await expect(page.locator('.s-toast-title', { hasText: 'Imported 1 file(s)' })).toBeVisible()
	await expect(row).toBeHidden()

	const refreshed = await (await page.request.get('/api/v1/episodes', { params: { seriesId, seasonNumber: 2, includeFiles: true } })).json() as { episodeNumber: number, files: { id: number }[] }[]
	const imported = refreshed.find(episode => episode.episodeNumber === 7)
	expect(imported?.files.length).toBeGreaterThan(0)
})

test('library import: adopts an existing series folder using its proposed metadata match', async ({ page }) => {
	await ensureRootFolder(page.request)

	const list = await (await page.request.get('/api/v1/series', { params: { PageSize: 200 } })).json() as { items: { id: number, tvdbId: number }[] }
	for (const leftover of list.items.filter(series => series.tvdbId === TRAWLER_TVDB_ID)) {
		const removed = await page.request.delete(`/api/v1/series/${leftover.id}`)
		expect(removed.ok(), await removed.text()).toBe(true)
	}

	mkdirSync(seriesFolder, { recursive: true })
	writeFileSync(path.join(seriesFolder, 'The.Trawler.S01E01.1080p.WEB-DL.mkv'), Buffer.alloc(1024 * 1024))

	await signIn(page)
	await page.goto('/library-import')
	await expect(page.getByRole('heading', { level: 1, name: 'Import existing library' })).toBeVisible()
	await page.getByLabel('Root folder').click()
	await page.getByRole('option', { name: path.resolve('.e2e/media/tv') }).click()
	await page.getByRole('button', { name: 'Scan' }).click()

	const row = page.getByRole('row').filter({ hasText: 'The Trawler (2018)' })
	await expect(row).toBeVisible()
	await expect(row.getByRole('button').filter({ hasText: 'The Trawler (2018)' })).toBeVisible()
	await row.getByRole('checkbox', { name: 'Select The Trawler (2018)' }).check()
	await page.getByRole('button', { name: /Import selected/ }).click()

	await expect(page.locator('.s-toast-title', { hasText: /folder\(s\) added/ })).toBeVisible()
	await expect(row).toBeHidden()
	const added = await page.request.get('/api/v1/series', { params: { PageSize: 200 } })
	const addedItems = (await added.json() as { items: { tvdbId: number, title: string }[] }).items
	expect(addedItems).toContainEqual(expect.objectContaining({ tvdbId: TRAWLER_TVDB_ID, title: 'The Trawler' }))
	await page.goto('/series')
	await expect(page.getByText('The Trawler', { exact: true })).toBeVisible()
})

test('library import: adopts an existing movie folder using its proposed metadata match', async ({ page }) => {
	const rootPath = path.resolve('.e2e/media/movies')
	const rootsResponse = await page.request.get('/api/v1/root-folders')
	const roots = await rootsResponse.json() as { id: number, path: string, mediaKind: string }[]
	if (!roots.some(folder => folder.path === rootPath)) {
		const created = await page.request.post('/api/v1/root-folders', { data: { path: rootPath, mediaKind: 'MOVIES' } })
		expect(created.ok(), await created.text()).toBe(true)
	}

	const list = await (await page.request.get('/api/v1/movies', { params: { PageSize: 200 } })).json() as { items: { id: number, tmdbId: number }[] }
	for (const leftover of list.items.filter(movie => movie.tmdbId === ABYSS_TMDB_ID)) {
		const removed = await page.request.delete(`/api/v1/movies/${leftover.id}`)
		expect(removed.ok(), await removed.text()).toBe(true)
	}
	mkdirSync(movieFolder, { recursive: true })
	writeFileSync(path.join(movieFolder, 'Into.the.Abyss.2020.1080p.WEB-DL.mkv'), Buffer.alloc(1024 * 1024))

	await signIn(page)
	await page.goto('/library-import')
	await page.getByLabel('Root folder').click()
	await page.getByRole('option', { name: rootPath }).click()
	await page.getByRole('button', { name: 'Scan' }).click()

	const row = page.getByRole('row').filter({ hasText: 'Into the Abyss (2020)' })
	await expect(row).toBeVisible()
	await expect(row.getByRole('button').filter({ hasText: 'Into the Abyss (2020)' })).toBeVisible()
	await row.getByRole('checkbox', { name: 'Select Into the Abyss (2020)' }).check()
	await page.getByRole('button', { name: /Import selected/ }).click()

	await expect(page.locator('.s-toast-title', { hasText: /folder\(s\) added/ })).toBeVisible()
	await expect(row).toBeHidden()
	const added = await page.request.get('/api/v1/movies', { params: { PageSize: 200 } })
	const addedItems = (await added.json() as { items: { tmdbId: number, title: string }[] }).items
	expect(addedItems).toContainEqual(expect.objectContaining({ tmdbId: ABYSS_TMDB_ID, title: 'Into the Abyss' }))
	await page.goto('/movies')
	await expect(page.getByText('Into the Abyss', { exact: true })).toBeVisible()
})
