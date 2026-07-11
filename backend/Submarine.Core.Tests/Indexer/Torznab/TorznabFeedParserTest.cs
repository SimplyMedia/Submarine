using System;
using System.Collections.Generic;
using Submarine.Core.Indexer;
using Submarine.Core.Indexer.Torznab;
using Submarine.Core.Parser;
using Submarine.Core.Provider;
using Xunit;

namespace Submarine.Core.Tests.Indexer.Torznab;

public class TorznabFeedParserTest
{
	private const string TorznabFeedXml = """
		<?xml version="1.0" encoding="UTF-8"?>
		<rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom" xmlns:torznab="http://torznab.com/schemas/2015/feed">
			<channel>
				<title>Test Tracker</title>
				<item>
					<title>Movie.Title.2022.2160p.UHD.BluRay.REMUX-GROUP</title>
					<guid>https://tracker.example/details/12345</guid>
					<link>https://tracker.example/download/12345.torrent</link>
					<comments>https://tracker.example/details/12345#comments</comments>
					<pubDate>Fri, 07 Oct 2022 17:22:00 +0000</pubDate>
					<enclosure url="https://tracker.example/dl/12345.torrent" length="1000" type="application/x-bittorrent"/>
					<torznab:attr name="size" value="34359738368"/>
					<torznab:attr name="seeders" value="12"/>
					<torznab:attr name="peers" value="15"/>
					<torznab:attr name="category" value="2000"/>
					<torznab:attr name="category" value="2045"/>
					<torznab:attr name="imdbid" value="tt1630029"/>
					<torznab:attr name="tmdbid" value="76600"/>
					<torznab:attr name="downloadvolumefactor" value="0"/>
					<torznab:attr name="uploadvolumefactor" value="2"/>
				</item>
				<item>
					<title>Series.Title.S01E01.1080p.WEB.H264-GROUP</title>
					<guid>https://tracker.example/details/12346</guid>
					<link>https://tracker.example/download/12346.torrent</link>
					<enclosure url="https://tracker.example/dl/12346.torrent" length="2147483648" type="application/x-bittorrent"/>
					<torznab:attr name="seeders" value="3"/>
					<torznab:attr name="peers" value="10"/>
					<torznab:attr name="tvdbid" value="361753"/>
					<torznab:attr name="downloadvolumefactor" value="0.5"/>
					<torznab:attr name="uploadvolumefactor" value="1"/>
				</item>
			</channel>
		</rss>
		""";

	private const string NewznabFeedXml = """
		<?xml version="1.0" encoding="UTF-8"?>
		<rss version="2.0" xmlns:newznab="http://www.newznab.com/DTD/2010/feeds/attributes/">
			<channel>
				<title>Test Indexer</title>
				<item>
					<title>Movie.Title.2003.1080p.BluRay.x264-GROUP</title>
					<guid>https://indexer.example/details/abcdef</guid>
					<link>https://indexer.example/getnzb/abcdef.nzb</link>
					<comments>https://indexer.example/details/abcdef#comments</comments>
					<pubDate>Sat, 08 Oct 2022 20:14:00 +0000</pubDate>
					<enclosure url="https://indexer.example/getnzb/abcdef.nzb" length="1000" type="application/x-nzb"/>
					<newznab:attr name="category" value="2040"/>
					<newznab:attr name="size" value="5368709120"/>
					<newznab:attr name="imdb" value="0234215"/>
				</item>
				<item>
					<title>Minimal.Release.720p.HDTV-GROUP</title>
					<guid>https://indexer.example/details/ghijkl</guid>
					<link>https://indexer.example/getnzb/ghijkl.nzb</link>
				</item>
				<item>
					<guid>https://indexer.example/details/broken</guid>
				</item>
			</channel>
		</rss>
		""";

	private readonly IParser<IReadOnlyList<ReleaseInfo>> _instance;

	public TorznabFeedParserTest(ITestOutputHelper output)
		=> _instance = new TorznabFeedParser(new XunitLogger<TorznabFeedParser>(output));

	[Fact]
	public void Parse_ShouldParseTorrentItem_WhenFeedIsTorznab()
	{
		var release = _instance.Parse(TorznabFeedXml)[0];

		Assert.Equal("Movie.Title.2022.2160p.UHD.BluRay.REMUX-GROUP", release.Title);
		Assert.Equal("https://tracker.example/details/12345", release.Guid);
		Assert.Equal("https://tracker.example/dl/12345.torrent", release.DownloadUrl);
		Assert.Equal("https://tracker.example/details/12345#comments", release.InfoUrl);
		Assert.Equal(new DateTimeOffset(2022, 10, 7, 17, 22, 0, TimeSpan.Zero), release.PublishDate);
		Assert.Equal(Protocol.BITTORRENT, release.Protocol);
		Assert.Equal(new[] { 2000, 2045 }, release.Categories);
		Assert.Equal("tt1630029", release.ImdbId);
		Assert.Equal(76600, release.TmdbId);
	}

	[Fact]
	public void Parse_ShouldParseSeedersAndPeers_WhenTorznabAttrsPresent()
	{
		var release = _instance.Parse(TorznabFeedXml)[0];

		Assert.Equal(12, release.Seeders);
		Assert.Equal(15, release.Peers);
		Assert.Equal(3, release.Leechers);
	}

	[Fact]
	public void Parse_ShouldPreferSizeAttr_WhenBothAttrAndEnclosureLengthPresent()
		=> Assert.Equal(34359738368, _instance.Parse(TorznabFeedXml)[0].Size);

	[Fact]
	public void Parse_ShouldUseEnclosureLength_WhenSizeAttrMissing()
		=> Assert.Equal(2147483648, _instance.Parse(TorznabFeedXml)[1].Size);

	[Fact]
	public void Parse_ShouldParseFlags_WhenVolumeFactorsPresent()
	{
		var releases = _instance.Parse(TorznabFeedXml);

		Assert.Equal(new[] { IndexerFlag.FREELEECH, IndexerFlag.DOUBLE_UPLOAD }, releases[0].IndexerFlags);
		Assert.Equal(new[] { IndexerFlag.HALFLEECH }, releases[1].IndexerFlags);
	}

	[Fact]
	public void Parse_ShouldParseUsenetItem_WhenFeedIsNewznab()
	{
		var release = _instance.Parse(NewznabFeedXml)[0];

		Assert.Equal(Protocol.USENET, release.Protocol);
		Assert.Equal(5368709120, release.Size);
		Assert.Equal(new[] { 2040 }, release.Categories);
		Assert.Equal("0234215", release.ImdbId);
		Assert.Equal(new DateTimeOffset(2022, 10, 8, 20, 14, 0, TimeSpan.Zero), release.PublishDate);
		Assert.Null(release.Seeders);
	}

	[Fact]
	public void Parse_ShouldLeaveOptionalFieldsNull_WhenMissing()
	{
		var release = _instance.Parse(NewznabFeedXml)[1];

		Assert.Equal("Minimal.Release.720p.HDTV-GROUP", release.Title);
		Assert.Equal("https://indexer.example/getnzb/ghijkl.nzb", release.DownloadUrl);
		Assert.Null(release.InfoUrl);
		Assert.Null(release.Size);
		Assert.Null(release.PublishDate);
		Assert.Null(release.Seeders);
		Assert.Null(release.Leechers);
		Assert.Null(release.Peers);
		Assert.Null(release.ImdbId);
		Assert.Null(release.TvdbId);
		Assert.Null(release.TmdbId);
		Assert.Empty(release.Categories);
		Assert.Empty(release.IndexerFlags);
	}

	[Fact]
	public void Parse_ShouldSkipItem_WhenTitleMissing()
		=> Assert.Equal(2, _instance.Parse(NewznabFeedXml).Count);
}
