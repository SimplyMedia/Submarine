import { defineVitestConfig } from '@nuxt/test-utils/config'

export default defineVitestConfig({
	test: {
		environment: 'nuxt',
		include: ['tests/**/*.spec.ts'],
		environmentOptions: {
			nuxt: {
				domEnvironment: 'happy-dom',
			},
		},
	},
})
