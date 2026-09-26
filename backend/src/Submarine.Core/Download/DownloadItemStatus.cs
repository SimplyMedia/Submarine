namespace Submarine.Core.Download;

/// <summary>
///     Status of an item tracked by a download client
/// </summary>
public enum DownloadItemStatus
{
	/// <summary>Waiting to be picked up by the client</summary>
	QUEUED,

	/// <summary>Actively transferring data</summary>
	DOWNLOADING,

	/// <summary>Stopped before completion, manually or by seeding rules</summary>
	PAUSED,

	/// <summary>Fully downloaded</summary>
	COMPLETED,

	/// <summary>The client reported a failure</summary>
	FAILED,

	/// <summary>Present but in a state that maps to neither of the above</summary>
	WARNING
}
