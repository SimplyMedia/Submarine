import path from 'node:path'
import { expect, test } from '@playwright/test'
import { signIn } from './setup'

test.skip(!process.env.E2E_BASE_URL, 'Set E2E_BASE_URL to a running Submarine API to run e2e')
const metadataPort = process.env.E2E_MOCK_PORT_BASE ? Number(process.env.E2E_MOCK_PORT_BASE) : 5100
let originalImportListConfig: { cleanLibraryLevel: string } | undefined

test.afterEach(async ({ page }) => {
	for (const [endpoint, name] of [
		['/api/v1/notifications', 'E2E webhook connection'],
		['/api/v1/download-clients', 'E2E blackhole client'],
		['/api/v1/import-lists', 'E2E custom JSON list'],
	] as const) {
		const response = await page.request.get(endpoint, { params: { PageSize: 200 } })
		if (!response.ok()) continue
		const body = await response.json() as { id: number, name: string }[] | { items: { id: number, name: string }[] }
		const entries = Array.isArray(body) ? body : body.items
		for (const entry of entries.filter(item => item.name === name)) {
			const deleted = await page.request.delete(`${endpoint}/${entry.id}`)
			expect(deleted.ok(), await deleted.text()).toBe(true)
		}
	}
	if (originalImportListConfig) {
		const restored = await page.request.put('/api/v1/config/import-list', { data: originalImportListConfig })
		expect(restored.ok(), await restored.text()).toBe(true)
		originalImportListConfig = undefined
	}
})


test('connect: add and test a webhook, then preserve its enabled event selections', async ({ page }) => {
	await signIn(page)
	const name = 'E2E webhook connection'
	const existing = await page.request.get('/api/v1/notifications')
	if (existing.ok()) {
		const entries = await existing.json() as { id: number, name: string }[]
		for (const entry of entries.filter(item => item.name === name)) await page.request.delete(`/api/v1/notifications/${entry.id}`)
	}

	await page.goto('/settings/connect')
	await page.getByRole('button', { name: 'Add connection' }).first().click()
	const dialog = page.getByRole('dialog', { name: 'Add connection' })
	await dialog.getByRole('button', { name: 'Webhook', exact: true }).click()
	await dialog.getByLabel('Name', { exact: true }).fill(name)
	await dialog.getByLabel('URL').fill(`http://localhost:${metadataPort}/webhook`)
	await dialog.getByLabel('Grab').check()
	await dialog.getByLabel('Import').uncheck()
	await dialog.getByRole('button', { name: 'Test connection' }).click()
	await expect(page.locator('.s-toast-title', { hasText: 'Test succeeded' })).toBeVisible()
	await dialog.getByRole('button', { name: 'Add connection' }).click()

	const row = page.getByRole('row').filter({ hasText: name })
	await expect(row).toBeVisible()
	await expect(row).toContainText('Grab')
	await expect(row).not.toContainText('Import')
	const saved = await page.request.get('/api/v1/notifications')
	const notifications = await saved.json() as { name: string, onGrab: boolean, onImport: boolean }[]
	expect(notifications.find(item => item.name === name)).toMatchObject({ onGrab: true, onImport: false })
})

test('download clients: add a blackhole client, test it, and persist removal toggles', async ({ page }) => {
	await signIn(page)
	const name = 'E2E blackhole client'
	const existing = await page.request.get('/api/v1/download-clients')
	if (existing.ok()) {
		const response = await existing.json() as { items?: { id: number, name: string }[], id?: number, name?: string }[]
		const entries = Array.isArray(response) ? response : response.items ?? []
		for (const entry of entries.filter(item => item.name === name)) await page.request.delete(`/api/v1/download-clients/${entry.id}`)
	}

	await page.goto('/settings/download-clients')
	await page.getByRole('button', { name: 'Add download client' }).first().click()
	await page.getByRole('button', { name: /Torrent blackhole/i }).click()
	const dialog = page.getByRole('dialog')
	await dialog.getByLabel('Name').fill(name)
	await dialog.getByLabel('Torrent folder').fill(path.resolve('.e2e/downloads/torrents'))
	await dialog.getByLabel('Watch folder').fill(path.resolve('.e2e/downloads/watch'))
	await dialog.getByLabel('Remove completed downloads').check()
	await dialog.getByLabel('Remove failed downloads').check()
	await dialog.getByRole('button', { name: 'Test connection' }).click()
	await expect(page.locator('.s-toast-title', { hasText: 'Connection successful' })).toBeVisible()
	await dialog.getByRole('button', { name: 'Add download client' }).click()

	const row = page.getByRole('row').filter({ hasText: name })
	await expect(row).toBeVisible()
	await row.getByRole('button', { name: 'Download client actions' }).click()
	await page.getByRole('menuitem', { name: 'Test' }).click()
	await expect(page.locator('.s-toast-title', { hasText: 'Connection successful' })).toBeVisible()
	await row.getByRole('button', { name: 'Download client actions' }).click()
	await page.getByRole('menuitem', { name: 'Edit' }).click()
	const edit = page.getByRole('dialog')
	await expect(edit.getByLabel('Remove completed downloads')).toBeChecked()
	await expect(edit.getByLabel('Remove failed downloads')).toBeChecked()
	await edit.getByLabel('Remove completed downloads').uncheck()
	await edit.getByRole('button', { name: 'Save changes' }).click()
	await expect(page.locator('.s-toast-title', { hasText: 'Download client saved' })).toBeVisible()
	const listed = await page.request.get('/api/v1/download-clients')
	const clientBody = await listed.json() as { items?: { id: number, name: string, removeCompleted: boolean, removeFailed: boolean }[], id?: number, name?: string, removeCompleted?: boolean, removeFailed?: boolean }[]
	const clients = Array.isArray(clientBody) ? clientBody : clientBody.items ?? []
	expect(clients.find(client => client.name === name)).toMatchObject({ removeCompleted: false, removeFailed: true })

	await page.getByRole('row').filter({ hasText: name }).getByRole('button', { name: 'Download client actions' }).click()
	await page.getByRole('menuitem', { name: 'Delete' }).click()
	const deleteDialog = page.getByRole('dialog', { name: 'Delete download client' })
	await deleteDialog.getByRole('button', { name: 'Delete download client' }).click()
	await expect(page.getByRole('row').filter({ hasText: name })).toHaveCount(0)
})

test('import lists: add and sync custom JSON, manage an exclusion, and save clean-library level', async ({ page }) => {
	await signIn(page)
	const configResponse = await page.request.get('/api/v1/config/import-list')
	expect(configResponse.ok(), await configResponse.text()).toBe(true)
	originalImportListConfig = await configResponse.json() as { cleanLibraryLevel: string }
	const name = 'E2E custom JSON list'
	const existing = await page.request.get('/api/v1/import-lists')
	if (existing.ok()) {
		const entries = await existing.json() as { id: number, name: string }[]
		for (const entry of entries.filter(item => item.name === name)) await page.request.delete(`/api/v1/import-lists/${entry.id}`)
	}
	await page.goto('/settings/import-lists')
	const cleanLevel = page.locator('#clean-library-level')
	const previousCleanLevel = await cleanLevel.textContent()
	const nextCleanLevel = previousCleanLevel?.trim() === 'Log only' ? 'Disabled' : 'Log only'
	await cleanLevel.click()
	await page.getByRole('option', { name: nextCleanLevel, exact: true }).click()
	await page.getByRole('button', { name: 'Save changes' }).click()

	await page.getByRole('button', { name: 'Add import list' }).first().click()
	const dialog = page.getByRole('dialog', { name: 'Add import list' })
	await dialog.getByRole('button', { name: 'Custom', exact: true }).click()
	await dialog.getByLabel('Name').fill(name)
	await dialog.getByLabel('Enable automatic add').uncheck()
	await dialog.getByLabel('Media kind').click()
	await page.getByRole('option', { name: 'Movies', exact: true }).click()
	await dialog.getByLabel('URL').fill(`http://localhost:${metadataPort}/custom-import-list`)
	await dialog.getByRole('button', { name: 'Add import list' }).click()
	const row = page.getByRole('row').filter({ hasText: name })
	await expect(row).toBeVisible()
	await row.getByRole('button', { name: 'Import list actions' }).click()
	await page.getByRole('menuitem', { name: 'Test' }).click()
	await expect(page.locator('.s-toast-title', { hasText: /test succeeded|connection successful/i })).toBeVisible()
	const addedLists = await page.request.get('/api/v1/import-lists')
	const addedList = (await addedLists.json() as { id: number, name: string }[]).find(item => item.name === name)
	expect(addedList).toBeDefined()
	const sync = await page.request.post('/api/v1/commands', { data: { name: 'ImportListSync', importListId: addedList!.id } })
	expect(sync.ok(), await sync.text()).toBe(true)
	const syncCommand = await sync.json() as { id: number }
	await expect(async () => {
		const response = await page.request.get(`/api/v1/commands/${syncCommand.id}`)
		expect(response.ok()).toBe(true)
		expect((await response.json() as { status: string }).status).toBe('COMPLETED')
	}).toPass({ timeout: 30_000 })
	const preview = await page.request.get(`/api/v1/import-lists/${addedList!.id}/preview`)
	expect(preview.ok(), await preview.text()).toBe(true)
	expect(await preview.json()).toEqual(expect.arrayContaining([expect.objectContaining({ title: 'Mock Import Film' })]))

	await page.getByRole('button', { name: 'Add exclusion' }).first().click()
	const exclusionDialog = page.getByRole('dialog', { name: 'Add exclusion' })
	await exclusionDialog.getByLabel('Title').fill('E2E excluded mock movie')
	await exclusionDialog.getByLabel('Year').fill('2025')
	await exclusionDialog.getByLabel('TMDB id').fill('999901')
	await exclusionDialog.getByRole('button', { name: 'Add exclusion' }).click()
	const exclusion = page.getByRole('row').filter({ hasText: 'E2E excluded mock movie' })
	await expect(exclusion).toBeVisible()
	await exclusion.getByRole('button', { name: 'Remove' }).click()
	await expect(exclusion).toHaveCount(0)

	if (previousCleanLevel) {
		await cleanLevel.click()
		await page.getByRole('option', { name: previousCleanLevel.trim(), exact: true }).click()
		await page.getByRole('button', { name: 'Save changes' }).click()
	}
})
