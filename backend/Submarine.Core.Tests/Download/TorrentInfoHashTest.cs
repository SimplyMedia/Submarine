using System.Security.Cryptography;
using System.Text;
using Submarine.Core.Download;
using Xunit;

namespace Submarine.Core.Tests.Download;

public class TorrentInfoHashTest
{
	[Fact]
	public void Compute_ShouldReturnUppercaseSha1OfInfoDictionary_WhenGivenABencodedTorrent()
	{
		const string info = "d6:lengthi12e4:name4:teste";
		var torrent = Encoding.ASCII.GetBytes("d8:announce3:url4:info" + info + "e");
		var expected = Convert.ToHexString(SHA1.HashData(Encoding.ASCII.GetBytes(info)));

		Assert.Equal(expected, TorrentInfoHash.Compute(torrent));
	}

	[Fact]
	public void Compute_ShouldThrow_WhenTorrentHasNoInfoDictionary()
	{
		var torrent = Encoding.ASCII.GetBytes("d8:announce3:urle");

		Assert.Throws<DownloadClientException>(() => TorrentInfoHash.Compute(torrent));
	}
}
