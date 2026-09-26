import path from 'node:path'
import { expect, test } from '@playwright/test'
import { ensureRootFolder, signIn } from './setup'

test.skip(!process.env.E2E_BASE_URL, 'Set E2E_BASE_URL to a running Submarine API to run e2e')

const TMDB_ID = 155

async function ensureMovieRoot(request: Parameters<typeof ensureRootFolder>[0]): Promise<number> {
	const folderPath = path.resolve('.e2e/media/movies')
	const response = await request.get('/api/v1/root-folders')
	const folders = await response.json() as { id: number, path: string }[]
	const found = folders.find(folder => folder.path === folderPath)
	if (found) return found.id
	const created = await request.post('/api/v1/root-folders', { data: { path: folderPath, mediaKind: 'MOVIES' } })
	expect(created.ok(), await created.text()).toBe(true)
	return (await created.json() as { id: number }).id
}

async function removeMovieIfPresent(request: Parameters<typeof ensureRootFolder>[0]) {
	const list = await request.get('/api/v1/movies', { params: { PageSize: 200 } })
	const movies = (await list.json() as { items: { id: number, tmdbId: number }[] }).items
	for (const movie of movies.filter(item => item.tmdbId === TMDB_ID)) {
		const removed = await request.delete(`/api/v1/movies/${movie.id}`)
		expect(removed.ok(), await removed.text()).toBe(true)
	}
}

test('movies: add from lookup, edit details, mass edit, and delete', async ({ page }) => {
	await ensureMovieRoot(page.request)
	await removeMovieIfPresent(page.request)
	await signIn(page, '/movies')
	await page.getByRole('button', { name: 'Add movie' }).first().click()
	const lookup = page.getByRole('dialog', { name: 'Add movie' })
	await lookup.getByRole('searchbox', { name: 'Search by title' }).fill('Quiet Running')
	await lookup.getByRole('button', { name: /Quiet Running \(2027\)/ }).click()
	const addForm = page.getByRole('dialog', { name: 'Quiet Running' })
	await expect(addForm.getByRole('combobox').first()).toBeVisible()
	await addForm.getByRole('button', { name: 'Add movie' }).click()
	await expect(page).toHaveURL(/\/movies\/\d+$/)
	await expect(page.getByRole('heading', { name: /Quiet Running/ })).toBeVisible()

	const list = await page.request.get('/api/v1/movies', { params: { PageSize: 200 } })
	const movie = (await list.json() as { items: { id: number, tmdbId: number, monitored: boolean, minimumAvailability: string }[] }).items.find(item => item.tmdbId === TMDB_ID)
	expect(movie).toBeTruthy()
	const movieId = movie!.id
	await page.getByRole('button', { name: 'Actions' }).click()
	await page.getByRole('menuitem', { name: 'Edit' }).click()
	const edit = page.getByRole('dialog', { name: 'Edit movie' })
	await expect(edit).toBeVisible()
	await edit.getByRole('combobox').first().click()
	await page.getByRole('option', { name: 'Announced' }).click()
	await edit.getByRole('button', { name: 'Save changes' }).click()
	await expect(page.locator('.s-toast-title', { hasText: 'Movie updated' })).toBeVisible()
	await expect.poll(async () => {
		const response = await page.request.get(`/api/v1/movies/${movieId}`)
		return (await response.json() as { movie: { minimumAvailability: string } }).movie.minimumAvailability
	}).toBe('ANNOUNCED')

	await page.goto('/movies')
	await page.getByRole('button', { name: 'Table view' }).click()
	await page.getByRole('button', { name: 'Mass edit' }).click()
	await page.getByRole('row').filter({ hasText: 'Quiet Running' }).getByRole('checkbox').check()
	await page.getByRole('button', { name: 'Edit 1 selected' }).click()
	const bulk = page.getByRole('dialog', { name: 'Edit movies' })
	await bulk.getByRole('combobox').first().click()
	await page.getByRole('option', { name: 'Unmonitored' }).click()
	await bulk.getByRole('button', { name: 'Save changes' }).click()
	await expect.poll(async () => {
		const response = await page.request.get(`/api/v1/movies/${movieId}`)
		return (await response.json() as { movie: { monitored: boolean } }).movie.monitored
	}).toBe(false)

	await page.goto(`/movies/${movieId}`)
	await page.getByRole('button', { name: 'Actions' }).click()
	await page.getByRole('menuitem', { name: 'Delete' }).click()
	await page.getByRole('dialog', { name: 'Delete movie' }).getByRole('button', { name: 'Delete', exact: true }).click()
	await expect(page.locator('.s-toast-title', { hasText: 'Quiet Running deleted' })).toBeVisible()
	await expect.poll(async () => {
		const response = await page.request.get('/api/v1/movies', { params: { PageSize: 200 } })
		return (await response.json() as { items: { tmdbId: number }[] }).items.some(item => item.tmdbId === TMDB_ID)
	}).toBe(false)
})
