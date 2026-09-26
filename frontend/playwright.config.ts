import { defineConfig, devices } from '@playwright/test'

export const viewports = [
	{ name: '854x480', width: 854, height: 480 },
	{ name: '1280x720', width: 1280, height: 720 },
	{ name: '1920x1080', width: 1920, height: 1080 },
	{ name: '2560x1440', width: 2560, height: 1440 },
	{ name: '3840x2160', width: 3840, height: 2160 },
	{ name: '1470x956', width: 1470, height: 956 },
	{ name: '1440x900', width: 1440, height: 900 },
]

export default defineConfig({
	testDir: './e2e',
	globalSetup: './e2e/global-setup.ts',
	timeout: 60_000,
	expect: { timeout: 10_000 },
	fullyParallel: false,
	workers: 1,
	reporter: [['list']],
	use: {
		baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8989',
		storageState: process.env.E2E_BASE_URL ? '.e2e/storage-state.json' : undefined,
		trace: 'retain-on-failure',
	},
	projects: viewports.map(viewport => ({
		name: viewport.name,
		use: {
			...devices['Desktop Chrome'],
			viewport: { width: viewport.width, height: viewport.height },
		},
	})),
})
