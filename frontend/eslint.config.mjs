// @ts-check
import withNuxt from './.nuxt/eslint.config.mjs'

export default withNuxt(
	{
		ignores: ['app/types/api.d.ts'],
	},
	{
		files: ['**/pages/**/*.vue'],
		rules: {
			'vue/multi-word-component-names': 'off',
		},
	},
	{
		files: ['e2e/**/*.ts'],
		rules: {
			'@stylistic/indent': ['error', 'tab'],
		},
	},
)
