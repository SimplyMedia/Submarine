namespace Submarine.Core.Download;

/// <summary>
///     Snapshot of a download tracked by a download client
/// </summary>
public record DownloadClientItem
{
	/// <summary>
	///     Client-side identifier of the download (e.g. torrent hash or nzo id)
	/// </summary>
	public required string DownloadId { get; init; }

	/// <summary>
	///     Title of the download
	/// </summary>
	public required string Title { get; init; }

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
	///     Current status of the download
	/// </summary>
	public DownloadItemStatus Status { get; init; }

	/// <summary>
	///     Path the client downloads into, once known
	/// </summary>
	public string? OutputPath { get; init; }

	/// <summary>
	///     Category the download was added under
	/// </summary>
	public string? Category { get; init; }

	/// <summary>
	///     Client-provided status message (e.g. error detail)
	/// </summary>
	public string? Message { get; init; }
}
