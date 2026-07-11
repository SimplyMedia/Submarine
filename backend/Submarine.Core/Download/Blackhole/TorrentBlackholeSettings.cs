namespace Submarine.Core.Download.Blackhole;

/// <summary>
///     Folder settings for a <see cref="TorrentBlackholeClient" />
/// </summary>
public record TorrentBlackholeSettings
{
	/// <summary>
	///     Folder new .torrent/.magnet files are dropped into for an external client to pick up
	/// </summary>
	public required string TorrentFolder { get; init; }

	/// <summary>
	///     Folder the external client places completed downloads into
	/// </summary>
	public required string WatchFolder { get; init; }
}
