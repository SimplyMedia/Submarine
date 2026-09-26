// Starts the mock services and a published Submarine API for end-to-end runs.
//   node e2e/stack.mjs start   (SUBMARINE_API_DIR = folder with Submarine.Api.dll, default /tmp/submarine-api)
//   node e2e/stack.mjs stop
// The API listens on http://localhost:8989 with a fresh SQLite database under .e2e/.
import { spawn } from 'node:child_process'
import { mkdirSync, rmSync, writeFileSync, readFileSync, existsSync, openSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)))
const stateDir = path.join(root, '.e2e')
const pidFile = path.join(stateDir, 'pids.json')
const apiDir = process.env.SUBMARINE_API_DIR ?? '/tmp/submarine-api'
const apiPort = Number(process.env.E2E_API_PORT ?? 8989)

async function waitFor(url, attempts = 60) {
	for (let i = 0; i < attempts; i++) {
		try {
			const response = await fetch(url)
			if (response.ok) return
		}
		catch {
			// not up yet
		}
		await new Promise(resolve => setTimeout(resolve, 1000))
	}
	throw new Error(`Timed out waiting for ${url}`)
}

function detach(name, command, args, options) {
	const log = openSync(path.join(stateDir, `${name}.log`), 'a')
	const child = spawn(command, args, { ...options, detached: true, stdio: ['ignore', log, log] })
	child.unref()
	return child.pid
}

async function start() {
	rmSync(stateDir, { recursive: true, force: true })
	mkdirSync(path.join(stateDir, 'data'), { recursive: true })
	mkdirSync(path.join(stateDir, 'media', 'tv'), { recursive: true })
	mkdirSync(path.join(stateDir, 'media', 'movies'), { recursive: true })
	mkdirSync(path.join(stateDir, 'downloads', 'torrents'), { recursive: true })
	mkdirSync(path.join(stateDir, 'downloads', 'watch'), { recursive: true })

	const mocks = detach('mocks', 'node', [path.join(root, 'e2e', 'mock-services.mjs')], { cwd: root })
	let torznab = null
	if (existsSync(path.join(root, 'e2e', 'mock-torznab.mjs'))) {
		torznab = detach('torznab', 'node', [path.join(root, 'e2e', 'mock-torznab.mjs')], { cwd: root })
	}
	const api = detach('api', 'dotnet', ['Submarine.Api.dll'], {
		cwd: apiDir,
		env: {
			...process.env,
			ASPNETCORE_URLS: `http://localhost:${apiPort}`,
			ASPNETCORE_ENVIRONMENT: 'Production',
			ConnectionStrings__Sqlite: `Data Source=${path.join(stateDir, 'data', 'submarine.db')}`,
			Metadata__BaseUrl: 'http://localhost:5100',
			Mappings__BaseUrl: 'http://localhost:5200',
		},
	})
	writeFileSync(pidFile, JSON.stringify({ mocks, torznab, api }))
	await waitFor('http://localhost:5100/_status/healthz')
	await waitFor(`http://localhost:${apiPort}/_status/ready`)
	console.log(`stack ready: api http://localhost:${apiPort}, state ${stateDir}`)
}

function stop() {
	if (!existsSync(pidFile)) return
	const pids = JSON.parse(readFileSync(pidFile, 'utf8'))
	for (const pid of Object.values(pids)) {
		if (!pid) continue
		try {
			process.kill(pid)
		}
		catch {
			// already gone
		}
	}
	rmSync(pidFile, { force: true })
}

const command = process.argv[2]
if (command === 'start') await start()
else if (command === 'stop') stop()
else {
	console.error('usage: node e2e/stack.mjs start|stop')
	process.exit(1)
}
