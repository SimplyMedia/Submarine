import tailwindcss from '@tailwindcss/vite'

const apiTarget = process.env.API_URL ?? 'http://localhost:8989'

export default defineNuxtConfig({
	modules: [
		'@nuxt/eslint',
		'@nuxt/icon',
		'@nuxtjs/color-mode',
		'@pinia/nuxt',
		'@vueuse/nuxt',
	],
	ssr: false,
	components: [{ path: '~/components', pathPrefix: false }],
	devtools: { enabled: false },
	app: {
		head: {
			htmlAttrs: { lang: 'en' },
			title: 'Submarine',
			meta: [
				{ name: 'viewport', content: 'width=device-width, initial-scale=1' },
				{ name: 'description', content: 'The automated library for series and movies.' },
			],
			link: [{ rel: 'icon', href: '/favicon.ico' }],
		},
	},
	css: [
		'@fontsource-variable/ibm-plex-sans',
		'~/assets/css/tokens.css',
		'~/assets/css/main.css',
	],
	colorMode: {
		classSuffix: '',
		preference: 'system',
		fallback: 'light',
		storageKey: 'submarine-color-mode',
	},
	compatibilityDate: '2025-07-15',
	nitro: {
		prerender: {
			ignore: ['/dev/ui'],
		},
		devProxy: {
			// Nitro's dev proxy strips the mount path before forwarding, so the
			// mount segment must be re-added to the target or the backend sees
			// e.g. `/v1/setup/status` instead of `/api/v1/setup/status`.
			'/api': { target: `${apiTarget}/api`, changeOrigin: true },
			'/hubs': { target: `${apiTarget}/hubs`, ws: true, changeOrigin: true },
		},
	},
	vite: {
		plugins: [tailwindcss()],
	},
	typescript: { strict: true, typeCheck: false },
	eslint: {
		config: {
			stylistic: {
				indent: 'tab',
				quotes: 'single',
				semi: false,
			},
		},
	},
	icon: {
		mode: 'css',
		cssLayer: 'base',
		clientBundle: { scan: true, sizeLimitKb: 512 },
		serverBundle: { collections: ['lucide'] },
	},
})
