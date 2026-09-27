import { rmSync } from 'node:fs'
import { expect, test } from '@playwright/test'
import { ensureRootFolder, signIn } from './setup'

test.skip(!process.env.E2E_BASE_URL, 'Set E2E_BASE_URL to a running Submarine API to run e2e')

const consumerName = 'E2E Kodi metadata consumer'
const ruleName = 'E2E Drama auto-tag rule'
const tagLabel = 'E2E Drama'
const taggedSeriesTvdbId = 81189

async function removeTestSeries(request: Parameters<typeof ensureRootFolder>[0]) {
	const response = await request.get('/api/v1/series', { params: { PageSize: 200 } })
	if (!response.ok()) return
	const items = (await response.json() as { items: { id: number, tvdbId: number }[] }).items
	for (const item of items.filter(series => series.tvdbId === taggedSeriesTvdbId)) {
		const folderResponse = await request.get(`/api/v1/series/${item.id}/folder`)
		const { folder } = await folderResponse.json() as { folder: string | null }
		const removed = await request.delete(`/api/v1/series/${item.id}`)
		expect(removed.ok(), await removed.text()).toBe(true)
		if (folder) rmSync(folder, { recursive: true, force: true })
	}
}

test.afterEach(async ({ page }) => {
	await removeTestSeries(page.request)
	const rulesResponse = await page.request.get('/api/v1/auto-tagging')
	if (rulesResponse.ok()) {
		for (const rule of (await rulesResponse.json() as { id: number, name: string }[]).filter(item => item.name === ruleName)) {
			await page.request.delete(`/api/v1/auto-tagging/${rule.id}`)
		}
	}
	const consumersResponse = await page.request.get('/api/v1/metadata-consumers')
	if (consumersResponse.ok()) {
		for (const consumer of (await consumersResponse.json() as { id: number, name: string }[]).filter(item => item.name === consumerName)) {
			await page.request.delete(`/api/v1/metadata-consumers/${consumer.id}`)
		}
	}
	const tagsResponse = await page.request.get('/api/v1/tags')
	if (tagsResponse.ok()) {
		for (const tag of (await tagsResponse.json() as { id: number, label: string }[]).filter(item => item.label === tagLabel)) {
			await page.request.delete(`/api/v1/tags/${tag.id}`)
		}
	}
})

test('metadata consumers: enable Kodi and persist it after reload', async ({ page }) => {
	const existing = await page.request.get('/api/v1/metadata-consumers')
	if (existing.ok()) {
		for (const consumer of (await existing.json() as { id: number, name: string }[]).filter(item => item.name === consumerName)) {
			await page.request.delete(`/api/v1/metadata-consumers/${consumer.id}`)
		}
	}

	await signIn(page, '/settings/metadata-consumers')
	await page.locator('header').getByRole('button', { name: 'Add consumer' }).click()
	const dialog = page.getByRole('dialog', { name: 'Add metadata consumer' })
	await dialog.getByLabel('Name').fill(consumerName)
	await dialog.getByRole('combobox').click()
	await page.getByRole('option', { name: 'Kodi', exact: true }).click()
	await dialog.getByRole('button', { name: 'Save changes' }).click()

	const consumer = page.locator('.consumer-row').filter({ hasText: consumerName })
	await expect(consumer).toContainText('Enabled')
	await page.reload()
	await expect(page.locator('.consumer-row').filter({ hasText: consumerName })).toContainText('Enabled')
})

test('auto tagging: apply a new rule to a newly added matching series', async ({ page }) => {
	const tagsResponse = await page.request.get('/api/v1/tags')
	const existingTag = (await tagsResponse.json() as { id: number, label: string }[]).find(tag => tag.label === tagLabel)
	if (existingTag) await page.request.delete(`/api/v1/tags/${existingTag.id}`)
	const createdTag = await page.request.post('/api/v1/tags', { data: { label: tagLabel } })
	expect(createdTag.ok(), await createdTag.text()).toBe(true)

	await signIn(page, '/settings/tags')
	await page.getByRole('button', { name: 'Add rule' }).click()
	const dialog = page.getByRole('dialog', { name: 'Add auto tagging rule' })
	await dialog.getByLabel('Name').fill(ruleName)
	await dialog.getByRole('button', { name: 'Add specification' }).click()
	const specification = dialog.locator('.spec-row').last()
	await specification.getByPlaceholder('Comma-separated values').fill('Drama')
	await dialog.getByRole('combobox').last().fill(tagLabel)
	await page.getByRole('option', { name: tagLabel, exact: true }).click()
	await dialog.getByRole('button', { name: 'Save changes' }).click()
	await expect(page.locator('.rule-row').filter({ hasText: ruleName })).toContainText(tagLabel)

	const rootFolderId = await ensureRootFolder(page.request)
	const qualityProfiles = (await (await page.request.get('/api/v1/quality-profiles')).json() as { items: { id: number }[] }).items
	const languageProfiles = (await (await page.request.get('/api/v1/language-profiles')).json() as { items: { id: number }[] }).items
	const added = await page.request.post('/api/v1/series', {
		data: {
			tvdbId: taggedSeriesTvdbId,
			metadataProvider: 'TVDB',
			rootFolderId,
			versions: [{ qualityProfileId: qualityProfiles[0]!.id, languageProfileId: languageProfiles[0]!.id }],
		},
	})
	expect(added.ok(), await added.text()).toBe(true)
	const detail = await added.json() as { series: { id: number } }
	const tag = await createdTag.json() as { id: number }
	await expect.poll(async () => {
		const response = await page.request.get(`/api/v1/series/${detail.series.id}`)
		const current = await response.json() as { series: { tagIds: number[] } }
		return current.series.tagIds
	}).toContain(tag.id)
})
