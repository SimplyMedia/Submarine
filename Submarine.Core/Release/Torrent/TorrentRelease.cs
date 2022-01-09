namespace Submarine.Core.Release.Torrent;

public record TorrentRelease : BaseRelease
{
	public TorrentReleaseFlags Flags { get; init; } = TorrentReleaseFlags.NONE;

	public string? Hash { get; init; }

	public TorrentRelease(BaseRelease baseRelease) : base(baseRelease)
		=> Protocol = Protocols.BITTORRENT;
}
