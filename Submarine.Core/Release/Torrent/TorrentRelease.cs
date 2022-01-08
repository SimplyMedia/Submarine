namespace Submarine.Core.Release.Torrent
{
	public record TorrentRelease : BaseRelease
	{
		public TorrentReleaseFlags Flags { get; init; }
		
		public TorrentRelease(BaseRelease baseRelease) : base(baseRelease)
		{
			Protocol = Protocols.BITTORRENT;
		}
	}
}
