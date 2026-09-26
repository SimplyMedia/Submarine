import { describe, expect, it } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import STable from '~/components/ui/STable.vue'

describe('STable', () => {
	const columns = [
		{ key: 'title', label: 'Title' },
		{ key: 'size', label: 'Size', align: 'right' as const },
	]

	it('renders a header row and one row per entry', async () => {
		const wrapper = await mountSuspended(STable, {
			props: {
				columns,
				rows: [
					{ title: 'Harbour Lights', size: '1.2 GB' },
					{ title: 'The Trawler', size: '0.6 GB' },
				],
			},
		})
		const headers = wrapper.findAll('th')
		expect(headers.map(header => header.text().trim())).toEqual(['Title', 'Size'])
		expect(wrapper.findAll('tbody tr')).toHaveLength(2)
		expect(wrapper.text()).toContain('Harbour Lights')
	})

	it('labels cells for stacked mobile layout', async () => {
		const wrapper = await mountSuspended(STable, {
			props: { columns, rows: [{ title: 'Harbour Lights', size: '1.2 GB' }] },
		})
		const cells = wrapper.findAll('td')
		expect(cells[0]?.attributes('data-label')).toBe('Title')
		expect(cells[1]?.attributes('data-label')).toBe('Size')
	})

	it('renders the default empty state when there are no rows', async () => {
		const wrapper = await mountSuspended(STable, {
			props: { columns, rows: [] },
		})
		expect(wrapper.find('table').exists()).toBe(false)
		expect(wrapper.text()).toContain('Nothing here yet')
	})

	it('renders a custom empty slot instead of the default message', async () => {
		const wrapper = await mountSuspended(STable, {
			props: { columns, rows: [] },
			slots: { empty: () => 'Nothing on the blocklist' },
		})
		expect(wrapper.text()).toContain('Nothing on the blocklist')
	})

	it('supports cell overrides through named slots', async () => {
		const wrapper = await mountSuspended(STable, {
			props: { columns, rows: [{ title: 'Harbour Lights', size: '1.2 GB' }] },
			slots: {
				'cell-title': ({ row }) => `Series: ${row.title}`,
			},
		})
		expect(wrapper.text()).toContain('Series: Harbour Lights')
	})
})
