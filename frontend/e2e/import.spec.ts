import { mkdirSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { expect, test } from '@playwright/test'
import { ensureLibrary, ensureRootFolder, signIn } from './setup'

// The smoke suite drives a real API (dev proxy target or E2E_BASE_URL).
// Without it the suite compiles but skips.
test.skip(!process.env.E2E_BASE_URL, 'Set E2E_BASE_URL to a running Submarine API to run e2e')

const TRAWLER_TVDB_ID = 73739

test('manual import: matches a dropped file to an existing series episode and imports it', async ({ page }) => {
	const { seriesId } = await ensureLibrary(page.request)

	const folder = path.resolve('.e2e/manual-import')
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

test('library import: adopts an existing folder as a new series', async ({ page }) => {
	const rootFolderId = await ensureRootFolder(page.request)

	// Idempotent: remove a series left over from a previous run so the folder scans as unmapped again.
	const list = await (await page.request.get('/api/v1/series', { params: { PageSize: 200 } })).json() as { items: { id: number, tvdbId: number }[] }
	const leftover = list.items.find(series => series.tvdbId === TRAWLER_TVDB_ID)
	if (leftover) {
		const removed = await page.request.delete('/api/v1/series/editor', { data: { ids: [leftover.id], deleteFiles: true, addImportListExclusion: false } })
		expect(removed.ok(), await removed.text()).toBe(true)
	}

	const seriesFolder = path.join(path.resolve('.e2e/media/tv'), 'The Trawler (2018)')
	mkdirSync(seriesFolder, { recursive: true })
	writeFileSync(path.join(seriesFolder, 'The.Trawler.S01E01.1080p.WEB-DL.mkv'), Buffer.alloc(1024 * 1024))

	await signIn(page)
	await page.goto('/library-import')
	await expect(page.getByRole('heading', { level: 1, name: 'Import existing library' })).toBeVisible()

	const rootFolderSelect = page.getByLabel('Root folder')
	await rootFolderSelect.click()
	const tvOption = page.getByRole('option', { name: path.resolve('.e2e/media/tv') })
	if (await tvOption.count()) {
		await tvOption.click()
	}
	else {
		await page.keyboard.press('Escape')
	}
	void rootFolderId

	await page.getByRole('button', { name: 'Scan' }).click()

	const row = page.getByRole('row').filter({ hasText: 'The Trawler (2018)' })
	await expect(row).toBeVisible()
	await expect(row.getByText('The Trawler (2018)').first()).toBeVisible()

	await row.getByRole('checkbox', { name: 'Select The Trawler (2018)' }).check()
	await page.getByRole('button', { name: /Import selected/ }).click()

	await expect(page.locator('.s-toast-title', { hasText: /folder\(s\) added/ })).toBeVisible()
	await expect(row).toBeHidden()
})
