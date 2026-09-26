import { spawn } from 'node:child_process'
import { mkdir, rm } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { chromium } from '@playwright/test'

const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)))
const outDir = path.join(root, '.screenshots')
const port = process.env.SCREENSHOT_PORT ? Number(process.env.SCREENSHOT_PORT) : 3100
const base = `http://localhost:${port}`

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

// Kernel endpoint mocks. The hub negotiate answers 404 so the dot shows the
// honest offline state without any browser console noise.
function apiHandler(profile) {
	return (route) => {
		const url = new URL(route.request().url())
		const target = url.pathname
		const json = (body, status = 200) => route.fulfill({
			status,
			contentType: 'application/json',
			body: JSON.stringify(body),
		})

		if (target.startsWith('/hubs/')) {
			return route.fulfill({ status: 404, contentType: 'text/plain', body: '' })
		}
		if (target === '/api/v1/setup/status') {
			return json({ needsSetup: profile.needsSetup })
		}
		if (target === '/api/v1/auth/me') {
			return profile.user ? json({ id: 1, username: 'admin' }) : json({ title: 'Current user not found' }, 401)
		}
		if (target === '/api/v1/system/status') {
			return json({ appName: 'Submarine', version: '0.1.0-dev', authRequired: true, needsSetup: false, databaseType: 'Sqlite' })
		}
		if (target === '/api/v1/auth/login' || target === '/api/v1/setup') {
			return json({ id: 1, username: 'admin' }, target === '/api/v1/setup' ? 201 : 200)
		}
		if (target === '/api/v1/auth/logout') {
			return route.fulfill({ status: 204, body: '' })
		}
		return json({}, 404)
	}
}

const targets = [
	{ name: 'login', path: '/login', profile: { needsSetup: false, user: false } },
	{ name: 'setup', path: '/setup', profile: { needsSetup: true, user: false } },
	{ name: 'series', path: '/series', profile: { needsSetup: false, user: true } },
	{ name: 'dev-ui', path: '/dev/ui', profile: { needsSetup: false, user: true } },
]

function startDevServer() {
	const child = spawn('pnpm', ['dev', '--port', String(port)], {
		cwd: root,
		stdio: ['ignore', 'pipe', 'pipe'],
	})
	let output = ''
	child.stdout.on('data', (chunk) => {
		output += chunk.toString()
	})
	child.stderr.on('data', (chunk) => {
		output += chunk.toString()
	})
	child.ready = new Promise((resolve, reject) => {
		const started = Date.now()
		const poll = async () => {
			if (child.exitCode !== null) {
				reject(new Error(`dev server exited with ${child.exitCode}\n${output}`))
				return
			}
			try {
				const response = await fetch(base)
				if (response.ok) {
					resolve()
					return
				}
			}
			catch {
				// not up yet
			}
			if (Date.now() - started > 180_000) {
				reject(new Error(`dev server did not start in time\n${output}`))
				return
			}
			setTimeout(poll, 500)
		}
		void poll()
	})
	return child
}

const server = startDevServer()
let failed = false

try {
	await server.ready
	await rm(outDir, { recursive: true, force: true })
	await mkdir(outDir, { recursive: true })

	const browser = await chromium.launch()
	const consoleLog = []
	const problems = []

	for (const theme of themes) {
		const context = await browser.newContext({ colorScheme: theme })
		await context.addInitScript(() => {
			globalThis.__SUBMARINE_MOCK__ = true
		})
		for (const viewport of viewports) {
			for (const target of targets) {
				const page = await context.newPage()
				await page.setViewportSize({ width: viewport.width, height: viewport.height })
				page.on('console', (message) => {
					if (message.type() === 'error' || message.type() === 'warning') {
						consoleLog.push(`${target.name} ${viewport.name} ${theme}: ${message.type()}: ${message.text()}`)
					}
				})
				page.on('pageerror', (error) => {
					consoleLog.push(`${target.name} ${viewport.name} ${theme}: pageerror: ${error.message}`)
				})
				await page.route('**/api/**', apiHandler(target.profile))
				await page.route('**/hubs/**', apiHandler(target.profile))

				await page.goto(`${base}${target.path}`, { waitUntil: 'networkidle' })
				await page.evaluate(() => document.fonts.ready)
				await page.waitForTimeout(400)

				const overflow = await page.evaluate(
					() => document.documentElement.scrollWidth - document.documentElement.clientWidth,
				)
				if (overflow > 0) {
					problems.push(`${target.name} ${viewport.name} ${theme}: horizontal overflow of ${overflow}px`)
				}

				const file = path.join(outDir, `${target.name}-${viewport.name}-${theme}.png`)
				await page.screenshot({ path: file })
				await page.close()
			}
		}
		await context.close()
	}

	await browser.close()

	console.log(`Saved ${viewports.length * themes.length * targets.length} screenshots to ${outDir}`)
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
finally {
	server.kill('SIGTERM')
}

process.exit(failed ? 1 : 0)
