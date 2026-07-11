using Submarine.Core.Provider;

namespace Submarine.Core.Release.Torrent;

/// <summary>
///     A Torrent Release
/// </summary>
public record TorrentRelease : BaseRelease
{
	/// <summary>
	///     Flags for this Torrent Release
	/// </summary>
	public TorrentReleaseFlags Flags { get; init; } = TorrentReleaseFlags.NONE;

	/// <summary>
	///     Hash of the Torrent, if any
	/// </summary>
	public string? Hash { get; init; }

	/// <summary>
	///     Creates a new instance of <see cref="TorrentRelease" /> from a <see cref="BaseRelease" />
	/// </summary>
	/// <param name="baseRelease">The base release</param>
	public TorrentRelease(BaseRelease baseRelease) : base(baseRelease)
		=> Protocol = Protocol.BITTORRENT;
}
