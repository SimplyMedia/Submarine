import { expect, test } from '@playwright/test'
import { mkdirSync, rmSync } from 'node:fs'
import path from 'node:path'

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

	await newestRow.getByRole('button', { name: 'Restore this backup' }).click()
	const restoreDialog = page.getByRole('dialog', { name: 'Restore backup?' })
	await expect(restoreDialog).toBeVisible()
	await expect(restoreDialog).toContainText(await newestRow.locator('td').first().innerText())
	await restoreDialog.getByRole('button', { name: 'Cancel' }).click()
	await expect(restoreDialog).toBeHidden()
})

test('system logs: displays stored log entries and filter controls', async ({ page }) => {
	await page.goto('/system/logs')
	await expect(page.getByRole('heading', { level: 1, name: 'Logs' })).toBeVisible()
	await expect(page.getByPlaceholder('Search message or logger')).toBeVisible()
	await expect(page.getByText('ERROR', { exact: true })).toBeVisible()
	const table = page.getByRole('table')
	await expect(table).toBeVisible()
	await expect(table.getByRole('row').first()).toContainText(/Time|Level|Logger|Message/)

})

test('system status: shows a root-folder warning and clears it after the folder is restored', async ({ page }) => {
	const folder = path.resolve('.e2e/health-root-folder')
	rmSync(folder, { recursive: true, force: true })
	mkdirSync(folder, { recursive: true })
	const rootFolderResponse = await page.request.post('/api/v1/root-folders', { data: { path: folder, mediaKind: 'SERIES' } })
	expect(rootFolderResponse.ok(), await rootFolderResponse.text()).toBe(true)
	const rootFolder = await rootFolderResponse.json() as { id: number }
	rmSync(folder, { recursive: true, force: true })

	const runHealthCheck = async () => {
		const queued = await page.request.post('/api/v1/commands', { data: { name: 'HealthCheck' } })
		expect(queued.ok(), await queued.text()).toBe(true)
		const command = await queued.json() as { id: number }
		await expect(async () => {
			const response = await page.request.get(`/api/v1/commands/${command.id}`)
			expect(response.ok()).toBe(true)
			const result = await response.json() as { status: string }
			expect(result.status).toBe('COMPLETED')
		}).toPass({ timeout: 30_000 })
	}
	const hasMissingFolderIssue = async () => {
		const response = await page.request.get('/api/v1/health')
		const issues = await response.json() as { message: string }[]
		return issues.some(issue => issue.message.includes(folder))
	}
	try {
		await runHealthCheck()
		await expect(async () => expect(await hasMissingFolderIssue()).toBe(true)).toPass({ timeout: 15_000 })
		await page.goto('/system/status')
		await expect(page.getByRole('heading', { level: 1, name: 'System' })).toBeVisible()
		const healthSection = page.locator('.s-section').filter({ hasText: 'Health issues' })
		await expect(healthSection.getByRole('row').filter({ hasText: folder })).toContainText('Error')

		mkdirSync(folder, { recursive: true })
		await runHealthCheck()
		await expect(async () => expect(await hasMissingFolderIssue()).toBe(false)).toPass({ timeout: 15_000 })
		await page.goto('/system/status')
		await expect(page.locator('.s-section').filter({ hasText: 'Health issues' }).getByRole('row').filter({ hasText: folder })).toHaveCount(0)
	}
	finally {
		mkdirSync(folder, { recursive: true })
		await page.request.delete(`/api/v1/root-folders/${rootFolder.id}`)
		rmSync(folder, { recursive: true, force: true })
	}
})
