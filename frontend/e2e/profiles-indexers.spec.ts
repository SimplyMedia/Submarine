import { expect, test } from '@playwright/test'
import { ensureLibrary, ensureStubTrackerIndexer, signIn, STUB_TRACKER_NAME } from './setup'

test.skip(!process.env.E2E_BASE_URL, 'Set E2E_BASE_URL to a running Submarine API to run e2e')
const metadataPort = process.env.E2E_MOCK_PORT_BASE ? Number(process.env.E2E_MOCK_PORT_BASE) : 5100
let originalQualityDefinition: { id: number, minSizeMbPerMinute: number | null, maxSizeMbPerMinute: number | null, preferredSizeMbPerMinute: number | null } | undefined

test.afterEach(async ({ page }) => {
	for (const [endpoint, name] of [
		['/api/v1/language-profiles', 'E2E language profile'],
		['/api/v1/delay-profiles', 'E2E delay profile'],
		['/api/v1/release-profiles', 'E2E release profile'],
		['/api/v1/indexer-proxies', 'E2E FlareSolverr proxy'],
	] as const) {
		const response = await page.request.get(endpoint, { params: { PageSize: 200 } })
		if (!response.ok()) continue
		const body = await response.json() as { id: number, name: string }[] | { items: { id: number, name: string }[] }
		const profiles = Array.isArray(body) ? body : body.items
		for (const profile of profiles.filter(item => item.name === name)) {
			const deleted = await page.request.delete(`${endpoint}/${profile.id}`)
			expect(deleted.ok(), await deleted.text()).toBe(true)
		}
	}
	if (originalQualityDefinition) {
		const restored = await page.request.put('/api/v1/quality-definitions', {
			data: [originalQualityDefinition],
		})
		expect(restored.ok(), await restored.text()).toBe(true)
		originalQualityDefinition = undefined
	}
})


test('profiles: create language, delay with protocol switches, and release profiles', async ({ page }) => {
	await signIn(page)
	await page.goto('/settings/profiles')

	const languageName = 'E2E language profile'
	const delayName = 'E2E delay profile'
	const releaseName = 'E2E release profile'
	const clean = async (endpoint: string, name: string) => {
		const response = await page.request.get(endpoint, { params: { PageSize: 200 } })
		if (!response.ok()) return
		const body = await response.json() as { id: number, name: string }[] | { items: { id: number, name: string }[] }
		const rows = Array.isArray(body) ? body : body.items
		for (const row of rows.filter(item => item.name === name)) await page.request.delete(`${endpoint}/${row.id}`)
	}
	await clean('/api/v1/language-profiles', languageName)
	await clean('/api/v1/delay-profiles', delayName)
	await clean('/api/v1/release-profiles', releaseName)

	await page.getByRole('tab', { name: 'Language' }).click()
	await page.getByRole('button', { name: 'Add language profile' }).first().click()
	let dialog = page.getByRole('dialog', { name: 'Add language profile' })
	await dialog.getByLabel('Name').fill(languageName)
	await dialog.getByRole('combobox').first().click()
	await page.getByRole('option', { name: 'French', exact: true }).click()
	await dialog.getByRole('button', { name: 'Add', exact: true }).click()
	await dialog.getByRole('button', { name: 'Save changes' }).click()
	await expect(page.getByRole('row').filter({ hasText: languageName })).toContainText('French')

	await page.getByRole('tab', { name: 'Delay' }).click()
	await page.getByRole('button', { name: 'Add delay profile' }).first().click()
	dialog = page.getByRole('dialog', { name: 'Add delay profile' })
	await dialog.getByLabel('Name').fill(delayName)
	const preferredProtocol = dialog.locator('#delay-protocol')
	await preferredProtocol.click()
	await page.getByRole('option', { name: 'Usenet', exact: true }).click()
	await dialog.getByLabel('Usenet delay (minutes)').fill('12')
	await dialog.getByLabel('Torrent delay (minutes)').fill('34')
	await dialog.getByRole('button', { name: 'Save changes' }).click()
	await expect(page.getByRole('listitem').filter({ hasText: delayName })).toContainText('Usenet')

	await page.getByRole('tab', { name: 'Release' }).click()
	await page.getByRole('button', { name: 'Add release profile' }).first().click()
	dialog = page.getByRole('dialog', { name: 'Add release profile' })
	await dialog.getByLabel('Name').fill(releaseName)
	await dialog.getByPlaceholder('Type and press enter').nth(0).fill('E2E-REQUIRED')
	await dialog.getByPlaceholder('Type and press enter').nth(0).press('Enter')
	await dialog.getByPlaceholder('Type and press enter').nth(1).fill('E2E-IGNORED')
	await dialog.getByPlaceholder('Type and press enter').nth(1).press('Enter')
	await dialog.getByRole('button', { name: 'Save changes' }).click()
	const profile = page.getByRole('row').filter({ hasText: releaseName })
	await expect(profile).toBeVisible()
	const releaseProfiles = (await (await page.request.get('/api/v1/release-profiles', { params: { PageSize: 200 } })).json() as { items: { name: string, required: string[], ignored: string[] }[] }).items
	const savedReleaseProfile = releaseProfiles.find(item => item.name === releaseName)
	expect(savedReleaseProfile?.required).toContain('E2E-REQUIRED')
	expect(savedReleaseProfile?.ignored).toContain('E2E-IGNORED')
})

test('quality definitions: edit a size and import TRaSH quality sizes', async ({ page }) => {
	await signIn(page)
	const definitions = await page.request.get('/api/v1/quality-definitions')
	expect(definitions.ok(), await definitions.text()).toBe(true)
	const rows = await definitions.json() as { id: number, title: string, minSizeMbPerMinute: number | null, maxSizeMbPerMinute: number | null, preferredSizeMbPerMinute: number | null }[]
	const web1080Definition = rows.find(item => item.title === 'WebDL-1080p')
	expect(web1080Definition).toBeDefined()
	originalQualityDefinition = {
		id: web1080Definition!.id,
		minSizeMbPerMinute: web1080Definition!.minSizeMbPerMinute,
		maxSizeMbPerMinute: web1080Definition!.maxSizeMbPerMinute,
		preferredSizeMbPerMinute: web1080Definition!.preferredSizeMbPerMinute,
	}
	await page.goto('/settings/quality')
	const web1080 = page.getByRole('row').filter({ hasText: 'WebDL-1080p' }).first()
	await expect(web1080).toBeVisible()
	const minInput = web1080.locator('input').first()
	const original = await minInput.inputValue()
	const changed = original === '2' ? '3' : '2'
	await minInput.fill(changed)
	await minInput.press('Tab')
	await page.getByRole('button', { name: 'Save changes' }).click()
	await expect(minInput).toHaveValue(changed)

	await page.getByRole('button', { name: 'Import', exact: true }).click()
	const dialog = page.getByRole('dialog', { name: 'Import quality sizes' })
	await dialog.getByRole('textbox').fill(JSON.stringify({ qualities: [{ quality: 'WEBDL-1080p', min: 7, preferred: 21, max: 63 }] }))
	await dialog.getByRole('button', { name: 'Import', exact: true }).click()
	await expect(page.locator('.s-toast-title', { hasText: 'Updated 1 quality definition' })).toBeVisible()
	await expect(web1080.locator('input').first()).toHaveValue('7')
	await expect(web1080.locator('input').nth(1)).toHaveValue('21')
	await expect(web1080.locator('input').nth(2)).toHaveValue('63')
})

test('indexer proxies and stats: add and test a proxy, then inspect search statistics', async ({ page }) => {
	await signIn(page)
	const name = 'E2E FlareSolverr proxy'
	const proxies = await page.request.get('/api/v1/indexer-proxies', { params: { PageSize: 200 } })
	if (proxies.ok()) {
		const rows = await proxies.json() as { items: { id: number, name: string }[] }
		for (const item of rows.items.filter(proxy => proxy.name === name)) await page.request.delete(`/api/v1/indexer-proxies/${item.id}`)
	}
	await page.goto('/indexers/proxies')
	await page.getByRole('button', { name: 'Add proxy' }).first().click()
	const dialog = page.getByRole('dialog', { name: 'Add proxy' })
	await dialog.getByLabel('Name').fill(name)
	await dialog.locator('button.s-select-trigger').first().click()
	await page.getByRole('option', { name: 'FlareSolverr', exact: true }).click()
	await dialog.getByLabel('FlareSolverr host').fill('127.0.0.1')
	await dialog.getByLabel('Port').fill(String(metadataPort))
	await dialog.getByRole('button', { name: 'Add proxy' }).click()
	const proxyRow = page.getByRole('row').filter({ hasText: name })
	await expect(proxyRow).toBeVisible()
	await proxyRow.getByRole('button', { name: 'Proxy actions' }).click()
	await page.getByRole('menuitem', { name: 'Test' }).click()
	await expect(page.locator('.s-toast-title', { hasText: 'Connection successful' })).toBeVisible()

	await ensureLibrary(page.request)
	await ensureStubTrackerIndexer(page.request)
	await page.goto('/indexers/search')
	await page.getByPlaceholder('Release title, e.g. Harbour Lights S02E06').fill('Harbour Lights')
	await page.getByRole('button', { name: 'Search', exact: true }).click()
	await expect(page.getByRole('row').filter({ hasText: 'Harbour.Lights' }).first()).toBeVisible()
	await page.goto('/indexers/stats')
	await expect(page.getByRole('heading', { level: 1, name: 'Stats' })).toBeVisible()
	await expect(page.getByRole('row').filter({ hasText: STUB_TRACKER_NAME })).toBeVisible()
})
