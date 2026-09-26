import { readdirSync, readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

const sourceRoot = resolve(process.cwd(), 'app')
const catalogPath = resolve(process.cwd(), 'i18n/locales/en.json')

type Catalog = { [key: string]: string | Catalog }

function sourceFiles(directory: string): string[] {
	return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
		const path = join(directory, entry.name)
		return entry.isDirectory() ? sourceFiles(path) : /\.(vue|ts)$/.test(entry.name) ? [path] : []
	})
}

function catalogKeys(catalog: Catalog, prefix = ''): string[] {
	return Object.entries(catalog).flatMap(([key, value]) => {
		const fullKey = prefix ? `${prefix}.${key}` : key
		return typeof value === 'string' ? [fullKey] : catalogKeys(value, fullKey)
	})
}

describe('English locale catalog', () => {
	it('contains every statically referenced key', () => {
		const catalog = JSON.parse(readFileSync(catalogPath, 'utf8')) as Catalog
		const codeKeys = new Set<string>()
		const keyPattern = /(?:\$t|\bt)\(\s*['"]([^'"]+)['"]/g
		const literalKeyPattern = /['"`]((?:utils|pages|components|nav|navigation|settings|common)\.[A-Za-z0-9_-]+\.[A-Za-z0-9_.-]+)['"`]/g

		for (const file of sourceFiles(sourceRoot)) {
			const source = readFileSync(file, 'utf8')
			for (const match of source.matchAll(keyPattern)) {
				codeKeys.add(match[1]!)
			}
			for (const match of source.matchAll(literalKeyPattern)) {
				const key = match[1]!
				if (!key.endsWith('.')) {
					codeKeys.add(key)
				}
			}
			for (const match of source.matchAll(/nameKey:\s*['"]([^'"]+)['"]/g)) {
				codeKeys.add(match[1]!)
			}
		}
		const settingsLabels = readFileSync(resolve(process.cwd(), 'app/utils/settings-labels.ts'), 'utf8')
		for (const match of settingsLabels.matchAll(/(?:toOptions|makeLookup)\((\w+),\s*['"]([^'"]+)['"]\)/g)) {
			const [, mapName, group] = match
			const map = settingsLabels.match(new RegExp(`const ${mapName}: Record<string, string> = \\{([^}]*)\\}`))
			for (const [, member] of map?.[1]?.matchAll(/([A-Z0-9_]+):/g) ?? []) {
				codeKeys.add(`utils.settingsLabels.${group}.${member.toLowerCase().replace(/_([a-z])/g, (_, letter: string) => letter.toUpperCase())}`)
			}
		}
		const languages = settingsLabels.match(/const LANGUAGE_NAMES = \[([\s\S]*?)\] as const/)?.[1] ?? ''
		for (const [, member] of languages.matchAll(/'([A-Z]+)'/g)) {
			codeKeys.add(`utils.settingsLabels.language.${member.toLowerCase()}`)
		}
		const generalSettings = readFileSync(resolve(process.cwd(), 'app/pages/settings/general.vue'), 'utf8')
		const logLevels = generalSettings.match(/const logLevelOptions = \[([^\]]+)\]/)?.[1] ?? ''
		for (const [, level] of logLevels.matchAll(/['"]([^'"]+)['"]/g)) {
			codeKeys.add(`pages.settings.general.logLevels.${level.toLowerCase()}`)
		}

		const keys = new Set(catalogKeys(catalog))
		expect([...codeKeys].filter(key => !keys.has(key)).sort(), 'keys used in app but missing from en.json').toEqual([])
		expect([...keys].filter(key => !codeKeys.has(key)).sort(), 'unused keys in en.json').toEqual([])
	})
})
