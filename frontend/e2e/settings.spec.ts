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
	// The seeded profile shows once the list has loaded, so the leftover check below sees real rows.
	await expect(page.getByRole('row').filter({ hasText: 'Any' })).toBeVisible()

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
	await expect(page.locator('.s-toast-title', { hasText: '"HD-720p" created' })).toBeVisible()

	const row = page.getByRole('row').filter({ hasText: 'HD-720p' })
	await expect(row).toBeVisible()
	await row.getByRole('button', { name: 'Profile actions' }).click()
	await page.getByRole('menuitem', { name: 'Edit' }).click()

	const dialog = page.getByRole('dialog', { name: 'Edit quality profile' })
	await expect(dialog).toBeVisible()
	const nameInput = dialog.getByLabel('Name')
	await nameInput.fill('HD-720p (renamed)')
	await dialog.getByRole('button', { name: 'Save changes' }).click()

	await expect(page.locator('.s-toast-title', { hasText: 'Saved' })).toBeVisible()
	await expect(page.getByRole('row').filter({ hasText: 'HD-720p (renamed)' })).toBeVisible()

	// Clean up so re-runs stay idempotent.
	await page.getByRole('row').filter({ hasText: 'HD-720p (renamed)' })
		.getByRole('button', { name: 'Profile actions' }).click()
	await page.getByRole('menuitem', { name: 'Delete' }).click()
	await page.getByRole('button', { name: 'Delete', exact: true }).click()
	await expect(page.getByRole('row').filter({ hasText: 'HD-720p' })).toHaveCount(0)
})

test('general settings: regenerate the API key', async ({ page }) => {
	await signIn(page)
	await page.goto('/settings/general')

	const apiKeyField = page.locator('.key-row input').first()
	await expect(apiKeyField).not.toHaveValue('')
	const originalKey = await apiKeyField.inputValue()
	expect(originalKey.length).toBeGreaterThan(0)

	await page.getByRole('button', { name: 'Regenerate' }).click()
	await page.getByRole('dialog').getByRole('button', { name: 'Regenerate' }).click()

	await expect(page.locator('.s-toast-title', { hasText: 'API key regenerated' })).toBeVisible()
	await expect(apiKeyField).not.toHaveValue(originalKey)
})
type GeneralConfig = {
	authMethod?: string
	authenticationRequired?: string
	urlBase?: string
	instanceName?: string
	logLevel?: string
	branch?: string
	applicationUrl?: string
	trustedProxies?: string
	certificateValidation?: string
	proxyEnabled?: boolean
	proxyType?: string
	proxyHost?: string
	proxyPort?: number
	proxyUsername?: string | null
	proxyPassword?: string | null
	proxyBypassFilter?: string
	proxyBypassLocalAddresses?: boolean
	backupFolder?: string
	backupIntervalDays?: number
	backupRetention?: number
}

function editableGeneralConfig(config: GeneralConfig): GeneralConfig {
	return {
		urlBase: config.urlBase,
		instanceName: config.instanceName,
		logLevel: config.logLevel,
		branch: config.branch,
		applicationUrl: config.applicationUrl,
		authMethod: config.authMethod,
		authenticationRequired: config.authenticationRequired,
		trustedProxies: config.trustedProxies,
		certificateValidation: config.certificateValidation,
		proxyEnabled: config.proxyEnabled,
		proxyType: config.proxyType,
		proxyHost: config.proxyHost,
		proxyPort: config.proxyPort,
		proxyUsername: config.proxyUsername,
		proxyPassword: config.proxyPassword,
		proxyBypassFilter: config.proxyBypassFilter,
		proxyBypassLocalAddresses: config.proxyBypassLocalAddresses,
		backupFolder: config.backupFolder,
		backupIntervalDays: config.backupIntervalDays,
		backupRetention: config.backupRetention,
	}
}

test('general settings: security, proxy, and backup configuration roundtrip', async ({ page }) => {
	await signIn(page)
	const initialResponse = await page.request.get('/api/v1/config/general')
	expect(initialResponse.ok(), await initialResponse.text()).toBe(true)
	const original = await initialResponse.json() as GeneralConfig

	try {
		await page.goto('/settings/general')
		await page.locator('#general-instance-name').fill('Settings E2E')

		const authMethod = page.locator('#general-auth-method')
		const nextAuthMethod = original.authMethod === 'FORMS' ? 'Disabled' : 'Forms login'
		await authMethod.click()
		await page.getByRole('option', { name: nextAuthMethod, exact: true }).click()

		const authRequired = page.locator('#general-auth-required')
		const nextAuthRequired = original.authenticationRequired === 'ENABLED'
			? 'Disabled for local addresses'
			: 'Enabled'
		await authRequired.click()
		await page.getByRole('option', { name: nextAuthRequired, exact: true }).click()

		await page.locator('#general-trusted-proxies').fill('10.0.0.0/8, 192.168.0.0/16')

		const proxySwitch = page.getByRole('switch', { name: 'Use an outbound proxy' })
		if (await proxySwitch.getAttribute('aria-checked') !== 'true') {
			await proxySwitch.click()
		}
		await page.locator('#general-proxy-type').click()
		await page.getByRole('option', { name: 'HTTP', exact: true }).click()
		await page.locator('#general-proxy-host').fill('proxy.e2e.invalid')
		await page.locator('#general-proxy-port').fill('18080')
		await page.locator('#general-proxy-bypass').fill('localhost, *.e2e.invalid')

		await page.locator('#general-backup-folder').fill('e2e-backups')
		await page.locator('#general-backup-interval').fill('17')
		await page.locator('#general-backup-retention').fill('9')
		await page.getByRole('button', { name: 'Save changes' }).click()

		await expect(page.locator('.s-toast-title', { hasText: 'Saved' })).toBeVisible()
		const savedResponse = await page.request.get('/api/v1/config/general')
		expect(savedResponse.ok(), await savedResponse.text()).toBe(true)
		const saved = await savedResponse.json() as GeneralConfig
		expect(saved.authMethod).toBe(original.authMethod === 'FORMS' ? 'NONE' : 'FORMS')
		expect(saved.authenticationRequired).toBe(original.authenticationRequired === 'ENABLED'
			? 'DISABLED_FOR_LOCAL_ADDRESSES'
			: 'ENABLED')
		expect(saved.trustedProxies).toBe('10.0.0.0/8, 192.168.0.0/16')
		expect(saved.proxyEnabled).toBe(true)
		expect(saved.proxyType).toBe('HTTP')
		expect(saved.proxyHost).toBe('proxy.e2e.invalid')
		expect(saved.proxyPort).toBe(18080)
		expect(saved.proxyBypassFilter).toBe('localhost, *.e2e.invalid')
		expect(saved.backupFolder).toBe('e2e-backups')
		expect(saved.backupIntervalDays).toBe(17)
		expect(saved.backupRetention).toBe(9)
		expect(saved.instanceName).toBe('Settings E2E')
		await expect(page).toHaveTitle('General - Settings E2E')
	}
	finally {
		const restored = await page.request.put('/api/v1/config/general', {
			data: editableGeneralConfig(original),
		})
		expect(restored.ok(), await restored.text()).toBe(true)
	}
})

test('system updates: displays current status and release history', async ({ page }) => {
	await signIn(page)
	await page.goto('/system/updates')

	await expect(page.getByRole('heading', { level: 1, name: 'Updates' })).toBeVisible()
	await expect(page.getByText('Current version')).toBeVisible()
	await expect(page.locator('.fact-row').filter({ hasText: 'Current version' }).locator('dd')).toHaveText(/\S+/)
	await expect(page.locator('.updates-status')).toBeVisible()
	await expect(page.locator('.updates-status')).toHaveText(/Unknown|Update available|No releases yet|Up to date/)

	const releaseHistory = page.locator('.s-section').filter({ hasText: 'Release history' })
	await expect(releaseHistory).toBeVisible()
	await expect(async () => {
		const hasRows = await releaseHistory.locator('.release-row').count() > 0
		const hasEmptyState = await releaseHistory.getByText('No releases have been published yet.').count() > 0
		expect(hasRows || hasEmptyState).toBe(true)
	}).toPass()
})
