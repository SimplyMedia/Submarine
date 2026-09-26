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
	QUEUED: 'utils.activityLabels.trackedDownloadStatus.queued',
	DOWNLOADING: 'utils.activityLabels.trackedDownloadStatus.downloading',
	PAUSED: 'utils.activityLabels.trackedDownloadStatus.paused',
	COMPLETED: 'utils.activityLabels.trackedDownloadStatus.completed',
	FAILED: 'utils.activityLabels.trackedDownloadStatus.failed',
	WARNING: 'utils.activityLabels.trackedDownloadStatus.warning',
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
	DOWNLOADING: 'utils.activityLabels.trackedDownloadState.downloading',
	IMPORT_PENDING: 'utils.activityLabels.trackedDownloadState.importPending',
	IMPORTING: 'utils.activityLabels.trackedDownloadState.importing',
	IMPORTED: 'utils.activityLabels.trackedDownloadState.imported',
	FAILED_PENDING: 'utils.activityLabels.trackedDownloadState.failedPending',
	FAILED: 'utils.activityLabels.trackedDownloadState.failed',
	IGNORED: 'utils.activityLabels.trackedDownloadState.ignored',
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
	DELAY: 'utils.activityLabels.pendingReleaseReason.delay',
	AVAILABILITY: 'utils.activityLabels.pendingReleaseReason.availability',
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
	GRABBED: 'utils.libraryLabels.historyEventType.grabbed',
	IMPORTED: 'utils.libraryLabels.historyEventType.imported',
	RENAMED: 'utils.libraryLabels.historyEventType.renamed',
	DELETED: 'utils.libraryLabels.historyEventType.deleted',
	FAILED: 'utils.libraryLabels.historyEventType.failed',
	IGNORED: 'utils.libraryLabels.historyEventType.ignored',
	UPGRADED: 'utils.libraryLabels.historyEventType.upgraded',
})
