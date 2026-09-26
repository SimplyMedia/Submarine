import { describe, expect, it } from 'vitest'
import { i18n } from '~/i18n'
import {
	cardigannFieldToSchemaField,
	indexerHistoryEventTone,
	indexerHistoryEventTypeLabel,
	indexerImplementationIcon,
	indexerImplementationLabel,
	indexerImplementationOptions,
	indexerProxyTypeLabel,
	indexerProxyTypeOptions,
	settingsFieldToSchemaField,
} from '~/utils/indexer-labels'

describe('indexer-labels', () => {
	it('every implementation/proxy option has a label lookup that resolves each of its own values', () => {
		for (const option of indexerImplementationOptions) {
			expect(indexerImplementationLabel(option.value)).toBe(option.label)
			expect(indexerImplementationIcon(option.value)).toMatch(/^lucide:/)
		}
		for (const option of indexerProxyTypeOptions) {
			expect(indexerProxyTypeLabel(option.value)).toBe(option.label)
		}
	})

	it('indexerHistoryEventTone marks failures danger and grabs ok', () => {
		expect(indexerHistoryEventTone('FAILED')).toBe('danger')
		expect(indexerHistoryEventTone('GRAB')).toBe('ok')
		expect(i18n.global.t(indexerHistoryEventTypeLabel('AUTH'))).toBe('Sign in')
	})

	it('settingsFieldToSchemaField infers control type from clrType and treats api-key-like names as password', () => {
		expect(settingsFieldToSchemaField({ name: 'apiKey', clrType: 'string', required: false, enumValues: null, default: null }).type).toBe('password')
		expect(settingsFieldToSchemaField({ name: 'apiPath', clrType: 'string', required: true, enumValues: null, default: '/api' }).type).toBe('text')
		expect(settingsFieldToSchemaField({ name: 'minimumSeeders', clrType: 'int', required: false, enumValues: null, default: 1 }).type).toBe('number')
		expect(settingsFieldToSchemaField({ name: 'enabled', clrType: 'bool', required: false, enumValues: null, default: false }).type).toBe('checkbox')
	})

	it('cardigannFieldToSchemaField maps a select field and exposes its value-to-label option map', () => {
		const { field, optionLabels } = cardigannFieldToSchemaField({
			name: 'sort',
			type: 'SELECT',
			label: 'Sort by',
			default: 'created',
			options: { created: 'Created date', seeders: 'Seeders' },
		})
		expect(field.type).toBe('select')
		expect(field.options).toEqual(['created', 'seeders'])
		expect(optionLabels).toEqual({ created: 'Created date', seeders: 'Seeders' })
	})

	it('cardigannFieldToSchemaField turns a captcha field into an info field with a hint', () => {
		const { field } = cardigannFieldToSchemaField({ name: 'captcha', type: 'CARDIGANNCAPTCHA', label: null, default: null, options: null })
		expect(field.type).toBe('info')
		expect(field.helpText).toMatch(/captcha/i)
	})
})
