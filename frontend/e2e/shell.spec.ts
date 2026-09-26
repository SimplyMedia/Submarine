import { navigation } from '../app/navigation'
import type { Page } from '@playwright/test'
import { expect, test } from '@playwright/test'

// The smoke suite drives a real API (dev proxy target or E2E_BASE_URL).
// Without it the suite compiles but skips.
test.skip(!process.env.E2E_BASE_URL, 'Set E2E_BASE_URL to a running Submarine API to run e2e')

const username = 'submarine-e2e'
const password = 'submarine-e2e-password'
const untranslatedKey = /\b(?:utils|pages|components|nav|navigation|settings|common)\.[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]+)+\b/

function collectConsoleIssues(page: Page): string[] {
	const issues: string[] = []
	page.on('console', (message) => {
		if (message.type() === 'error' || message.type() === 'warning') {
			issues.push(`${message.type()}: ${message.text()}`)
		}
	})
	page.on('pageerror', (error) => {
		issues.push(`pageerror: ${error.message}`)
	})
	return issues
}

test('shell: first run, sign in, visit every page', async ({ page }) => {
	const issues = collectConsoleIssues(page)

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

	for (const item of navigation) {
		for (const path of [item.to, ...(item.children?.map(child => child.to) ?? [])]) {
			await page.goto(path)
			await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
			expect(
				await page.locator('body').innerText(),
				`raw i18n key at ${path}`,
			).not.toMatch(untranslatedKey)
			const overflow = await page.evaluate(
				() => document.documentElement.scrollWidth - document.documentElement.clientWidth,
			)
			expect(overflow, `horizontal overflow at ${path}`).toBeLessThanOrEqual(0)
		}
	}

	expect(issues, issues.join('\n')).toEqual([])
})
