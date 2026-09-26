import { describe, expect, it } from 'vitest'
import {
	backupKindLabel,
	commandLabel,
	commandStatusLabel,
	commandStatusTone,
} from '~/utils/system-labels'

describe('commandLabel', () => {
	it('falls back to splitting PascalCase for unknown names', () => {
		expect(commandLabel('SomeFutureCommand')).toBe('Some future command')
	})
})

describe('commandStatusLabel and commandStatusTone', () => {
	it('falls back to the raw value and a neutral tone for unknown statuses', () => {
		expect(commandStatusLabel('WEIRD')).toBe('WEIRD')
		expect(commandStatusTone('WEIRD')).toBe('neutral')
	})
})

describe('backupKindLabel', () => {
	it('reads the kind embedded in the backup file name', () => {
		expect(backupKindLabel('submarine_backup_v2_20260101000000_MANUAL.zip')).toBe('Manual')
		expect(backupKindLabel('submarine_backup_v2_20260101000000_SCHEDULED.zip')).toBe('Scheduled')
		expect(backupKindLabel('not-a-backup.zip')).toBe('Unknown')
	})
})
