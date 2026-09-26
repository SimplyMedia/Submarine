import { expect, test } from '@playwright/test'
import { ensureRootFolder, signIn } from './setup'

test.skip(!process.env.E2E_BASE_URL, 'Set E2E_BASE_URL to a running Submarine API to run e2e')

const TVDB_ID = 328487

test('series: add from metadata lookup, edit monitoring, season pass, mass edit, and delete', async ({ page }) => {
	await ensureRootFolder(page.request)
	const before = await page.request.get('/api/v1/series', { params: { PageSize: 200 } })
	const beforeItems = (await before.json() as { items: { id: number, tvdbId: number }[] }).items
	for (const series of beforeItems.filter(item => item.tvdbId === TVDB_ID)) {
		const response = await page.request.delete('/api/v1/series/editor', { data: { ids: [series.id], deleteFiles: false, addImportListExclusion: false } })
		expect(response.ok(), await response.text()).toBe(true)
	}
	await signIn(page, '/series')
	await page.getByRole('button', { name: 'Add series' }).first().click()
	const lookup = page.getByRole('dialog', { name: 'Add series' })
	await lookup.getByRole('searchbox', { name: 'Search by title' }).fill('Sonar')
	await lookup.getByRole('button', { name: /Sonar \(2024\)/ }).click()
	const addForm = page.getByRole('dialog', { name: 'Sonar' })
	await expect(addForm.getByRole('combobox').first()).toBeVisible()
	await addForm.getByRole('button', { name: 'Add series' }).click()
	await expect(page).toHaveURL(/\/series\/\d+$/)
	await expect(page.getByRole('heading', { name: /Sonar/ })).toBeVisible()

	const created = await page.request.get('/api/v1/series', { params: { PageSize: 200 } })
	const series = (await created.json() as { items: { id: number, tvdbId: number }[] }).items.find(item => item.tvdbId === TVDB_ID)
	expect(series).toBeTruthy()
	const seriesId = series!.id
	const detail = page.getByRole('button', { name: 'Toggle monitored for Sonar' })
	await detail.click()
	await expect.poll(async () => {
		const response = await page.request.get(`/api/v1/series/${seriesId}`)
		return (await response.json() as { series: { monitored: boolean } }).series.monitored
	}).toBe(false)

	await page.getByRole('button', { name: 'Toggle monitored for season 1' }).click()
	await expect.poll(async () => {
		const response = await page.request.get('/api/v1/episodes', { params: { seriesId, seasonNumber: 1 } })
		return (await response.json() as { monitored: boolean }[]).every(episode => !episode.monitored)
	}).toBe(true)
	await page.getByRole('button', { name: 'Toggle monitored for episode 1' }).first().click()
	await expect.poll(async () => {
		const response = await page.request.get('/api/v1/episodes', { params: { seriesId } })
		return (await response.json() as { episodeNumber: number, monitored: boolean }[]).find(episode => episode.episodeNumber === 1)?.monitored
	}).toBe(true)
	await page.getByRole('button', { name: 'Actions' }).click()
	await page.getByRole('menuitem', { name: 'Edit' }).click()
	const detailEdit = page.getByRole('dialog', { name: 'Edit series' })
	await detailEdit.getByRole('combobox').nth(1).click()
	await page.getByRole('option', { name: 'DVD order' }).click()
	await detailEdit.getByRole('button', { name: 'Save changes' }).click()
	await expect(page.locator('.s-toast-title', { hasText: 'Series updated' })).toBeVisible()
	await expect.poll(async () => {
		const response = await page.request.get(`/api/v1/series/${seriesId}`)
		return (await response.json() as { series: { numbering: string } }).series.numbering
	}).toBe('DVD')

	await page.getByRole('button', { name: 'Season pass' }).click()
	const seasonPass = page.getByRole('dialog', { name: 'Season pass' })
	await seasonPass.getByRole('combobox').first().click()
	await page.keyboard.press('Escape')
	await seasonPass.getByRole('button', { name: 'Apply' }).click()
	await expect.poll(async () => {
		const response = await page.request.get('/api/v1/episodes', { params: { seriesId, seasonNumber: 1 } })
		return (await response.json() as { monitored: boolean }[]).every(episode => episode.monitored)
	}).toBe(true)

	await page.goto('/series')
	await page.getByRole('button', { name: 'Table view' }).click()
	await page.getByRole('button', { name: 'Mass edit' }).click()
	await page.getByRole('row').filter({ hasText: 'Sonar' }).getByRole('checkbox').check()
	await page.getByRole('button', { name: 'Edit 1 selected' }).click()
	const edit = page.getByRole('dialog', { name: 'Edit series' })
	await edit.getByRole('combobox').first().click()
	await page.getByRole('option', { name: 'Unmonitored' }).click()
	await edit.getByRole('button', { name: 'Save changes' }).click()
	await expect.poll(async () => {
		const response = await page.request.get(`/api/v1/series/${seriesId}`)
		return (await response.json() as { series: { monitored: boolean } }).series.monitored
	}).toBe(false)
	await page.goto(`/series/${seriesId}`)

	await page.getByRole('button', { name: 'Actions' }).click()
	await page.getByRole('menuitem', { name: 'Delete' }).click()
	await page.getByRole('dialog', { name: 'Delete series' }).getByRole('button', { name: 'Delete', exact: true }).click()
	await expect(page.locator('.s-toast-title', { hasText: 'Sonar deleted' })).toBeVisible()
	await expect.poll(async () => {
		const response = await page.request.get('/api/v1/series', { params: { PageSize: 200 } })
		return (await response.json() as { items: { tvdbId: number }[] }).items.some(item => item.tvdbId === TVDB_ID)
	}).toBe(false)
})
