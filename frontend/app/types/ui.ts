/** Shared prop types for the design system components. */

export interface SelectOption {
	value: string
	label: string
}

export interface MenuEntry {
	label: string
	icon?: string
	danger?: boolean
	disabled?: boolean
	onSelect?: () => void
}

export type MenuEntryOrSeparator = MenuEntry | { separator: true }

export interface TabEntry {
	value: string
	label: string
}

/** One tile in MediaPosterGrid / row in MediaTable. */
export interface PosterCardBadge {
	label: string
	tone?: 'ok' | 'warn' | 'danger' | 'info' | 'neutral'
}

export interface PosterCardItem {
	id: number
	to: string
	posterUrl: string | null
	title: string
	meta?: string
	/** Second meta line, e.g. a network or studio under the year. */
	metaSecondary?: string
	badges?: PosterCardBadge[]
	progress?: number | null
	progressLabel?: string
}

export interface MediaTableColumn {
	key: string
	label: string
	align?: 'left' | 'right'
	sortKey?: string
}

/** A search hit normalised from SeriesLookupDto / MovieLookupDto for AddMediaDialog. */
export interface MediaLookupResult {
	key: string
	title: string
	year: number | null
	overview: string | null
	posterUrl: string | null
	existingId: number | null
	/** TVDB id, when the hit came from the series lookup. */
	tvdbId: number | null
	/** TMDB id, when the hit came from the movie lookup (or the series lookup, if known). */
	tmdbId: number | null
	/** Upstream provider name, e.g. "tvdb" or "tmdb". */
	provider: string
}

/** One version row edited by VersionEditor before the series/movie exists. */
export interface DraftVersion {
	name: string
	qualityProfileId: number | null
	languageProfileId: number | null
	rootFolderId: number | null
}
