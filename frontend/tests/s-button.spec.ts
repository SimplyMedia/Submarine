import { describe, expect, it } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import SButton from '~/components/ui/SButton.vue'

describe('SButton', () => {
	it('renders a secondary button with its label by default', async () => {
		const wrapper = await mountSuspended(SButton, {
			slots: { default: () => 'Save changes' },
		})
		expect(wrapper.text()).toContain('Save changes')
		expect(wrapper.attributes('type')).toBe('button')
		expect(wrapper.attributes('disabled')).toBeUndefined()
	})

	it('disables and marks itself busy while loading', async () => {
		const wrapper = await mountSuspended(SButton, {
			props: { loading: true },
			slots: { default: () => 'Save changes' },
		})
		expect(wrapper.attributes('disabled')).toBeDefined()
		expect(wrapper.attributes('aria-busy')).toBe('true')
	})

	it('stays disabled when the disabled prop is set without loading', async () => {
		const wrapper = await mountSuspended(SButton, {
			props: { disabled: true },
			slots: { default: () => 'Archive' },
		})
		expect(wrapper.attributes('disabled')).toBeDefined()
		expect(wrapper.attributes('aria-busy')).toBeUndefined()
	})

	it('renders a submit button when type is submit', async () => {
		const wrapper = await mountSuspended(SButton, {
			attrs: { type: 'submit' },
			slots: { default: () => 'Sign in' },
		})
		expect(wrapper.attributes('type')).toBe('submit')
	})
})
