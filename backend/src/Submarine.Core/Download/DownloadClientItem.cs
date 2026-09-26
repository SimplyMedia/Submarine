namespace Submarine.Core.Download;

/// <summary>
///     Snapshot of a download tracked by a download client
/// </summary>
public record DownloadClientItem
{
	/// <summary>
	///     Client-side identifier of the download (e.g. torrent info hash or nzo id)
	/// </summary>
	public required string DownloadId { get; init; }

	/// <summary>
	///     Title of the download
	/// </summary>
	public required string Title { get; init; }

	/// <summary>
	///     Category, label or destination the download was added under
	/// </summary>
	public string? Category { get; init; }

	/// <summary>
	///     Total size in bytes
	/// </summary>
	public long TotalSize { get; init; }

	/// <summary>
	///     Bytes left to download
	/// </summary>
	public long RemainingSize { get; init; }

	/// <summary>
	///     Estimated time until completion, if the client reports one
	/// </summary>
	public TimeSpan? RemainingTime { get; init; }

	/// <summary>
	///     Path the client downloads into, once known
	/// </summary>
	public string? OutputPath { get; init; }

	/// <summary>
	///     Current status of the download
	/// </summary>
	public DownloadItemStatus Status { get; init; }

	/// <summary>
	///     Client-provided status message, e.g. an error detail
	/// </summary>
	public string? Message { get; init; }

	/// <summary>
	///     True when the download does not belong to this app (category mismatch), so it must not be
	///     removed, imported or moved by Submarine
	/// </summary>
	public bool IsReadOnly { get; init; }

	/// <summary>
	///     Whether the download may be removed from the client
	/// </summary>
	public bool CanBeRemoved { get; init; }

	/// <summary>
	///     Whether files may be moved or deleted after import, i.e. the client no longer needs them
	/// </summary>
	public bool CanMoveFiles { get; init; }

	/// <summary>
	///     Seed ratio limit configured on the download, if any
	/// </summary>
	public double? SeedRatio { get; init; }

	/// <summary>
	///     Seed time limit configured on the download, if any
	/// </summary>
	public TimeSpan? SeedTime { get; init; }

	/// <summary>
	///     Protocol the download uses
	/// </summary>
	public Provider.Protocol Protocol { get; init; }

	/// <summary>
	///     Submarine id of the download client that reported this item
	/// </summary>
	public int DownloadClientId { get; init; }

	/// <summary>
	///     Name of the download client that reported this item
	/// </summary>
	public string? DownloadClientName { get; init; }
}
