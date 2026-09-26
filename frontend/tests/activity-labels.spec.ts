import { describe, expect, it } from 'vitest'
import {
	historyEventTypeOptions,
	trackedDownloadStateLabel,
	trackedDownloadStateTone,
	trackedDownloadStatusLabel,
	trackedDownloadStatusTone,
} from '~/utils/activity-labels'
import { historyEventTypeLabel } from '~/utils/library-labels'

describe('activity-labels', () => {
	it('trackedDownloadStatus labels and tones cover every TrackedDownloadStatus member', () => {
		for (const status of ['QUEUED', 'DOWNLOADING', 'PAUSED', 'COMPLETED', 'FAILED', 'WARNING']) {
			expect(trackedDownloadStatusLabel(status)).not.toBe(status)
			expect(['ok', 'warn', 'danger', 'info', 'neutral']).toContain(trackedDownloadStatusTone(status))
		}
		expect(trackedDownloadStatusTone('FAILED')).toBe('danger')
		expect(trackedDownloadStatusTone('COMPLETED')).toBe('ok')
	})

	it('trackedDownloadState labels and tones cover every TrackedDownloadState member', () => {
		for (const state of ['DOWNLOADING', 'IMPORT_PENDING', 'IMPORTING', 'IMPORTED', 'FAILED_PENDING', 'FAILED', 'IGNORED']) {
			expect(trackedDownloadStateLabel(state)).not.toBe(state)
		}
		expect(trackedDownloadStateTone('IMPORTED')).toBe('ok')
		expect(trackedDownloadStateTone('FAILED')).toBe('danger')
	})

	it('falls back to the raw value for an unknown status instead of throwing', () => {
		expect(trackedDownloadStatusLabel('SOMETHING_NEW')).toBe('SOMETHING_NEW')
		expect(trackedDownloadStatusTone('SOMETHING_NEW')).toBe('neutral')
	})

	it('historyEventTypeOptions values all resolve through the shared library-labels lookup', () => {
		for (const option of historyEventTypeOptions) {
			expect(historyEventTypeLabel(option.value as never)).toBe(option.label)
		}
	})
})
