import { describe, expect, it } from 'vitest'
import { formatBytes, formatDate, formatDateTime, formatDuration, formatRelative } from '~/composables/useFormat'

describe('formatBytes', () => {
	it('renders zero and invalid sizes as 0 B', () => {
		expect(formatBytes(0)).toBe('0 B')
		expect(formatBytes(-5)).toBe('0 B')
		expect(formatBytes(Number.NaN)).toBe('0 B')
	})

	it('renders whole bytes without decimals', () => {
		expect(formatBytes(512)).toBe('512 B')
	})

	it('renders decimal units with one decimal place', () => {
		expect(formatBytes(846 * 1000)).toBe('846.0 KB')
		expect(formatBytes(1.4 * 1000 * 1000)).toBe('1.4 MB')
		expect(formatBytes(12.86 * 1000 * 1000 * 1000)).toBe('12.9 GB')
	})
})

describe('formatDate', () => {
	it('formats an ISO date as 26 Sep 2026', () => {
		expect(formatDate('2026-09-26T14:05:00Z')).toBe('26 Sep 2026')
	})

	it('accepts Date objects', () => {
		expect(formatDate(new Date('2026-01-03T00:00:00Z'))).toBe('3 Jan 2026')
	})

	it('returns an empty string for invalid input', () => {
		expect(formatDate('not a date')).toBe('')
	})
})

describe('formatDateTime', () => {
	it('formats the date and local time', () => {
		const date = new Date('2026-09-26T14:05:00Z')
		const hours = String(date.getHours()).padStart(2, '0')
		const minutes = String(date.getMinutes()).padStart(2, '0')
		expect(formatDateTime(date)).toBe(`26 Sep 2026, ${hours}:${minutes}`)
	})
})

describe('formatRelative', () => {
	const now = new Date('2026-09-26T12:00:00Z')

	it('returns just now inside a minute', () => {
		expect(formatRelative('2026-09-26T11:59:40Z', now)).toBe('just now')
	})

	it('returns past and future minute offsets', () => {
		expect(formatRelative('2026-09-26T11:58:00Z', now)).toBe('2 minutes ago')
		expect(formatRelative('2026-09-26T12:01:00Z', now)).toBe('in 1 minute')
	})

	it('returns day offsets', () => {
		expect(formatRelative('2026-09-24T12:00:00Z', now)).toBe('2 days ago')
		expect(formatRelative('2026-09-28T12:00:00Z', now)).toBe('in 2 days')
	})

	it('returns empty string for invalid input', () => {
		expect(formatRelative('garbage', now)).toBe('')
	})
})

describe('formatDuration', () => {
	it('renders minutes only below an hour', () => {
		expect(formatDuration(45)).toBe('45m')
	})

	it('renders hours and minutes', () => {
		expect(formatDuration(85)).toBe('1h 25m')
	})

	it('renders whole hours without trailing minutes', () => {
		expect(formatDuration(120)).toBe('2h')
	})

	it('renders zero minutes as 0m', () => {
		expect(formatDuration(0)).toBe('0m')
	})
})
