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
		await page.getByRole('button', { name: 'Add indexer' }).last().click()
		const addDialog = page.getByRole('dialog')
		await addDialog.getByRole('button', { name: 'Torznab', exact: true }).click()
		await addDialog.getByLabel('Name').fill(STUB_TRACKER_NAME)
		await addDialog.getByLabel('Base URL').fill(STUB_TRACKER_BASE_URL)
		await addDialog.getByLabel('API path').fill('/api')
		await addDialog.getByLabel('VIP expiration').fill('2027-01-15')
		await addDialog.getByLabel('Query limit').fill('100')
		await addDialog.getByLabel('Grab limit').fill('10')
		await addDialog.getByLabel('Limits unit').click()
		await page.getByRole('option', { name: 'Per hour' }).click()
		await addDialog.getByLabel('Season search maximum single episode age (days)').fill('14')
		await addDialog.getByRole('checkbox', { name: 'freeleech', exact: true }).check()
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
	await expect(editDialog.getByLabel('VIP expiration')).toHaveValue('2027-01-15')
	await expect(editDialog.getByLabel('Query limit')).toHaveValue('100')
	await expect(editDialog.getByLabel('Grab limit')).toHaveValue('10')
	await expect(editDialog.getByLabel('Season search maximum single episode age (days)')).toHaveValue('14')
	await expect(editDialog.getByRole('checkbox', { name: 'freeleech', exact: true })).toBeChecked()
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
	await page.getByLabel('Category').click()
	const categorySearch = page.waitForRequest(request => request.url().includes('/api/v1/search') && request.url().includes('categories=5000'))
	await page.getByRole('option', { name: 'TV', exact: true }).click()
	await categorySearch
	await expect(page.getByRole('row').filter({ hasText: TARGET_RELEASE_FRAGMENT })).toHaveCount(1)
	await page.getByRole('combobox', { name: 'Indexer', exact: true }).click()
	const indexerSearch = page.waitForRequest(request => request.url().includes('/api/v1/search') && request.url().includes('indexerIds='))
	await page.getByRole('option', { name: STUB_TRACKER_NAME, exact: true }).click()
	await indexerSearch
	await expect(page.getByRole('row').filter({ hasText: TARGET_RELEASE_FRAGMENT })).toHaveCount(1)
	await page.getByLabel('Search type').click()
	const tvSearch = page.waitForRequest(request => request.url().includes('/api/v1/search') && request.url().includes('type=tv'))
	await page.getByRole('option', { name: 'TV', exact: true }).click()
	await tvSearch
	await resetGrabState(page.request)
	await targetRow.getByRole('checkbox').check()
	await expect(page.getByRole('button', { name: 'Grab selected' })).toBeVisible()
	await page.getByRole('button', { name: 'Grab selected' }).click()
	await expect(page.locator('.s-toast-title', { hasText: '1 releases grabbed' })).toBeVisible()
	await expect(page.getByRole('button', { name: 'Next' })).toBeDisabled()
})

test('interactive search: shows rejected releases with decision reasons', async ({ page }) => {
	await signIn(page)
	await ensureLibrary(page.request)
	await ensureStubTrackerIndexer(page.request)
	await resetGrabState(page.request)
	const name = 'E2E search rejection profile'
	const profileResponse = await page.request.get('/api/v1/release-profiles')
	const profiles = (await profileResponse.json() as { items: { id: number, name: string }[] }).items
	for (const profile of profiles.filter(item => item.name === name)) await page.request.delete(`/api/v1/release-profiles/${profile.id}`)
	const created = await page.request.post('/api/v1/release-profiles', {
		data: { name, enabled: true, required: ['E2E-NOT-FOUND'], ignored: [], indexerId: null, tags: [] },
	})
	expect(created.ok(), await created.text()).toBe(true)
	const releaseProfile = await created.json() as { id: number }
	try {
		await page.goto('/indexers/search')
		await page.getByPlaceholder('Release title, e.g. Harbour Lights S02E06').fill('Harbour Lights')
		await page.getByRole('button', { name: 'Search', exact: true }).click()

		const results = page.getByRole('table')
		const rejected = results.getByRole('row').filter({ hasText: TARGET_RELEASE_FRAGMENT })
		await expect(rejected).toBeVisible()
		const score = rejected.locator('.release-score-rejected')
		await expect(score).toBeVisible()
		await score.hover()
		await expect(page.getByRole('tooltip')).toContainText('E2E-NOT-FOUND')
	}
	finally {
		await page.request.delete(`/api/v1/release-profiles/${releaseProfile.id}`)
	}
})
