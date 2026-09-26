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

	/// <summary>
	///     Whether the release is recent: for series, any grabbed episode aired within the last 14 days;
	///     for movies, the physical or digital release was within the last 21 days, or the cinema release
	///     within the last 120 days. Clients that support it prioritize recent releases differently from
	///     older ones
	/// </summary>
	public bool IsRecentRelease { get; init; }

	/// <summary>Release group parsed from the title, if any</summary>
	public string? ReleaseGroup { get; init; }

	/// <summary>Formatted quality (source and resolution), if known</summary>
	public string? Quality { get; init; }

	/// <summary>Languages parsed from the title</summary>
	public IReadOnlyList<string> Languages { get; init; } = [];

	/// <summary>Name of the indexer the release came from, if known</summary>
	public string? Indexer { get; init; }

	/// <summary>First air year (series) or release year (movie), if known</summary>
	public int? Year { get; init; }

	/// <summary>Broadcast network of the series, if known; movies have none</summary>
	public string? Network { get; init; }
}
