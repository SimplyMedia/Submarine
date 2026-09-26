/**
 * Sentence-case labels and status-to-tone mappings for the system area.
 * Command and scheduled task names are the C# record/registry names
 * (PascalCase, see Submarine.Core.Commands and SubmarineSeeder.DefaultTasks).
 */

/** Matches the `tone` prop of ui/SBadge.vue. */
export type SBadgeTone = 'ok' | 'warn' | 'danger' | 'info' | 'neutral'

const COMMAND_LABELS: Record<string, string> = {
	RefreshSeries: 'utils.systemLabels.commands.refreshSeries',
	RefreshMovie: 'utils.systemLabels.commands.refreshMovie',
	RefreshMetadata: 'utils.systemLabels.commands.refreshMetadata',
	RescanSeries: 'utils.systemLabels.commands.rescanSeries',
	RescanMovie: 'utils.systemLabels.commands.rescanMovie',
	RenameSeries: 'utils.systemLabels.commands.renameSeries',
	RenameMovie: 'utils.systemLabels.commands.renameMovie',
	MoveSeries: 'utils.systemLabels.commands.moveSeries',
	MoveMovie: 'utils.systemLabels.commands.moveMovie',
	SeriesSearch: 'utils.systemLabels.commands.seriesSearch',
	SeasonSearch: 'utils.systemLabels.commands.seasonSearch',
	EpisodeSearch: 'utils.systemLabels.commands.episodeSearch',
	MovieSearch: 'utils.systemLabels.commands.movieSearch',
	MissingSearch: 'utils.systemLabels.commands.missingSearch',
	CutoffUnmetSearch: 'utils.systemLabels.commands.cutoffUnmetSearch',
	RssSync: 'utils.systemLabels.commands.rssSync',
	DownloadMonitor: 'utils.systemLabels.commands.downloadMonitor',
	ProcessPendingReleases: 'utils.systemLabels.commands.processPendingReleases',
	ImportListSync: 'utils.systemLabels.commands.importListSync',
	Backup: 'utils.systemLabels.commands.backup',
	HealthCheck: 'utils.systemLabels.commands.healthCheck',
	IndexerDefinitionSync: 'utils.systemLabels.commands.indexerDefinitionSync',
	RecycleBinCleanup: 'utils.systemLabels.commands.recycleBinCleanup',
	CheckForUpdate: 'utils.systemLabels.commands.checkForUpdate',
	ClearBlocklist: 'utils.systemLabels.commands.clearBlocklist',
	CommandCleanup: 'utils.systemLabels.commands.commandCleanup',
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
	QUEUED: 'utils.systemLabels.commandStatus.queued',
	RUNNING: 'utils.systemLabels.commandStatus.running',
	COMPLETED: 'utils.systemLabels.commandStatus.completed',
	FAILED: 'utils.systemLabels.commandStatus.failed',
	CANCELLED: 'utils.systemLabels.commandStatus.cancelled',
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
	MANUAL: 'utils.systemLabels.commandTrigger.manual',
	SCHEDULED: 'utils.systemLabels.commandTrigger.scheduled',
	SYSTEM: 'utils.systemLabels.commandTrigger.system',
}

export function commandTriggerLabel(trigger: string): string {
	return COMMAND_TRIGGER_LABELS[trigger] ?? trigger
}

const HEALTH_TYPE_LABELS: Record<string, string> = {
	OK: 'utils.systemLabels.healthType.ok',
	NOTICE: 'utils.systemLabels.healthType.notice',
	WARNING: 'utils.systemLabels.healthType.warning',
	ERROR: 'utils.systemLabels.healthType.error',
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

const LOG_LEVEL_LABELS: Record<string, string> = {
	Verbose: 'utils.systemLabels.logLevel.verbose',
	Debug: 'utils.systemLabels.logLevel.debug',
	Information: 'utils.systemLabels.logLevel.information',
	Warning: 'utils.systemLabels.logLevel.warning',
	Error: 'utils.systemLabels.logLevel.error',
	Fatal: 'utils.systemLabels.logLevel.fatal',
}

export function logLevelLabel(level: string): string {
	return LOG_LEVEL_LABELS[level] ?? level
}

export function logLevelTone(level: string): SBadgeTone {
	return LOG_LEVEL_TONES[level] ?? 'neutral'
}

/** Serilog levels the log filter chips and level select offer. */
export const LOG_LEVELS = ['Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal'] as const

/** Backup file names end in `_MANUAL.zip` or `_SCHEDULED.zip` (see BackupService.CreateAsync). */
export function backupKindLabel(name: string): string {
	if (name.endsWith('_MANUAL.zip')) {
		return 'utils.systemLabels.backupKind.manual'
	}
	if (name.endsWith('_SCHEDULED.zip')) {
		return 'utils.systemLabels.backupKind.scheduled'
	}
	return 'utils.systemLabels.backupKind.unknown'
}
