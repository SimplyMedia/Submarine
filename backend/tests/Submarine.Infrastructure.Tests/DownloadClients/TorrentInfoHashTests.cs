using Shouldly;
using Xunit;
using System.Text;
using Submarine.Core.Download;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class TorrentInfoHashTests
{
	[Fact]
	public void Compute_ShouldHashInfoDictionary_UpperCase()
	{
		TorrentInfoHash.Compute(TestTorrent.Bytes).ShouldBe(TestTorrent.InfoHash);
	}

	[Fact]
	public void Compute_ShouldThrow_WhenInfoDictionaryMissing()
	{
		var bytes = Encoding.ASCII.GetBytes("d8:announce11:http://a.coe");

		var exception = Should.Throw<DownloadClientException>(() => TorrentInfoHash.Compute(bytes));

		exception.Message.ShouldContain("no info dictionary");
	}

	[Fact]
	public void Compute_ShouldThrow_WhenNotBencode()
	{
		var exception = Should.Throw<DownloadClientException>(() =>
			TorrentInfoHash.Compute([0x00, 0x01, 0x02]));

		exception.Message.ShouldContain("bencode");
	}

	[Fact]
	public void FromMagnet_ShouldParseHexHash()
	{
		TorrentInfoHash.FromMagnet("magnet:?xt=urn:btih:0123456789abcdef0123456789abcdef01234567&dn=x")
			.ShouldBe("0123456789ABCDEF0123456789ABCDEF01234567");
	}

	[Fact]
	public void FromMagnet_ShouldDecodeBase32Hash()
	{
		TorrentInfoHash.FromMagnet("magnet:?dn=x&xt=urn:btih:AERUKZ4JVPG66AJDIVTYTK6N54ASGRLH")
			.ShouldBe("0123456789ABCDEF0123456789ABCDEF01234567");
	}

	[Fact]
	public void FromMagnet_ShouldReturnNull_WhenNoBtih()
	{
		TorrentInfoHash.FromMagnet("magnet:?dn=x&tr=http://tracker").ShouldBeNull();
	}

	[Fact]
	public void FromMagnet_ShouldReturnNull_WhenHashMalformed()
	{
		TorrentInfoHash.FromMagnet("magnet:?xt=urn:btih:zzzz").ShouldBeNull();
	}

	[Fact]
	public void FromMagnet_ShouldReturnNull_WhenNotAMagnet()
	{
		TorrentInfoHash.FromMagnet("http://example.com/torrent").ShouldBeNull();
	}
}
