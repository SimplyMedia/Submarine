/**
 * Sentence-case labels for library enums. The API returns UPPER_SNAKE
 * member names; these maps are the only place that turns them into copy.
 */
import type { components } from '~/types/api'

type SeriesStatus = components['schemas']['SeriesStatus']
type SeriesType = components['schemas']['SeriesType']
type SeriesNumbering = components['schemas']['SeriesNumbering']
type MonitorNewItems = components['schemas']['MonitorNewItems']
type MovieStatus = components['schemas']['MovieStatus']
type MinimumAvailability = components['schemas']['MinimumAvailability']
type AddMonitorOption = NonNullable<components['schemas']['AddMonitorOption']>
type MetadataProvider = components['schemas']['MetadataProvider']
type HistoryEventType = NonNullable<components['schemas']['HistoryEventType']>
type QualityModel = components['schemas']['QualityModel']

const seriesStatusLabels: Record<SeriesStatus, string> = {
	CONTINUING: 'Continuing',
	ENDED: 'Ended',
	UPCOMING: 'Upcoming',
	UNKNOWN: 'Unknown',
}

const seriesTypeLabels: Record<SeriesType, string> = {
	STANDARD: 'Standard',
	DAILY: 'Daily',
	ANIME: 'Anime',
}

const seriesNumberingLabels: Record<SeriesNumbering, string> = {
	AIRED: 'Aired order',
	DVD: 'DVD order',
	ABSOLUTE: 'Absolute order',
}

const monitorNewItemsLabels: Record<MonitorNewItems, string> = {
	ALL: 'All new seasons',
	NONE: 'No new seasons',
}

const movieStatusLabels: Record<MovieStatus, string> = {
	ANNOUNCED: 'Announced',
	IN_CINEMAS: 'In cinemas',
	RELEASED: 'Released',
}

const minimumAvailabilityLabels: Record<MinimumAvailability, string> = {
	ANNOUNCED: 'Announced',
	IN_CINEMAS: 'In cinemas',
	RELEASED: 'Released',
}

const addMonitorOptionLabels: Record<AddMonitorOption, string> = {
	ALL: 'All episodes',
	FUTURE: 'Future episodes',
	MISSING: 'Missing episodes',
	EXISTING: 'Existing episodes',
	PILOT: 'Pilot only',
	FIRST_SEASON: 'First season',
	LATEST_SEASON: 'Latest season',
	NONE: 'None',
}

const metadataProviderLabels: Record<MetadataProvider, string> = {
	TVDB: 'TheTVDB',
	TMDB: 'TMDB',
}

const historyEventTypeLabels: Record<HistoryEventType, string> = {
	GRABBED: 'Grabbed',
	IMPORTED: 'Imported',
	RENAMED: 'Renamed',
	DELETED: 'Deleted',
	FAILED: 'Failed',
	IGNORED: 'Ignored',
	UPGRADED: 'Upgraded',
}

export function seriesStatusLabel(value: SeriesStatus): string {
	return seriesStatusLabels[value] ?? value
}

export function seriesTypeLabel(value: SeriesType): string {
	return seriesTypeLabels[value] ?? value
}

export function seriesNumberingLabel(value: SeriesNumbering): string {
	return seriesNumberingLabels[value] ?? value
}

export function monitorNewItemsLabel(value: MonitorNewItems): string {
	return monitorNewItemsLabels[value] ?? value
}

export function movieStatusLabel(value: MovieStatus): string {
	return movieStatusLabels[value] ?? value
}

export function minimumAvailabilityLabel(value: MinimumAvailability): string {
	return minimumAvailabilityLabels[value] ?? value
}

export function addMonitorOptionLabel(value: AddMonitorOption): string {
	return addMonitorOptionLabels[value] ?? value
}

export function metadataProviderLabel(value: MetadataProvider): string {
	return metadataProviderLabels[value] ?? value
}

export function historyEventTypeLabel(value: HistoryEventType): string {
	return historyEventTypeLabels[value] ?? value
}

/** Turns enum member names into option lists for SSelect. */
export function toOptions<T extends string>(labels: Record<T, string>): Array<{ value: T, label: string }> {
	return (Object.keys(labels) as T[]).map(value => ({ value, label: labels[value] }))
}

export const seriesStatusOptions = toOptions(seriesStatusLabels)
export const seriesTypeOptions = toOptions(seriesTypeLabels)
export const seriesNumberingOptions = toOptions(seriesNumberingLabels)
export const monitorNewItemsOptions = toOptions(monitorNewItemsLabels)
export const movieStatusOptions = toOptions(movieStatusLabels)
export const minimumAvailabilityOptions = toOptions(minimumAvailabilityLabels)
export const metadataProviderOptions = toOptions(metadataProviderLabels)

export const addMonitorOptionOptions: Array<{ value: AddMonitorOption, label: string }> = (
	['ALL', 'FUTURE', 'MISSING', 'EXISTING', 'PILOT', 'FIRST_SEASON', 'LATEST_SEASON', 'NONE'] as const
).map(value => ({ value, label: addMonitorOptionLabels[value] }))

/** Display name for a file's quality, e.g. "WebDL-1080p Proper". */
export function qualityLabel(quality: QualityModel | null | undefined): string {
	if (!quality) {
		return 'Unknown'
	}
	const name = quality.resolution?.name ?? 'Unknown'
	const suffixes: string[] = []
	if (quality.revision?.isRepack) {
		suffixes.push('Repack')
	}
	if (quality.revision?.isProper) {
		suffixes.push('Proper')
	}
	if (quality.revision?.isReal) {
		suffixes.push('Real')
	}
	return suffixes.length > 0 ? `${name} ${suffixes.join(' ')}` : name
}

/** Badge tone for a health issue or command status severity string. */
export function severityTone(type: string): 'ok' | 'warn' | 'danger' | 'info' | 'neutral' {
	switch (type) {
		case 'ERROR':
		case 'FAILED':
			return 'danger'
		case 'WARNING':
		case 'NOTICE':
			return 'warn'
		case 'OK':
		case 'COMPLETED':
			return 'ok'
		default:
			return 'neutral'
	}
}
