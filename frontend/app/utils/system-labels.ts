/**
 * Sentence-case labels and status-to-tone mappings for the system area.
 * Command and scheduled task names are the C# record/registry names
 * (PascalCase, see Submarine.Core.Commands and SubmarineSeeder.DefaultTasks).
 */

/** Matches the `tone` prop of ui/SBadge.vue. */
export type SBadgeTone = 'ok' | 'warn' | 'danger' | 'info' | 'neutral'

const COMMAND_LABELS: Record<string, string> = {
	RefreshSeries: 'Refresh series',
	RefreshMovie: 'Refresh movie',
	RefreshMetadata: 'Refresh metadata',
	RescanSeries: 'Rescan series',
	RescanMovie: 'Rescan movie',
	RenameSeries: 'Rename series',
	RenameMovie: 'Rename movie',
	MoveSeries: 'Move series',
	MoveMovie: 'Move movie',
	SeriesSearch: 'Series search',
	SeasonSearch: 'Season search',
	EpisodeSearch: 'Episode search',
	MovieSearch: 'Movie search',
	MissingSearch: 'Missing search',
	CutoffUnmetSearch: 'Cutoff unmet search',
	RssSync: 'RSS sync',
	DownloadMonitor: 'Download monitor',
	ProcessPendingReleases: 'Process pending releases',
	ImportListSync: 'Import list sync',
	Backup: 'Back up library',
	HealthCheck: 'Health check',
	IndexerDefinitionSync: 'Indexer definition sync',
	RecycleBinCleanup: 'Recycle bin cleanup',
	CheckForUpdate: 'Check for update',
	ClearBlocklist: 'Clear blocklist',
	CommandCleanup: 'Command cleanup',
}

/** Sentence-case label for a command or scheduled task registry name. */
export function commandLabel(name: string): string {
	const known = COMMAND_LABELS[name]
	if (known) {
		return known
	}
	// Fallback for names not in the map: split PascalCase into words.
	const spaced = name.replace(/([a-z0-9])([A-Z])/g, '$1 $2')
	return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase()
}

const COMMAND_STATUS_LABELS: Record<string, string> = {
	QUEUED: 'Queued',
	RUNNING: 'Running',
	COMPLETED: 'Completed',
	FAILED: 'Failed',
	CANCELLED: 'Cancelled',
}

export function commandStatusLabel(status: string): string {
	return COMMAND_STATUS_LABELS[status] ?? status
}

const COMMAND_STATUS_TONES: Record<string, SBadgeTone> = {
	QUEUED: 'neutral',
	RUNNING: 'info',
	COMPLETED: 'ok',
	FAILED: 'danger',
	CANCELLED: 'neutral',
}

export function commandStatusTone(status: string): SBadgeTone {
	return COMMAND_STATUS_TONES[status] ?? 'neutral'
}

const COMMAND_TRIGGER_LABELS: Record<string, string> = {
	MANUAL: 'Manual',
	SCHEDULED: 'Scheduled',
	SYSTEM: 'System',
}

export function commandTriggerLabel(trigger: string): string {
	return COMMAND_TRIGGER_LABELS[trigger] ?? trigger
}

const HEALTH_TYPE_LABELS: Record<string, string> = {
	OK: 'OK',
	NOTICE: 'Notice',
	WARNING: 'Warning',
	ERROR: 'Error',
}

export function healthTypeLabel(type: string): string {
	return HEALTH_TYPE_LABELS[type] ?? type
}

const HEALTH_TYPE_TONES: Record<string, SBadgeTone> = {
	OK: 'ok',
	NOTICE: 'info',
	WARNING: 'warn',
	ERROR: 'danger',
}

export function healthTypeTone(type: string): SBadgeTone {
	return HEALTH_TYPE_TONES[type] ?? 'neutral'
}

const LOG_LEVEL_TONES: Record<string, SBadgeTone> = {
	Verbose: 'neutral',
	Debug: 'neutral',
	Information: 'info',
	Warning: 'warn',
	Error: 'danger',
	Fatal: 'danger',
}

export function logLevelTone(level: string): SBadgeTone {
	return LOG_LEVEL_TONES[level] ?? 'neutral'
}

/** Serilog levels the log filter chips and level select offer. */
export const LOG_LEVELS = ['Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal'] as const

/** Backup file names end in `_MANUAL.zip` or `_SCHEDULED.zip` (see BackupService.CreateAsync). */
export function backupKindLabel(name: string): string {
	if (name.endsWith('_MANUAL.zip')) {
		return 'Manual'
	}
	if (name.endsWith('_SCHEDULED.zip')) {
		return 'Scheduled'
	}
	return 'Unknown'
}
