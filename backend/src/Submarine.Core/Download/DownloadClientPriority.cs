namespace Submarine.Core.Download;

/// <summary>
///     Queue position preference for clients that support top/bottom ordering
/// </summary>
public enum DownloadClientItemPriority
{
	/// <summary>Add to the end of the queue</summary>
	LAST,

	/// <summary>Add to the front of the queue</summary>
	FIRST
}

/// <summary>
///     Transfer priority for clients that support priority values (usenet clients)
/// </summary>
public enum DownloadClientPriority
{
	/// <summary>Download paused</summary>
	PAUSED = -2,

	/// <summary>Low priority</summary>
	LOW = -1,

	/// <summary>Normal priority</summary>
	NORMAL = 0,

	/// <summary>High priority</summary>
	HIGH = 1,

	/// <summary>Forced download, ignores queue rules</summary>
	FORCED = 2
}
