import { describe, expect, it } from 'vitest'
import {
	authMethodLabel,
	authMethodOptions,
	downloadClientTypeIcon,
	downloadClientTypeLabel,
	humanizeEnumValue,
	humanizeFieldName,
	importListTypeIcon,
	notificationTypeIcon,
	qualityResolutionLabel,
} from '~/utils/settings-labels'

describe('settings-labels', () => {
	it('humanizeFieldName turns camelCase property names into sentence case, preserving known acronyms', () => {
		expect(humanizeFieldName('host')).toBe('Host')
		expect(humanizeFieldName('useSsl')).toBe('Use SSL')
		expect(humanizeFieldName('apiKey')).toBe('API key')
		expect(humanizeFieldName('urlBase')).toBe('URL base')
	})

	it('humanizeEnumValue turns SCREAMING_SNAKE members into sentence case', () => {
		expect(humanizeEnumValue('WEB_DL')).toBe('Web dl')
		expect(humanizeEnumValue('CUSTOM_SCRIPT')).toBe('Custom script')
		expect(humanizeEnumValue('')).toBe('')
	})

	it('qualityResolutionLabel converts R###_P members to a plain resolution string', () => {
		expect(qualityResolutionLabel('R1080_P')).toBe('1080p')
		expect(qualityResolutionLabel('R2160_P')).toBe('2160p')
		expect(qualityResolutionLabel(null)).toBe('Unknown')
	})

	it('every *Options list has a *Label lookup that resolves each of its own values', () => {
		for (const option of authMethodOptions) {
			expect(authMethodLabel(option.value)).toBe(option.label)
		}
	})

	it('*Label lookups fall back to a humanized value for unknown members instead of throwing', () => {
		expect(downloadClientTypeLabel('SOMETHING_NEW' as never)).toBe('Something new')
	})

	it('provider icon lookups return a sensible default for unknown types', () => {
		expect(notificationTypeIcon('DISCORD')).not.toBe('lucide:bell')
		expect(notificationTypeIcon('NOT_A_TYPE')).toBe('lucide:bell')
		expect(downloadClientTypeIcon('NOT_A_TYPE')).toBe('lucide:download')
		expect(importListTypeIcon('NOT_A_TYPE')).toBe('lucide:list')
	})
})
