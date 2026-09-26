import type { Page } from '@playwright/test'
import { expect, test } from '@playwright/test'

// Drives a real API (dev proxy target or E2E_BASE_URL). Without it the suite
// compiles but skips, matching e2e/shell.spec.ts.
test.skip(!process.env.E2E_BASE_URL, 'Set E2E_BASE_URL to a running Submarine API to run e2e')

const username = 'submarine-e2e'
const password = 'submarine-e2e-password'

async function signIn(page: Page) {
	await page.goto('/settings/profiles')
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
}

test('quality profiles: create from template, then edit and save', async ({ page }) => {
	await signIn(page)
	await page.goto('/settings/profiles')
	await expect(page.getByRole('button', { name: 'New from template' })).toBeVisible()

	// Leftovers from an interrupted run would make the template name collide.
	for (const leftover of ['HD-720p (renamed)', 'HD-720p']) {
		const existing = page.getByRole('row').filter({ hasText: leftover })
		if (await existing.count()) {
			await existing.first().getByRole('button', { name: 'Profile actions' }).click()
			await page.getByRole('menuitem', { name: 'Delete' }).click()
			await page.getByRole('button', { name: 'Delete', exact: true }).click()
			await expect(existing).toHaveCount(0)
		}
	}

	await page.getByRole('button', { name: 'New from template' }).click()
	await page.getByRole('button', { name: 'HD-720p', exact: true }).click()
	await expect(page.getByText('"HD-720p" created')).toBeVisible()

	const row = page.getByRole('row').filter({ hasText: 'HD-720p' })
	await expect(row).toBeVisible()
	await row.getByRole('button', { name: 'Profile actions' }).click()
	await page.getByRole('menuitem', { name: 'Edit' }).click()

	const dialog = page.getByRole('dialog', { name: 'Edit quality profile' })
	await expect(dialog).toBeVisible()
	const nameInput = dialog.getByLabel('Name')
	await nameInput.fill('HD-720p (renamed)')
	await dialog.getByRole('button', { name: 'Save changes' }).click()

	await expect(page.getByText('Saved')).toBeVisible()
	await expect(page.getByRole('row').filter({ hasText: 'HD-720p (renamed)' })).toBeVisible()

	// Clean up so re-runs stay idempotent.
	await page.getByRole('row').filter({ hasText: 'HD-720p (renamed)' })
		.getByRole('button', { name: 'Profile actions' }).click()
	await page.getByRole('menuitem', { name: 'Delete' }).click()
	await page.getByRole('button', { name: 'Delete', exact: true }).click()
})

test('general settings: regenerate the API key', async ({ page }) => {
	await signIn(page)
	await page.goto('/settings/general')

	const apiKeyField = page.locator('.key-row input').first()
	const originalKey = await apiKeyField.inputValue()
	expect(originalKey.length).toBeGreaterThan(0)

	await page.getByRole('button', { name: 'Regenerate' }).click()
	await page.getByRole('dialog').getByRole('button', { name: 'Regenerate' }).click()

	await expect(page.getByText('API key regenerated')).toBeVisible()
	await expect(apiKeyField).not.toHaveValue(originalKey)
})
