/**
 * Human readable labels for enums and field names used across the settings
 * area. Backend enum member names are serialization contracts (never
 * renamed); this file is the only place that turns them into copy.
 */

export interface LabelOption { value: string, label: string }

/** Generic SCREAMING_SNAKE enum member to sentence case, for values with no dedicated dictionary. */
export function humanizeEnumValue(name: string): string {
	if (!name) {
		return ''
	}
	const words = name.split('_').filter(Boolean)
	return words
		.map((word, index) => {
			const lower = word.toLowerCase()
			return index === 0 ? lower.charAt(0).toUpperCase() + lower.slice(1) : lower
		})
		.join(' ')
}

function toOptions(map: Record<string, string>): LabelOption[] {
	return Object.entries(map).map(([value, label]) => ({ value, label }))
}

function makeLookup(map: Record<string, string>) {
	return (value: string | null | undefined) => (value ? (map[value] ?? humanizeEnumValue(value)) : '')
}

/** camelCase or PascalCase property name to a sentence case label, with a few domain acronyms preserved. */
export function humanizeFieldName(name: string): string {
	const spaced = name
		.replace(/([a-z0-9])([A-Z])/g, '$1 $2')
		.replace(/([A-Z]+)([A-Z][a-z])/g, '$1 $2')
		.toLowerCase()
	const words = spaced.split(/\s+/).filter(Boolean)
	const acronyms: Record<string, string> = { ssl: 'SSL', url: 'URL', api: 'API', id: 'ID', tv: 'TV', nzb: 'NZB', rpc: 'RPC', ip: 'IP' }
	return words
		.map((word, index) => {
			if (acronyms[word]) {
				return acronyms[word]
			}
			return index === 0 ? word.charAt(0).toUpperCase() + word.slice(1) : word
		})
		.join(' ')
}

const AUTH_METHOD: Record<string, string> = { NONE: 'Disabled', FORMS: 'Forms login' }
const THEME: Record<string, string> = { AUTO: 'Match system', LIGHT: 'Light', DARK: 'Dark' }
const COLON_REPLACEMENT: Record<string, string> = {
	DELETE: 'Delete',
	DASH: 'Replace with dash',
	SPACE_DASH: 'Replace with space dash',
	SPACE_DASH_SPACE: 'Replace with space dash space',
	SMART: 'Smart replace',
}
const MULTI_EPISODE_STYLE: Record<string, string> = {
	EXTEND: 'Extend',
	DUPLICATE: 'Duplicate',
	REPEAT: 'Repeat',
	SCENE: 'Scene',
	RANGE: 'Range',
	PREFIXED_RANGE: 'Prefixed range',
}
const DOWNLOAD_PROPERS_AND_REPACKS: Record<string, string> = {
	PREFER_AND_UPGRADE: 'Prefer and upgrade',
	DO_NOT_UPGRADE: 'Do not prefer',
	DO_NOT_PREFER: 'Do not prefer or upgrade',
}
const CLEAN_LIBRARY_LEVEL: Record<string, string> = {
	DISABLED: 'Disabled',
	LOG_ONLY: 'Log only',
	KEEP_AND_UNMONITOR: 'Keep and unmonitor',
	REMOVE_AND_KEEP: 'Remove and keep files',
	REMOVE_AND_DELETE: 'Remove and delete files',
}
const MEDIA_KIND: Record<string, string> = { SERIES: 'Series', MOVIES: 'Movies' }
const PROTOCOL: Record<string, string> = { BITTORRENT: 'Torrent', USENET: 'Usenet', XDCC: 'XDCC' }
const RELEASE_FILTER_MODE: Record<string, string> = { ALLOW: 'Allow', BLOCK: 'Block', PREFER: 'Prefer' }
const RELEASE_FILTER_FIELD: Record<string, string> = {
	RELEASE_GROUP: 'Release group',
	INDEXER: 'Indexer',
	QUALITY: 'Quality',
	LANGUAGE: 'Language',
	SOURCE: 'Source',
}

const QUALITY_SOURCE: Record<string, string> = {
	UNKNOWN: 'Unknown',
	CAM: 'Cam',
	TV: 'TV',
	DVD: 'DVD',
	RAW_HD: 'Raw-HD',
	WEB_RIP: 'WebRip',
	WEB_DL: 'WebDL',
	BLURAY: 'Bluray',
	BLURAY_REMUX: 'Bluray remux',
	BLURAY_DISK: 'Bluray disk',
}

/** Resolution member name (e.g. R1080_P) to display string (e.g. 1080p). */
export function qualityResolutionLabel(value: string | null | undefined): string {
	if (!value) {
		return 'Unknown'
	}
	const match = /^R(\d+)_P$/.exec(value)
	return match ? `${match[1]}p` : humanizeEnumValue(value)
}

const CUSTOM_FORMAT_SPEC_TYPE: Record<string, string> = {
	RELEASE_TITLE: 'Release title',
	RELEASE_GROUP: 'Release group',
	LANGUAGE: 'Language',
	QUALITY_SOURCE: 'Quality source',
	RESOLUTION: 'Resolution',
	STREAMING_PROVIDER: 'Streaming provider',
	EDITION: 'Edition',
	RELEASE_FLAG: 'Release flag',
	PROTOCOL: 'Protocol',
	HARDCODED_SUBS: 'Hardcoded subs',
	SIZE: 'Size',
	YEAR: 'Year',
	INDEXER_FLAG: 'Indexer flag',
	RELEASE_TYPE: 'Release type',
	QUALITY_MODIFIER: 'Quality modifier',
}

/** SeriesReleaseType member names (Submarine.Core.Release). */
const RELEASE_TYPE: Record<string, string> = {
	SPECIAL: 'Special',
	EPISODE: 'Episode',
	MULTI_EPISODES: 'Multi-episode',
	PARTIAL_SEASON: 'Partial season',
	FULL_SEASON: 'Full season',
	MULTI_SEASON: 'Multi-season',
}

/** QualityModifier member names (Submarine.Core.Enums). */
const QUALITY_MODIFIER: Record<string, string> = {
	NONE: 'None',
	RAW_HD: 'Raw HD',
	BLURAY_DISK: 'BluRay disc',
	BLURAY_REMUX: 'BluRay remux',
}

const DOWNLOAD_CLIENT_TYPE: Record<string, string> = {
	QBITTORRENT: 'qBittorrent',
	TRANSMISSION: 'Transmission',
	DELUGE: 'Deluge',
	RTORRENT: 'rTorrent',
	UTORRENT: 'uTorrent',
	ARIA2: 'Aria2',
	FLOOD: 'Flood',
	DOWNLOAD_STATION: 'Download Station',
	SABNZBD: 'SABnzbd',
	NZBGET: 'NZBGet',
	TORRENT_BLACKHOLE: 'Torrent blackhole',
	USENET_BLACKHOLE: 'Usenet blackhole',
}

const IMPORT_LIST_TYPE: Record<string, string> = {
	TMDB_LIST: 'TMDB list',
	TMDB_POPULAR: 'TMDB popular',
	TMDB_COLLECTION: 'TMDB collection',
	TMDB_PERSON: 'TMDB person',
	TRAKT_LIST: 'Trakt list',
	TRAKT_POPULAR: 'Trakt popular',
	TRAKT_USER: 'Trakt user',
	ANILIST_SEASON: 'AniList season',
	PLEX: 'Plex watchlist',
	SONARR: 'Sonarr',
	RADARR: 'Radarr',
	STEVEN_LU: 'StevenLu',
	CUSTOM: 'Custom',
	SIMKL: 'Simkl',
	IMDB: 'IMDb list',
	MYANIMELIST: 'MyAnimeList',
	RSS: 'RSS list',
}

const NOTIFICATION_TYPE: Record<string, string> = {
	DISCORD: 'Discord',
	TELEGRAM: 'Telegram',
	WEBHOOK: 'Webhook',
	SLACK: 'Slack',
	PUSHOVER: 'Pushover',
	PUSHBULLET: 'Pushbullet',
	GOTIFY: 'Gotify',
	KODI: 'Kodi',
	CUSTOM_SCRIPT: 'Custom script',
	PLEX: 'Plex',
	EMBY: 'Emby',
	JELLYFIN: 'Jellyfin',
	EMAIL: 'Email',
	NTFY: 'Ntfy',
	APPRISE: 'Apprise',
}

const STREAMING_PROVIDER: Record<string, string> = {
	AMAZON: 'Amazon',
	NETFLIX: 'Netflix',
	APPLE_TV: 'Apple TV+',
	HBO_MAX: 'Max',
	DISNEY: 'Disney+',
	HULU: 'Hulu',
	CRUNCHYROLL: 'Crunchyroll',
	FUNIMATION: 'Funimation',
	YOUTUBE_PREMIUM: 'YouTube Premium',
	PEACOCK: 'Peacock',
	DC_UNIVERSE: 'DC Universe',
	HBO_NOW: 'HBO Now',
	PARAMOUNT_PLUS: 'Paramount+',
	COMEDY_CENTRAL: 'Comedy Central',
	CRAVE: 'Crave',
	HIDIVE: 'HIDIVE',
	ITUNES: 'iTunes',
	MOVIES_ANYWHERE: 'Movies Anywhere',
	STAN: 'Stan',
	ROKU: 'Roku',
}

/** TorrentReleaseFlags member names (Submarine.Core.Release.Torrent), a bit flag set. */
const RELEASE_FLAG: Record<string, string> = {
	FREELEECH: 'Freeleech',
	HALFLEECH: 'Halfleech',
	NEUTRALLEECH: 'Neutralleech',
	DOUBLE_UPLOAD: 'Double upload',
	OTHER_PROMOTION: 'Other promotion',
	SCENE: 'Scene',
	INTERNAL: 'Internal',
	EXCLUSIVE: 'Exclusive',
}

/** IndexerFlag member names (Submarine.Core.Indexers). */
const INDEXER_FLAG: Record<string, string> = {
	FREELEECH: 'Freeleech',
	HALFLEECH: 'Halfleech',
	DOUBLE_UPLOAD: 'Double upload',
	INTERNAL: 'Internal',
	SCENE: 'Scene',
	EXCLUSIVE: 'Exclusive',
	G_FREELEECH: '25% freeleech',
}

const LANGUAGE_NAMES = [
	'ENGLISH', 'FRENCH', 'SPANISH', 'GERMAN', 'ITALIAN', 'DANISH', 'DUTCH', 'JAPANESE', 'ICELANDIC', 'CHINESE',
	'RUSSIAN', 'POLISH', 'VIETNAMESE', 'SWEDISH', 'NORWEGIAN', 'FINNISH', 'TURKISH', 'PORTUGUESE', 'FLEMISH',
	'GREEK', 'KOREAN', 'HUNGARIAN', 'HEBREW', 'LITHUANIAN', 'CZECH', 'ARABIC', 'HINDI',
] as const
const LANGUAGE: Record<string, string> = Object.fromEntries(LANGUAGE_NAMES.map(name => [name, humanizeEnumValue(name)]))

export const authMethodOptions = toOptions(AUTH_METHOD)
export const themeOptions = toOptions(THEME)
export const colonReplacementOptions = toOptions(COLON_REPLACEMENT)
export const multiEpisodeStyleOptions = toOptions(MULTI_EPISODE_STYLE)
export const downloadPropersAndRepacksOptions = toOptions(DOWNLOAD_PROPERS_AND_REPACKS)
export const cleanLibraryLevelOptions = toOptions(CLEAN_LIBRARY_LEVEL)
export const mediaKindOptions = toOptions(MEDIA_KIND)
export const protocolOptions = toOptions(PROTOCOL)
export const releaseFilterModeOptions = toOptions(RELEASE_FILTER_MODE)
export const releaseFilterFieldOptions = toOptions(RELEASE_FILTER_FIELD)
export const qualitySourceOptions = toOptions(QUALITY_SOURCE)
export const customFormatSpecTypeOptions = toOptions(CUSTOM_FORMAT_SPEC_TYPE)
export const downloadClientTypeOptions = toOptions(DOWNLOAD_CLIENT_TYPE)
export const importListTypeOptions = toOptions(IMPORT_LIST_TYPE)
export const notificationTypeOptions = toOptions(NOTIFICATION_TYPE)
export const streamingProviderOptions = toOptions(STREAMING_PROVIDER)
export const releaseFlagOptions = toOptions(RELEASE_FLAG)
export const indexerFlagOptions = toOptions(INDEXER_FLAG)
export const releaseTypeOptions = toOptions(RELEASE_TYPE)
export const qualityModifierOptions = toOptions(QUALITY_MODIFIER)
export const languageOptions = toOptions(LANGUAGE)

export const authMethodLabel = makeLookup(AUTH_METHOD)
export const themeLabel = makeLookup(THEME)
export const colonReplacementLabel = makeLookup(COLON_REPLACEMENT)
export const multiEpisodeStyleLabel = makeLookup(MULTI_EPISODE_STYLE)
export const downloadPropersAndRepacksLabel = makeLookup(DOWNLOAD_PROPERS_AND_REPACKS)
export const cleanLibraryLevelLabel = makeLookup(CLEAN_LIBRARY_LEVEL)
export const mediaKindLabel = makeLookup(MEDIA_KIND)
export const protocolLabel = makeLookup(PROTOCOL)
export const releaseFilterModeLabel = makeLookup(RELEASE_FILTER_MODE)
export const releaseFilterFieldLabel = makeLookup(RELEASE_FILTER_FIELD)
export const qualitySourceLabel = makeLookup(QUALITY_SOURCE)
export const customFormatSpecTypeLabel = makeLookup(CUSTOM_FORMAT_SPEC_TYPE)
export const downloadClientTypeLabel = makeLookup(DOWNLOAD_CLIENT_TYPE)
export const importListTypeLabel = makeLookup(IMPORT_LIST_TYPE)
export const notificationTypeLabel = makeLookup(NOTIFICATION_TYPE)
export const streamingProviderLabel = makeLookup(STREAMING_PROVIDER)
export const releaseFlagLabel = makeLookup(RELEASE_FLAG)
export const indexerFlagLabel = makeLookup(INDEXER_FLAG)
export const releaseTypeLabel = makeLookup(RELEASE_TYPE)
export const qualityModifierLabel = makeLookup(QUALITY_MODIFIER)
export const languageLabel = makeLookup(LANGUAGE)

/** Icon for a notification/provider type, used by ProviderCard grids. Falls back to a generic bell. */
export function notificationTypeIcon(type: string): string {
	const icons: Record<string, string> = {
		DISCORD: 'lucide:message-circle',
		TELEGRAM: 'lucide:send',
		WEBHOOK: 'lucide:webhook',
		SLACK: 'lucide:hash',
		PUSHOVER: 'lucide:bell-ring',
		PUSHBULLET: 'lucide:bell',
		GOTIFY: 'lucide:megaphone',
		KODI: 'lucide:tv',
		CUSTOM_SCRIPT: 'lucide:terminal',
		PLEX: 'lucide:play',
		EMBY: 'lucide:play-circle',
		JELLYFIN: 'lucide:play-square',
		EMAIL: 'lucide:mail',
		NTFY: 'lucide:bell-plus',
		APPRISE: 'lucide:radio',
	}
	return icons[type] ?? 'lucide:bell'
}

/** Icon for a download client type, used by client picker grids. */
export function downloadClientTypeIcon(type: string): string {
	const icons: Record<string, string> = {
		QBITTORRENT: 'lucide:magnet',
		TRANSMISSION: 'lucide:magnet',
		DELUGE: 'lucide:magnet',
		RTORRENT: 'lucide:magnet',
		UTORRENT: 'lucide:magnet',
		ARIA2: 'lucide:magnet',
		FLOOD: 'lucide:magnet',
		DOWNLOAD_STATION: 'lucide:hard-drive',
		SABNZBD: 'lucide:newspaper',
		NZBGET: 'lucide:newspaper',
		TORRENT_BLACKHOLE: 'lucide:folder-down',
		USENET_BLACKHOLE: 'lucide:folder-down',
	}
	return icons[type] ?? 'lucide:download'
}

/** Icon for an import list type, used by list picker grids. */
export function importListTypeIcon(type: string): string {
	const icons: Record<string, string> = {
		TMDB_LIST: 'lucide:list',
		TMDB_POPULAR: 'lucide:flame',
		TMDB_COLLECTION: 'lucide:layers',
		TMDB_PERSON: 'lucide:user',
		TRAKT_LIST: 'lucide:list',
		TRAKT_POPULAR: 'lucide:flame',
		TRAKT_USER: 'lucide:user',
		ANILIST_SEASON: 'lucide:calendar-days',
		PLEX: 'lucide:play',
		SONARR: 'lucide:tv',
		RADARR: 'lucide:film',
		STEVEN_LU: 'lucide:list',
		CUSTOM: 'lucide:link',
		SIMKL: 'lucide:tv',
		IMDB: 'lucide:star',
		MYANIMELIST: 'lucide:sparkles',
		RSS: 'lucide:rss',
	}
	return icons[type] ?? 'lucide:list'
}
