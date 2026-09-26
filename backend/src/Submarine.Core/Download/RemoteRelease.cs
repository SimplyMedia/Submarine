namespace Submarine.Core.Download;

/// <summary>
///     Kind of media a remote release was found for
/// </summary>
public enum RemoteReleaseCategory
{
	/// <summary>TV episode or season</summary>
	SERIES,

	/// <summary>Movie</summary>
	MOVIE
}

/// <summary>
///     A release proposed for download by a download client
/// </summary>
public record RemoteRelease
{
	/// <summary>
	///     Release title
	/// </summary>
	public required string Title { get; init; }

	/// <summary>
	///     Download url of the release; may be a magnet uri or an http(s) link to a torrent or nzb file
	/// </summary>
	public string? DownloadUrl { get; init; }

	/// <summary>
	///     Magnet uri of the release, when it is a torrent with a known magnet link
	/// </summary>
	public string? MagnetUrl { get; init; }

	/// <summary>
	///     Info hash reported by the indexer, if known
	/// </summary>
	public string? InfoHash { get; init; }

	/// <summary>
	///     Release size in bytes
	/// </summary>
	public long Size { get; init; }

	/// <summary>
	///     Protocol of the release
	/// </summary>
	public required Provider.Protocol Protocol { get; init; }

	/// <summary>
	///     Whether the release is a full season pack
	/// </summary>
	public bool IsSeasonPack { get; init; }

	/// <summary>
	///     Kind of media the release belongs to
	/// </summary>
	public RemoteReleaseCategory Category { get; init; }

	/// <summary>
	///     Raw .torrent file contents, fetched by the caller when available
	/// </summary>
	public byte[]? TorrentFile { get; init; }

	/// <summary>
	///     Raw .nzb file contents, fetched by the caller when available
	/// </summary>
	public byte[]? NzbFile { get; init; }
}
