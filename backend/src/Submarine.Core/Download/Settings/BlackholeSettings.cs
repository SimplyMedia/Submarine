namespace Submarine.Core.Download;

/// <summary>
///     Folder settings for a torrent blackhole: files are dropped into a watched folder for an external
///     torrent client to pick up
/// </summary>
public record TorrentBlackholeSettings : DownloadClientSettings
{
	/// <summary>Folder new .torrent/.magnet files are dropped into</summary>
	public string TorrentFolder { get; init; } = string.Empty;

	/// <summary>Folder the external client places completed downloads into</summary>
	public string WatchFolder { get; init; } = string.Empty;

	/// <summary>Whether magnet uris are saved as .magnet files; when false, magnet releases are rejected</summary>
	public bool SaveMagnetFiles { get; init; }

	/// <summary>File extension used for saved magnet files</summary>
	public string MagnetFileExtension { get; init; } = ".magnet";

	/// <summary>When true, the watch folder is managed elsewhere and items are reported read-only</summary>
	public bool ReadOnly { get; init; }
}

/// <summary>
///     Folder settings for a usenet blackhole: .nzb files are dropped into a watched folder for an
///     external usenet client to pick up
/// </summary>
public record UsenetBlackholeSettings : DownloadClientSettings
{
	/// <summary>Folder new .nzb files are dropped into</summary>
	public string NzbFolder { get; init; } = string.Empty;

	/// <summary>Folder the external client places completed downloads into</summary>
	public string WatchFolder { get; init; } = string.Empty;
}
