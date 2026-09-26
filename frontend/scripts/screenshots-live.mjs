// Visual verification against a real running stack (real API + real dev
// server), unlike scripts/screenshots.mjs which mocks the kernel endpoints.
// Usage:
//   node scripts/screenshots-live.mjs --base http://localhost:3011 --area library \
//     --pages /,/series,/series/1,/movies,/calendar,/wanted
//   [--username admin] [--password submarine-admin-1]
import { mkdir } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { chromium } from '@playwright/test'

const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)))

function arg(name, fallback) {
	const index = process.argv.indexOf(`--${name}`)
	return index === -1 ? fallback : process.argv[index + 1]
}

const base = arg('base')
const area = arg('area')
const pagesArg = arg('pages')
const username = arg('username', 'admin')
const password = arg('password', 'submarine-admin-1')

if (!base || !area || !pagesArg) {
	console.error('Usage: node scripts/screenshots-live.mjs --base <url> --area <name> --pages <comma list> [--username x] [--password y]')
	process.exit(1)
}

const pages = pagesArg.split(',').map(entry => entry.trim()).filter(Boolean)
const outDir = path.join(root, '.screenshots', area)

const viewports = [
	{ name: '854x480', width: 854, height: 480 },
	{ name: '1280x720', width: 1280, height: 720 },
	{ name: '1920x1080', width: 1920, height: 1080 },
	{ name: '2560x1440', width: 2560, height: 1440 },
	{ name: '3840x2160', width: 3840, height: 2160 },
	{ name: '1470x956', width: 1470, height: 956 },
	{ name: '1440x900', width: 1440, height: 900 },
]

const themes = ['light', 'dark']

function slug(pagePath) {
	if (pagePath === '/') {
		return 'dashboard'
	}
	return pagePath.replaceAll('/', '-').replace(/^-/, '').replace(/-$/, '') || 'root'
}

async function signIn(page) {
	await page.goto(base)
	// The auth middleware runs an async GET /auth/me before deciding to
	// redirect to /login or /setup; goto's 'load' event can fire before that
	// redirect lands, so wait for the URL to settle instead of reading it
	// immediately (a fresh context always starts unauthenticated, so this
	// always resolves unless the redirect is genuinely slow).
	await page.waitForURL(url => url.pathname.includes('/login') || url.pathname.includes('/setup'), { timeout: 10_000 }).catch(() => {})
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
	await page.waitForURL(url => !url.pathname.includes('/login') && !url.pathname.includes('/setup'), { timeout: 15_000 })
}

let failed = false

try {
	await mkdir(outDir, { recursive: true })

	const browser = await chromium.launch()
	const consoleLog = []
	const problems = []

	for (const theme of themes) {
		const context = await browser.newContext({ colorScheme: theme })
		const authPage = await context.newPage()
		await signIn(authPage)
		await authPage.close()

		for (const viewport of viewports) {
			for (const pagePath of pages) {
				const target = await context.newPage()
				await target.setViewportSize({ width: viewport.width, height: viewport.height })
				target.on('console', (message) => {
					const text = message.text()
					if (message.type() === 'error' && text.includes('/auth/me') && text.includes('401')) {
						return
					}
					if (message.type() === 'error' || message.type() === 'warning') {
						consoleLog.push(`${pagePath} ${viewport.name} ${theme}: ${message.type()}: ${text}`)
					}
				})
				target.on('pageerror', (error) => {
					consoleLog.push(`${pagePath} ${viewport.name} ${theme}: pageerror: ${error.message}`)
				})

				// 'networkidle' never resolves here: the SignalR hub connection
				// stays open (and auto-retries if it can't connect), so we settle
				// on 'load' plus an explicit wait for data fetches to render.
				await target.goto(`${base}${pagePath}`, { waitUntil: 'load' })
				await target.evaluate(() => document.fonts.ready)
				await target.waitForTimeout(800)

				const overflow = await target.evaluate(
					() => document.documentElement.scrollWidth - window.innerWidth,
				)
				if (overflow > 0) {
					problems.push(`${pagePath} ${viewport.name} ${theme}: horizontal overflow of ${overflow}px`)
				}

				const file = path.join(outDir, `${area}-${slug(pagePath)}-${viewport.name}-${theme}.png`)
				await target.screenshot({ path: file })
				await target.close()
			}
		}
		await context.close()
	}

	await browser.close()

	console.log(`Saved ${viewports.length * themes.length * pages.length} screenshots to ${outDir}`)
	if (consoleLog.length > 0) {
		console.log('Console issues:')
		for (const line of consoleLog) {
			console.log(`  ${line}`)
		}
	}
	else {
		console.log('Console issues: none')
	}
	if (problems.length > 0) {
		console.log('Layout problems:')
		for (const line of problems) {
			console.log(`  ${line}`)
		}
		failed = true
	}
}
catch (error) {
	console.error(error)
	failed = true
}

process.exit(failed ? 1 : 0)
