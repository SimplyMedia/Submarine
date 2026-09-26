import { expect, test } from '@playwright/test'
import {
	ensureLibrary,
	resetGrabState,
	signIn,
	STUB_TRACKER_BASE_URL,
	STUB_TRACKER_NAME,
	TARGET_RELEASE_FRAGMENT,
} from './setup'

// The smoke suite drives a real API (dev proxy target or E2E_BASE_URL).
// Without it the suite compiles but skips.
test.skip(!process.env.E2E_BASE_URL, 'Set E2E_BASE_URL to a running Submarine API to run e2e')

test('indexers: add a Torznab tracker, test it, then search and grab a release', async ({ page }) => {
	await signIn(page)
	await ensureLibrary(page.request)

	await page.goto('/indexers')
	await expect(page.getByRole('heading', { level: 1, name: 'Indexers' })).toBeVisible()
	await page.waitForLoadState('networkidle')

	const row = page.getByRole('row').filter({ hasText: STUB_TRACKER_NAME }).first()
	if (await row.count() === 0) {
		await page.getByRole('button', { name: 'Add indexer' }).click()
		const addDialog = page.getByRole('dialog')
		await addDialog.getByRole('button', { name: 'Torznab', exact: true }).click()
		await addDialog.getByLabel('Name').fill(STUB_TRACKER_NAME)
		await addDialog.getByLabel('Base URL').fill(STUB_TRACKER_BASE_URL)
		await addDialog.getByLabel('API path').fill('/api')
		await addDialog.getByRole('button', { name: 'Add indexer' }).click()
		await expect(page.locator('.s-toast-title', { hasText: 'Indexer added' })).toBeVisible()
	}
	await expect(row).toBeVisible()

	// Run the connection test for this indexer specifically.
	await row.getByRole('button', { name: 'Indexer actions' }).click()
	await page.getByRole('menuitem', { name: 'Edit' }).click()
	const editDialog = page.getByRole('dialog')
	await expect(editDialog).toBeVisible()
	await editDialog.getByRole('button', { name: 'Test' }).click()
	await expect(page.locator('.s-toast-title', { hasText: 'Connection successful' })).toBeVisible()
	await editDialog.getByRole('button', { name: 'Cancel' }).click()

	await resetGrabState(page.request)
	await page.goto('/indexers/search')
	await expect(page.getByRole('heading', { level: 1, name: 'Search' })).toBeVisible()
	await page.getByPlaceholder('Release title, e.g. Harbour Lights S02E06').fill('Harbour Lights')
	await page.getByRole('button', { name: 'Search', exact: true }).click()

	await expect(page.getByText('BluRay Disc-1080p').first()).toBeVisible()
	await expect(page.getByText('BluRay Remux-2160p').first()).toBeVisible()
	await expect(page.getByText('WebDL-1080p').first()).toBeVisible()

	const targetRow = page.getByRole('row').filter({ hasText: TARGET_RELEASE_FRAGMENT })
	await expect(targetRow).toHaveCount(1)
	await targetRow.getByRole('button', { name: 'Grab' }).click()
	await expect(page.locator('.s-toast-title', { hasText: /^Grabbed "Harbour\.Lights\.S02E06\.1080p\.WEB\.H264-GROUP"$/ })).toBeVisible()
})
