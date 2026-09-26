/**
 * Sentence-case labels for the activity area (queue, history, blocklist).
 * `HistoryEventDto.type` reuses `HistoryEventType` from library-labels.ts;
 * import `historyEventTypeLabel` from there instead of duplicating it here.
 * The queue's client status and import pipeline state come back as plain
 * strings (Submarine.Core.Enums.TrackedDownloadStatus / TrackedDownloadState).
 */
import { toOptions } from '~/utils/library-labels'
import type { SBadgeTone } from '~/utils/system-labels'

const TRACKED_DOWNLOAD_STATUS: Record<string, string> = {
	QUEUED: 'Queued',
	DOWNLOADING: 'Downloading',
	PAUSED: 'Paused',
	COMPLETED: 'Completed',
	FAILED: 'Failed',
	WARNING: 'Warning',
}

const TRACKED_DOWNLOAD_STATUS_TONE: Record<string, SBadgeTone> = {
	QUEUED: 'neutral',
	DOWNLOADING: 'info',
	PAUSED: 'neutral',
	COMPLETED: 'ok',
	FAILED: 'danger',
	WARNING: 'warn',
}

const TRACKED_DOWNLOAD_STATE: Record<string, string> = {
	DOWNLOADING: 'Downloading',
	IMPORT_PENDING: 'Waiting to import',
	IMPORTING: 'Importing',
	IMPORTED: 'Imported',
	FAILED_PENDING: 'Waiting to retry',
	FAILED: 'Import failed',
	IGNORED: 'Ignored',
}

const TRACKED_DOWNLOAD_STATE_TONE: Record<string, SBadgeTone> = {
	DOWNLOADING: 'info',
	IMPORT_PENDING: 'neutral',
	IMPORTING: 'info',
	IMPORTED: 'ok',
	FAILED_PENDING: 'warn',
	FAILED: 'danger',
	IGNORED: 'neutral',
}

const PENDING_RELEASE_REASON: Record<string, string> = {
	DELAY: 'Delay profile',
	AVAILABILITY: 'Availability',
}

export function trackedDownloadStatusLabel(value: string): string {
	return TRACKED_DOWNLOAD_STATUS[value] ?? value
}

export function trackedDownloadStatusTone(value: string): SBadgeTone {
	return TRACKED_DOWNLOAD_STATUS_TONE[value] ?? 'neutral'
}

export function trackedDownloadStateLabel(value: string): string {
	return TRACKED_DOWNLOAD_STATE[value] ?? value
}

export function trackedDownloadStateTone(value: string): SBadgeTone {
	return TRACKED_DOWNLOAD_STATE_TONE[value] ?? 'neutral'
}

export function pendingReleaseReasonLabel(value: string): string {
	return PENDING_RELEASE_REASON[value] ?? value
}

export const historyEventTypeOptions = toOptions({
	GRABBED: 'Grabbed',
	IMPORTED: 'Imported',
	RENAMED: 'Renamed',
	DELETED: 'Deleted',
	FAILED: 'Failed',
	IGNORED: 'Ignored',
	UPGRADED: 'Upgraded',
})
