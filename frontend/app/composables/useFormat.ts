import { reactive } from 'vue'

let formatLocale = () => 'en-GB'
let translateFormatMessage = (_key: string, fallback: string) => fallback
let formatDatePart = (date: Date, options: Intl.DateTimeFormatOptions, locale: string) =>
	new Intl.DateTimeFormat(locale, options).format(date)
let formatDateParts = (date: Date, options: Intl.DateTimeFormatOptions, locale: string) =>
	new Intl.DateTimeFormat(locale, options).formatToParts(date)
let formatNumberPart = (value: number, options: Intl.NumberFormatOptions, locale: string) =>
	new Intl.NumberFormat(locale, options).format(value)

export function configureFormatLocalization(
	locale: () => string,
	translate: (key: string, fallback: string) => string,
	dateFormatter: (date: Date, options: Intl.DateTimeFormatOptions, locale: string) => string,
	datePartsFormatter: (date: Date, options: Intl.DateTimeFormatOptions, locale: string) => Intl.DateTimeFormatPart[],
	numberFormatter: (value: number, options: Intl.NumberFormatOptions, locale: string) => string,
): void {
	formatLocale = locale
	translateFormatMessage = translate
	formatDatePart = dateFormatter
	formatDateParts = datePartsFormatter
	formatNumberPart = numberFormatter
}

/**
 * Formatting helpers. Dates/times use the instance's UiConfig tokens (set via
 * `applyUiFormat` on boot and after saving settings/ui.vue); byte units are
 * always decimal, local timezone throughout.
 */

export function formatBytes(bytes: number, decimals = 1): string {
	if (!Number.isFinite(bytes) || bytes <= 0) {
		return `${localizedInteger(0, formatLocale() || 'en-GB')} B`
	}
	const units = ['B', 'KB', 'MB', 'GB', 'TB', 'PB']
	const exponent = Math.min(Math.floor(Math.log(bytes) / Math.log(1000)), units.length - 1)
	const value = bytes / 1000 ** exponent
	const places = exponent === 0 ? 0 : decimals
	const rounded = formatNumberPart(value, { minimumFractionDigits: places, maximumFractionDigits: places, useGrouping: false }, formatLocale() || 'en-GB')
	return `${rounded} ${units[exponent]}`
}

/** Current UiConfig date/time preferences; defaults match the pre-config hardcoded format. */
const uiFormat = reactive({
	shortDateFormat: 'D MMM YYYY',
	longDateFormat: 'dddd, D MMMM YYYY',
	timeFormat: 'HH:mm',
	showRelativeDates: true,
})

/** Apply UiConfig fields fetched from the API; called on boot and after saving settings/ui.vue. */
export function applyUiFormat(config: {
	shortDateFormat?: string | null
	longDateFormat?: string | null
	timeFormat?: string | null
	showRelativeDates?: boolean | null
}): void {
	if (config.shortDateFormat) {
		uiFormat.shortDateFormat = config.shortDateFormat
	}
	if (config.longDateFormat) {
		uiFormat.longDateFormat = config.longDateFormat
	}
	if (config.timeFormat) {
		uiFormat.timeFormat = config.timeFormat
	}
	if (config.showRelativeDates != null) {
		uiFormat.showRelativeDates = config.showRelativeDates
	}
}

const dateNameCache = new Map<string, { shortMonths: string[], longMonths: string[], shortWeekdays: string[], longWeekdays: string[] }>()

function dateNames(locale: string) {
	let names = dateNameCache.get(locale)
	if (!names) {
		names = {
			shortMonths: Array.from({ length: 12 }, (_, month) => formatDatePart(new Date(Date.UTC(2020, month, 1)), { month: 'short', timeZone: 'UTC' }, locale).replace(/^Sept$/, 'Sep')),
			longMonths: Array.from({ length: 12 }, (_, month) => formatDatePart(new Date(Date.UTC(2020, month, 1)), { month: 'long', timeZone: 'UTC' }, locale)),
			shortWeekdays: Array.from({ length: 7 }, (_, weekday) => formatDatePart(new Date(Date.UTC(2020, 5, 7 + weekday)), { weekday: 'short', timeZone: 'UTC' }, locale)),
			longWeekdays: Array.from({ length: 7 }, (_, weekday) => formatDatePart(new Date(Date.UTC(2020, 5, 7 + weekday)), { weekday: 'long', timeZone: 'UTC' }, locale)),
		}
		dateNameCache.set(locale, names)
	}
	return names
}
function localizedDatePart(date: Date, options: Intl.DateTimeFormatOptions, part: Intl.DateTimeFormatPartTypes, locale: string): string {
	return formatDateParts(date, options, locale).find(item => item.type === part)?.value ?? ''
}

function localizedHour(date: Date, locale: string, twelveHour: boolean, twoDigits: boolean): string {
	const options: Intl.DateTimeFormatOptions = twelveHour
		? { hour: twoDigits ? '2-digit' : 'numeric', hourCycle: 'h12' }
		: { hour: twoDigits ? '2-digit' : 'numeric', hourCycle: 'h23' }
	return localizedDatePart(date, options, 'hour', locale)
}

/** Longest-match-first so e.g. `MMMM` isn't shadowed by `M`. */
const MOMENT_TOKEN = /YYYY|YY|MMMM|MMM|MM|M|dddd|ddd|DD|D|HH|H|hh|h|mm|m|ss|s|A|a/g

const numberOptions: Intl.NumberFormatOptions = { useGrouping: false, maximumFractionDigits: 0 }
const twoDigitNumberOptions: Intl.NumberFormatOptions = { ...numberOptions, minimumIntegerDigits: 2 }

function localizedInteger(value: number, locale: string, twoDigits = false): string {
	return formatNumberPart(value, twoDigits ? twoDigitNumberOptions : numberOptions, locale)
}

function hour12(date: Date): number {
	const hours = date.getHours() % 12
	return hours === 0 ? 12 : hours
}
function localizedDayPeriod(date: Date, locale: string, uppercase: boolean): string {
	const marker = localizedDatePart(date, { hour: 'numeric', hour12: true }, 'dayPeriod', locale)
	if (!marker) {
		return translateFormatMessage(date.getHours() < 12 ? 'utils.useFormat.am' : 'utils.useFormat.pm', date.getHours() < 12 ? 'AM' : 'PM')
	}
	return uppercase ? marker.toLocaleUpperCase(locale) : marker.toLocaleLowerCase(locale)
}
function momentToken(date: Date, token: string): string {
	const locale = formatLocale() || 'en-GB'
	const names = dateNames(locale)
	switch (token) {
		case 'YYYY': return localizedInteger(date.getFullYear(), locale)
		case 'YY': return localizedInteger(date.getFullYear() % 100, locale, true)
		case 'MMMM': return names.longMonths[date.getMonth()]!
		case 'MMM': return names.shortMonths[date.getMonth()]!
		case 'MM': return localizedInteger(date.getMonth() + 1, locale, true)
		case 'M': return localizedInteger(date.getMonth() + 1, locale)
		case 'dddd': return names.longWeekdays[date.getDay()]!
		case 'ddd': return names.shortWeekdays[date.getDay()]!
		case 'DD': return localizedInteger(date.getDate(), locale, true)
		case 'D': return localizedInteger(date.getDate(), locale)
		case 'HH': return localizedHour(date, locale, false, true)
		case 'H': return localizedInteger(date.getHours(), locale)
		case 'hh': return localizedHour(date, locale, true, true)
		case 'h': return localizedInteger(hour12(date), locale)
		case 'mm': return localizedInteger(date.getMinutes(), locale, true)
		case 'm': return localizedInteger(date.getMinutes(), locale)
		case 'ss': return localizedInteger(date.getSeconds(), locale, true)
		case 's': return localizedInteger(date.getSeconds(), locale)
		case 'A': return localizedDayPeriod(date, locale, true)
		case 'a': return localizedDayPeriod(date, locale, false)
		default: return token
	}
}

function applyMomentFormat(date: Date, format: string): string {
	return format.replace(MOMENT_TOKEN, token => momentToken(date, token))
}

export function formatDate(date: string | Date): string {
	const d = typeof date === 'string' ? new Date(date) : date
	if (Number.isNaN(d.getTime())) {
		return ''
	}
	return applyMomentFormat(d, uiFormat.shortDateFormat)
}

/** Full weekday date, e.g. the calendar's day-view heading. */
export function formatLongDate(date: string | Date): string {
	const d = typeof date === 'string' ? new Date(date) : date
	if (Number.isNaN(d.getTime())) {
		return ''
	}
	return applyMomentFormat(d, uiFormat.longDateFormat)
}

export function formatDateTime(date: string | Date): string {
	const d = typeof date === 'string' ? new Date(date) : date
	if (Number.isNaN(d.getTime())) {
		return ''
	}
	return `${applyMomentFormat(d, uiFormat.shortDateFormat)}, ${applyMomentFormat(d, uiFormat.timeFormat)}`
}

/** Relative time (e.g. "in 2 hours"), or the absolute date/time when the user disabled relative dates. */
export function formatRelative(date: string | Date, now: Date = new Date()): string {
	const d = typeof date === 'string' ? new Date(date) : date
	if (Number.isNaN(d.getTime())) {
		return ''
	}
	if (!uiFormat.showRelativeDates) {
		return formatDateTime(d)
	}
	const diffMs = d.getTime() - now.getTime()
	const absMinutes = Math.abs(diffMs) / 60_000
	if (absMinutes < 1) {
		return translateFormatMessage('utils.useFormat.justNow', 'just now')
	}
	const units: Array<[Intl.RelativeTimeFormatUnit, number]> = [
		['minute', 1],
		['hour', 60],
		['day', 60 * 24],
		['month', 60 * 24 * 30],
		['year', 60 * 24 * 365],
	]
	let chosen: [Intl.RelativeTimeFormatUnit, number] = ['minute', 1]
	for (const unit of units) {
		if (absMinutes >= unit[1]) {
			chosen = unit
		}
	}
	const value = Math.round(diffMs / 60_000 / chosen[1])
	return new Intl.RelativeTimeFormat(formatLocale() || 'en-GB', { numeric: 'auto' }).format(value, chosen[0])
}

export function formatDuration(minutes: number): string {
	const locale = formatLocale() || 'en-GB'
	if (!Number.isFinite(minutes) || minutes <= 0) {
		return `0${translateFormatMessage('utils.useFormat.minuteShort', 'm')}`
	}
	const hours = Math.floor(minutes / 60)
	const rest = Math.round(minutes % 60)
	const minuteLabel = translateFormatMessage('utils.useFormat.minuteShort', 'm')
	const hourLabel = translateFormatMessage('utils.useFormat.hourShort', 'h')
	if (hours === 0) {
		return `${localizedInteger(rest, locale)}${minuteLabel}`
	}
	const hourPart = `${localizedInteger(hours, locale)}${hourLabel}`
	return rest === 0 ? hourPart : `${hourPart} ${localizedInteger(rest, locale)}${minuteLabel}`
}
