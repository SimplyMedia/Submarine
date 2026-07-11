namespace Submarine.Core.Download;

/// <summary>
///     Status of an item tracked by a download client
/// </summary>
public enum DownloadItemStatus
{
	QUEUED,
	DOWNLOADING,
	PAUSED,
	COMPLETED,
	FAILED,
	WARNING
}
