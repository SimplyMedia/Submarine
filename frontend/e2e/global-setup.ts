import { chromium } from '@playwright/test'
import type { FullConfig } from '@playwright/test'
import { E2E_PASSWORD, E2E_USERNAME } from './setup'

export const STORAGE_STATE = '.e2e/storage-state.json'

/**
 * Signs in once through the real setup or login page and shares the session with every spec,
 * so the suite stays under the login rate limit when it runs for all viewports.
 */
export default async function globalSetup(config: FullConfig) {
	const baseURL = config.projects[0]?.use.baseURL
	if (!process.env.E2E_BASE_URL || !baseURL) {
		return
	}

	const browser = await chromium.launch()
	const page = await browser.newPage({ baseURL })
	await page.goto('/')
	await page.getByRole('heading', { level: 1 }).first().waitFor()
	if (page.url().includes('/setup') || page.url().includes('/login')) {
		const onSetup = page.url().includes('/setup')
		await page.getByLabel('Username').fill(E2E_USERNAME)
		await page.getByLabel('Password').fill(E2E_PASSWORD)
		await page.getByRole('button', { name: onSetup ? 'Create account' : 'Sign in' }).click()
		await page.waitForURL(url => !/\/(login|setup)/.test(url.pathname))
	}
	await page.context().storageState({ path: STORAGE_STATE })
	await browser.close()
}
