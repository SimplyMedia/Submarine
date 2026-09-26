/**
 * Sentence-case labels for library enums. The API returns UPPER_SNAKE
 * member names; these maps are the only place that turns them into copy.
 */
import type { components } from '~/types/api'
import { i18n } from '~/i18n'

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
	CONTINUING: 'utils.libraryLabels.seriesStatus.continuing',
	ENDED: 'utils.libraryLabels.seriesStatus.ended',
	UPCOMING: 'utils.libraryLabels.seriesStatus.upcoming',
	UNKNOWN: 'utils.libraryLabels.seriesStatus.unknown',
}

const seriesTypeLabels: Record<SeriesType, string> = {
	STANDARD: 'utils.libraryLabels.seriesType.standard',
	DAILY: 'utils.libraryLabels.seriesType.daily',
	ANIME: 'utils.libraryLabels.seriesType.anime',
}

const seriesNumberingLabels: Record<SeriesNumbering, string> = {
	AIRED: 'utils.libraryLabels.seriesNumbering.aired',
	DVD: 'utils.libraryLabels.seriesNumbering.dvd',
	ABSOLUTE: 'utils.libraryLabels.seriesNumbering.absolute',
}

const monitorNewItemsLabels: Record<MonitorNewItems, string> = {
	ALL: 'utils.libraryLabels.monitorNewItems.all',
	NONE: 'utils.libraryLabels.monitorNewItems.none',
}

const movieStatusLabels: Record<MovieStatus, string> = {
	ANNOUNCED: 'utils.libraryLabels.movieStatus.announced',
	IN_CINEMAS: 'utils.libraryLabels.movieStatus.inCinemas',
	RELEASED: 'utils.libraryLabels.movieStatus.released',
}

const minimumAvailabilityLabels: Record<MinimumAvailability, string> = {
	ANNOUNCED: 'utils.libraryLabels.minimumAvailability.announced',
	IN_CINEMAS: 'utils.libraryLabels.minimumAvailability.inCinemas',
	RELEASED: 'utils.libraryLabels.minimumAvailability.released',
}

const addMonitorOptionLabels: Record<AddMonitorOption, string> = {
	ALL: 'utils.libraryLabels.addMonitorOption.all',
	FUTURE: 'utils.libraryLabels.addMonitorOption.future',
	MISSING: 'utils.libraryLabels.addMonitorOption.missing',
	EXISTING: 'utils.libraryLabels.addMonitorOption.existing',
	PILOT: 'utils.libraryLabels.addMonitorOption.pilot',
	FIRST_SEASON: 'utils.libraryLabels.addMonitorOption.firstSeason',
	LATEST_SEASON: 'utils.libraryLabels.addMonitorOption.latestSeason',
	NONE: 'utils.libraryLabels.addMonitorOption.none',
	RECENT: 'utils.libraryLabels.addMonitorOption.recent',
	SKIP: 'utils.libraryLabels.addMonitorOption.skip',
}

const metadataProviderLabels: Record<MetadataProvider, string> = {
	TVDB: 'utils.libraryLabels.metadataProvider.tvdb',
	TMDB: 'utils.libraryLabels.metadataProvider.tmdb',
}

const historyEventTypeLabels: Record<HistoryEventType, string> = {
	GRABBED: 'utils.libraryLabels.historyEventType.grabbed',
	IMPORTED: 'utils.libraryLabels.historyEventType.imported',
	RENAMED: 'utils.libraryLabels.historyEventType.renamed',
	DELETED: 'utils.libraryLabels.historyEventType.deleted',
	FAILED: 'utils.libraryLabels.historyEventType.failed',
	IGNORED: 'utils.libraryLabels.historyEventType.ignored',
	UPGRADED: 'utils.libraryLabels.historyEventType.upgraded',
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
	['ALL', 'FUTURE', 'MISSING', 'EXISTING', 'PILOT', 'FIRST_SEASON', 'LATEST_SEASON', 'RECENT', 'NONE', 'SKIP'] as const
).map(value => ({ value, label: addMonitorOptionLabels[value] }))

/** Display name for a file's quality, e.g. "WebDL-1080p Proper". */
export function qualityLabel(quality: QualityModel | null | undefined): string {
	if (!quality) {
		return i18n.global.t('utils.libraryLabels.quality.unknown')
	}
	const name = quality.resolution?.name ?? i18n.global.t('utils.libraryLabels.quality.unknown')
	const suffixes: string[] = []
	if (quality.revision?.isRepack) {
		suffixes.push(i18n.global.t('utils.libraryLabels.quality.repack'))
	}
	if (quality.revision?.isProper) {
		suffixes.push(i18n.global.t('utils.libraryLabels.quality.proper'))
	}
	if (quality.revision?.isReal) {
		suffixes.push(i18n.global.t('utils.libraryLabels.quality.real'))
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
