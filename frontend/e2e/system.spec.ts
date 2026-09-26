import { expect, test } from '@playwright/test'

// The smoke suite drives a real API (dev proxy target or E2E_BASE_URL).
// Without it the suite compiles but skips.
test.skip(!process.env.E2E_BASE_URL, 'Set E2E_BASE_URL to a running Submarine API to run e2e')

const username = 'submarine-e2e'
const password = 'submarine-e2e-password'

test.beforeEach(async ({ page }) => {
	await page.goto('/')
	// The SPA decides where to go after it loads; wait for that decision.
	await page.getByRole('heading', { level: 1 }).first().waitFor()
	if (page.url().includes('/setup')) {
		await page.getByLabel('Username').fill(username)
		await page.getByLabel('Password').fill(password)
		await page.getByRole('button', { name: 'Create account' }).click()
	}
	else if (page.url().includes('/login')) {
		await page.getByLabel('Username').fill(username)
		await page.getByLabel('Password').fill(password)
		await page.getByRole('button', { name: 'Sign in' }).click()
	}
	await expect(page).not.toHaveURL(/\/(login|setup)/)
})

test('tasks: running a scheduled task adds a new completed history row', async ({ page }) => {
	await page.goto('/system/tasks')
	await expect(page.getByRole('heading', { level: 1, name: 'Tasks' })).toBeVisible()

	const scheduledSection = page.locator('.s-section').filter({ hasText: 'Scheduled tasks' })
	const historySection = page.locator('.s-section').filter({ hasText: 'History' })
	const latestHealthCheck = async () => {
		const commands = await (await page.request.get('/api/v1/commands', { params: { PageSize: 250 } })).json() as { items: { id: number, name: string, status: string }[] }
		return Math.max(0, ...commands.items.filter(c => c.name === 'HealthCheck' && c.status === 'COMPLETED').map(c => c.id))
	}
	const before = await latestHealthCheck()

	const taskRow = scheduledSection.getByRole('row', { name: /Health check/ })
	await expect(taskRow).toBeVisible()
	await taskRow.getByRole('button', { name: 'Run now' }).click()

	// Polls until the run completes and a new "Health check" row lands in History; scoped to
	// that task specifically so an unrelated scheduled task completing meanwhile can't interfere.
	await expect(async () => {
		expect(await latestHealthCheck()).toBeGreaterThan(before)
	}).toPass({ timeout: 30_000 })
	await expect(historySection.getByRole('row', { name: /Health check/ }).first()).toBeVisible()
})

test('backups: creating a backup completes and can be downloaded', async ({ page }) => {
	await page.goto('/system/backups')
	await expect(page.getByRole('heading', { level: 1, name: 'Backups' })).toBeVisible()

	const archivesSection = page.locator('.s-section').filter({ hasText: 'Archives' })
	// Archives are named by timestamp and listed newest first; retention caps the count, so compare names.
	const newestRow = archivesSection.locator('tbody tr').first()
	const before = await newestRow.count() ? await newestRow.locator('td').first().innerText() : ''

	await page.getByRole('button', { name: 'Back up now' }).first().click()

	await expect(newestRow.locator('td').first()).not.toHaveText(before, { timeout: 60_000 })
	await expect(newestRow).toContainText('Manual')
	const downloadPromise = page.waitForEvent('download')
	await newestRow.getByRole('button', { name: 'Download backup' }).click()
	const download = await downloadPromise
	expect(download.suggestedFilename()).toMatch(/^submarine_backup_v2_.*\.zip$/)
})
