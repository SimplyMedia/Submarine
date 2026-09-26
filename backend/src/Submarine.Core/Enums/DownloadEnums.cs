namespace Submarine.Core.Enums;

/// <summary>
///     Status of a tracked download in the queue.
/// </summary>
public enum TrackedDownloadStatus
{
	/// <summary>Queued in the download client.</summary>
	QUEUED,

	/// <summary>Currently downloading.</summary>
	DOWNLOADING,

	/// <summary>Paused in the download client.</summary>
	PAUSED,

	/// <summary>Completed in the download client.</summary>
	COMPLETED,

	/// <summary>Failed.</summary>
	FAILED,

	/// <summary>Needs manual attention.</summary>
	WARNING
}

/// <summary>
///     Import pipeline state of a tracked download.
/// </summary>
public enum TrackedDownloadState
{
	/// <summary>Still downloading.</summary>
	DOWNLOADING,

	/// <summary>Waiting for import.</summary>
	IMPORT_PENDING,

	/// <summary>Currently importing.</summary>
	IMPORTING,

	/// <summary>Imported into the library.</summary>
	IMPORTED,

	/// <summary>Waiting for a retry of the failed import.</summary>
	FAILED_PENDING,

	/// <summary>Import failed.</summary>
	FAILED,

	/// <summary>Ignored by the user.</summary>
	IGNORED
}

/// <summary>
///     Type of a history entry.
/// </summary>
public enum HistoryEventType
{
	/// <summary>Release grabbed.</summary>
	GRABBED,

	/// <summary>Download imported.</summary>
	IMPORTED,

	/// <summary>File renamed.</summary>
	RENAMED,

	/// <summary>File or media deleted.</summary>
	DELETED,

	/// <summary>Download failed.</summary>
	FAILED,

	/// <summary>Release ignored.</summary>
	IGNORED,

	/// <summary>File upgraded.</summary>
	UPGRADED
}

/// <summary>
///     Why a release is held as pending.
/// </summary>
public enum PendingReleaseReason
{
	/// <summary>Waiting out the delay profile window.</summary>
	DELAY,

	/// <summary>Waiting for minimum availability.</summary>
	AVAILABILITY
}
