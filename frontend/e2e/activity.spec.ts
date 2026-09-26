import { expect, test } from '@playwright/test'
import {
	ensureLibrary,
	ensureStubTrackerIndexer,
	grabTargetRelease,
	resetGrabState,
	signIn,
	TARGET_RELEASE_FRAGMENT,
} from './setup'

// The smoke suite drives a real API (dev proxy target or E2E_BASE_URL).
// Without it the suite compiles but skips.
test.skip(!process.env.E2E_BASE_URL, 'Set E2E_BASE_URL to a running Submarine API to run e2e')

test('activity: a grabbed release shows in the queue, then removing it blocklists it and logs history', async ({ page }) => {
	await signIn(page)
	await ensureLibrary(page.request)
	await ensureStubTrackerIndexer(page.request)

	await resetGrabState(page.request)
	await grabTargetRelease(page.request)

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
	await resetGrabState(page.request)
	await grabTargetRelease(page.request)

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
