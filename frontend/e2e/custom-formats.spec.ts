import { readFile } from 'node:fs/promises'
import { expect, test } from '@playwright/test'
import { signIn } from './setup'

test.skip(!process.env.E2E_BASE_URL, 'Set E2E_BASE_URL to a running Submarine API to run e2e')

test('custom formats: create every spec type, export and re-import TRaSH JSON', async ({ page }) => {
	await signIn(page)
	const name = 'E2E release modifier format'
	const list = await page.request.get('/api/v1/custom-formats')
	if (list.ok()) {
		const formats = (await list.json() as { items: { id: number, name: string }[] }).items
		for (const format of formats.filter(item => item.name.startsWith(name))) await page.request.delete(`/api/v1/custom-formats/${format.id}`)
	}

	await page.goto('/settings/custom-formats')
	await page.getByRole('button', { name: 'Add custom format' }).first().click()
	const dialog = page.getByRole('dialog', { name: 'Add custom format' })
	await dialog.getByLabel('Name', { exact: true }).fill(name)
	const specs = [
		{ type: 'RELEASE_TITLE', label: 'Release title', value: 'E2E release title' },
		{ type: 'RELEASE_GROUP', label: 'Release group', value: 'E2E group' },
		{ type: 'LANGUAGE', label: 'Language' },
		{ type: 'QUALITY_SOURCE', label: 'Quality source' },
		{ type: 'RESOLUTION', label: 'Resolution' },
		{ type: 'STREAMING_PROVIDER', label: 'Streaming provider' },
		{ type: 'EDITION', label: 'Edition', value: 'Director' },
		{ type: 'RELEASE_FLAG', label: 'Release flag' },
		{ type: 'PROTOCOL', label: 'Protocol' },
		{ type: 'HARDCODED_SUBS', label: 'Hardcoded subs' },
		{ type: 'SIZE', label: 'Size', range: true },
		{ type: 'YEAR', label: 'Year', range: true },
		{ type: 'INDEXER_FLAG', label: 'Indexer flag' },
		{ type: 'RELEASE_TYPE', label: 'Release type', value: 'Episode' },
		{ type: 'QUALITY_MODIFIER', label: 'Quality modifier', value: 'BluRay remux' },
	]
	for (const [index, spec] of specs.entries()) {
		await dialog.getByRole('button', { name: 'Add specification' }).click()
		const row = dialog.locator('.spec-row').nth(index)
		await row.getByPlaceholder('Specification name').fill(spec.type)
		await row.locator('button.s-select-trigger').nth(0).scrollIntoViewIfNeeded()
		await row.locator('button.s-select-trigger').nth(0).click()
		await page.keyboard.type(spec.label, { delay: 50 })
		await page.keyboard.press('Enter')
		if (spec.range) {
			await row.getByPlaceholder('Min').fill(spec.type === 'SIZE' ? '1' : '2010')
			await row.getByPlaceholder('Max').fill(spec.type === 'SIZE' ? '20' : '2025')
		}
		else {
			const valueSelect = row.locator('button.s-select-trigger').nth(1)
			if (await valueSelect.count()) {
				await valueSelect.scrollIntoViewIfNeeded()
				await valueSelect.click()
				if (spec.value) {
					await page.keyboard.type(spec.value, { delay: 50 })
				}
				await page.keyboard.press('Enter')
			}
			else if (spec.type !== 'HARDCODED_SUBS') {
				await row.getByPlaceholder('Text or /regex/').fill(spec.value!)
			}
		}
	}

	await dialog.getByRole('button', { name: 'Save changes' }).click()
	const formatRow = page.getByRole('row').filter({ hasText: name })
	await expect(formatRow).toBeVisible()
	const apiFormats = (await (await page.request.get('/api/v1/custom-formats')).json() as { items: { id: number, name: string, specifications: { type: string, value: unknown }[] }[] }).items
	const created = apiFormats.find(format => format.name === name)
	expect(created?.specifications.map(spec => spec.type)).toEqual(specs.map(spec => spec.type))
	expect(created?.specifications.find(spec => spec.type === 'RELEASE_TYPE')?.value).toBe('EPISODE')
	expect(created?.specifications.find(spec => spec.type === 'QUALITY_MODIFIER')?.value).toBe('BLURAY_REMUX')

	await formatRow.getByRole('button', { name: 'Custom format actions' }).click()
	const downloadPromise = page.waitForEvent('download')
	await page.getByRole('menuitem', { name: 'Export' }).click()
	const download = await downloadPromise
	const exported = JSON.parse(await readFile((await download.path())!, 'utf8')) as { name: string, specifications: unknown[] }
	expect(exported.name).toBe(name)
	expect(exported.specifications).toHaveLength(specs.length)

	await page.getByRole('button', { name: 'Import', exact: true }).click()
	const importDialog = page.getByRole('dialog', { name: 'Import custom formats' })
	await importDialog.getByRole('textbox').fill(JSON.stringify({ ...exported, name: `${name} import` }))
	await importDialog.getByRole('button', { name: 'Import', exact: true }).click()
	await expect(page.locator('.s-toast-title', { hasText: 'Imported 1 custom format' })).toBeVisible()
	await expect(page.getByRole('row').filter({ hasText: `${name} import` })).toBeVisible()
})
