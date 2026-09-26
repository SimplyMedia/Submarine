import { reactive } from 'vue'

/**
 * Formatting helpers. Dates/times use the instance's UiConfig tokens (set via
 * `applyUiFormat` on boot and after saving settings/ui.vue); byte units are
 * always decimal, local timezone throughout.
 */

export function formatBytes(bytes: number, decimals = 1): string {
	if (!Number.isFinite(bytes) || bytes <= 0) {
		return '0 B'
	}
	const units = ['B', 'KB', 'MB', 'GB', 'TB', 'PB']
	const exponent = Math.min(Math.floor(Math.log(bytes) / Math.log(1000)), units.length - 1)
	const value = bytes / 1000 ** exponent
	const rounded = value.toFixed(exponent === 0 ? 0 : decimals)
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

const MONTHS_SHORT = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
const MONTHS_LONG = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']
const WEEKDAYS_SHORT = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
const WEEKDAYS_LONG = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']

/** Longest-match-first so e.g. `MMMM` isn't shadowed by `M`. */
const MOMENT_TOKEN = /YYYY|YY|MMMM|MMM|MM|M|dddd|ddd|DD|D|HH|H|hh|h|mm|m|ss|s|A|a/g

function hour12(date: Date): number {
	const hours = date.getHours() % 12
	return hours === 0 ? 12 : hours
}

function momentToken(date: Date, token: string): string {
	switch (token) {
		case 'YYYY': return String(date.getFullYear())
		case 'YY': return String(date.getFullYear()).slice(-2)
		case 'MMMM': return MONTHS_LONG[date.getMonth()]!
		case 'MMM': return MONTHS_SHORT[date.getMonth()]!
		case 'MM': return String(date.getMonth() + 1).padStart(2, '0')
		case 'M': return String(date.getMonth() + 1)
		case 'dddd': return WEEKDAYS_LONG[date.getDay()]!
		case 'ddd': return WEEKDAYS_SHORT[date.getDay()]!
		case 'DD': return String(date.getDate()).padStart(2, '0')
		case 'D': return String(date.getDate())
		case 'HH': return String(date.getHours()).padStart(2, '0')
		case 'H': return String(date.getHours())
		case 'hh': return String(hour12(date)).padStart(2, '0')
		case 'h': return String(hour12(date))
		case 'mm': return String(date.getMinutes()).padStart(2, '0')
		case 'm': return String(date.getMinutes())
		case 'ss': return String(date.getSeconds()).padStart(2, '0')
		case 's': return String(date.getSeconds())
		case 'A': return date.getHours() < 12 ? 'AM' : 'PM'
		case 'a': return date.getHours() < 12 ? 'am' : 'pm'
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
		return 'just now'
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
	return new Intl.RelativeTimeFormat('en-GB', { numeric: 'auto' }).format(value, chosen[0])
}

export function formatDuration(minutes: number): string {
	if (!Number.isFinite(minutes) || minutes <= 0) {
		return '0m'
	}
	const hours = Math.floor(minutes / 60)
	const rest = Math.round(minutes % 60)
	if (hours === 0) {
		return `${rest}m`
	}
	return rest === 0 ? `${hours}h` : `${hours}h ${rest}m`
}
