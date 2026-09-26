using System;
using System.Linq;
using Submarine.Core.Indexers;
using Submarine.Core.Provider;
using Submarine.Infrastructure.Indexers.Torznab;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Indexers;

public class TorznabFeedParserTests
{
	private const string TorznabFeedXml = """
		<?xml version="1.0" encoding="UTF-8"?>
		<rss version="2.0" xmlns:torznab="http://torznab.com/schemas/2015/feed">
			<channel>
				<item>
					<title>The Expanse S05E10 2160p WEB-DL</title>
					<guid>https://indexer.example/details/1</guid>
					<link>https://indexer.example/download/1.torrent</link>
					<comments>https://indexer.example/details/1#comments</comments>
					<pubDate>Wed, 10 Feb 2027 18:30:00 +0000</pubDate>
					<enclosure url="https://indexer.example/download/1.torrent" length="34359738368" type="application/x-bittorrent"/>
					<torznab:attr name="seeders" value="133"/>
					<torznab:attr name="peers" value="144"/>
					<torznab:attr name="size" value="34359738368"/>
					<torznab:attr name="downloadvolumefactor" value="0"/>
					<torznab:attr name="uploadvolumefactor" value="2"/>
					<torznab:attr name="minimumratio" value="1.1"/>
					<torznab:attr name="minimumseedtime" value="172800"/>
					<torznab:attr name="tvdbid" value="280619"/>
					<torznab:attr name="tmdbid" value="76600"/>
					<torznab:attr name="imdbid" value="tt3230854"/>
					<torznab:attr name="magneturl" value="magnet:?xt=urn:btih:0123456789012345678901234567890123456789"/>
					<torznab:attr name="infohash" value="ABCDEF0123456789ABCDEF0123456789ABCDEF01"/>
					<torznab:attr name="category" value="5000"/>
					<torznab:attr name="category" value="5045"/>
				</item>
				<item>
					<title>No Size Attr Release</title>
					<guid>https://indexer.example/details/2</guid>
					<link>https://indexer.example/download/2.torrent</link>
					<enclosure url="https://indexer.example/download/2.torrent" length="2147483648" type="application/x-bittorrent"/>
				</item>
				<item>
					<title>Halfleech Release</title>
					<guid>https://indexer.example/details/3</guid>
					<link>https://indexer.example/download/3.torrent</link>
					<enclosure url="https://indexer.example/download/3.torrent" length="1073741824" type="application/x-bittorrent"/>
					<torznab:attr name="downloadvolumefactor" value="0.5"/>
				</item>
			</channel>
		</rss>
		""";

	private const string NewznabFeedXml = """
		<?xml version="1.0" encoding="UTF-8"?>
		<rss version="2.0" xmlns:newznab="https://www.newznab.com/DTD/2010/feeds/attributes/">
			<channel>
				<item>
					<title>Ubuntu 22.04 LTS</title>
					<guid>https://indexer.example/details/10</guid>
					<link>https://indexer.example/api?t=get&amp;id=abc</link>
					<pubDate>Tue, 01 Mar 2027 12:00:00 +0100</pubDate>
					<enclosure url="https://indexer.example/api?t=get&amp;id=abc" length="5242880000" type="application/x-nzb"/>
					<newznab:attr name="size" value="5242880000"/>
					<newznab:attr name="grabs" value="42"/>
					<newznab:attr name="files" value="12"/>
					<newznab:attr name="category" value="4000"/>
					<newznab:attr name="imdb" value="0133093"/>
				</item>
				<item>
					<title>Broken Item Missing Guid</title>
				</item>
				<item>
					<title>Another Good One</title>
					<guid>https://indexer.example/details/12</guid>
					<link>https://indexer.example/api?t=get&amp;id=ghi</link>
				</item>
			</channel>
		</rss>
		""";

	[Fact]
	public void Parse_ShouldParseTorrentItem_WhenFeedIsTorznab()
	{
		var releases = TorznabFeedParser.Parse(TorznabFeedXml, Protocol.BITTORRENT);
		var release = releases[0];

		release.Title.ShouldBe("The Expanse S05E10 2160p WEB-DL");
		release.Guid.ShouldBe("https://indexer.example/details/1");
		release.DownloadUrl.ShouldBe("https://indexer.example/download/1.torrent");
		release.InfoUrl.ShouldBe("https://indexer.example/details/1#comments");
		release.MagnetUrl.ShouldBe("magnet:?xt=urn:btih:0123456789012345678901234567890123456789");
		release.InfoHash.ShouldBe("abcdef0123456789abcdef0123456789abcdef01");
		release.Protocol.ShouldBe(Protocol.BITTORRENT);
		release.PublishDate.ShouldNotBeNull().ShouldBe(new DateTime(2027, 2, 10, 18, 30, 0, DateTimeKind.Utc));
	}

	[Fact]
	public void Parse_ShouldParseSeedersAndPeers_WhenTorznabAttrsPresent()
	{
		var release = TorznabFeedParser.Parse(TorznabFeedXml, Protocol.BITTORRENT)[0];
		release.Seeders.ShouldBe(133);
		release.Peers.ShouldBe(144);
		release.Leechers.ShouldBe(11);
	}

	[Fact]
	public void Parse_ShouldPreferSizeAttr_WhenBothAttrAndEnclosureLengthPresent()
		=> TorznabFeedParser.Parse(TorznabFeedXml, Protocol.BITTORRENT)[0].Size.ShouldBe(34359738368);

	[Fact]
	public void Parse_ShouldUseEnclosureLength_WhenSizeAttrMissing()
		=> TorznabFeedParser.Parse(TorznabFeedXml, Protocol.BITTORRENT)[1].Size.ShouldBe(2147483648);

	[Fact]
	public void Parse_ShouldParseFlagsAndLimits_WhenVolumeFactorsPresent()
	{
		var release = TorznabFeedParser.Parse(TorznabFeedXml, Protocol.BITTORRENT)[0];
		release.DownloadVolumeFactor.ShouldBe(0);
		release.UploadVolumeFactor.ShouldBe(2);
		release.IndexerFlags.ShouldBe([IndexerFlag.FREELEECH, IndexerFlag.DOUBLE_UPLOAD]);
		TorznabFeedParser.Parse(TorznabFeedXml, Protocol.BITTORRENT)[2].IndexerFlags.ShouldBe([IndexerFlag.HALFLEECH]);
		release.MinimumRatio.ShouldBe(1.1);
		release.MinimumSeedTime.ShouldBe(172800);
	}

	[Fact]
	public void Parse_ShouldParseIdsAndCategories_WhenPresent()
	{
		var release = TorznabFeedParser.Parse(TorznabFeedXml, Protocol.BITTORRENT)[0];
		release.Categories.ShouldBe([5000, 5045]);
		release.TvdbId.ShouldBe(280619);
		release.ImdbId.ShouldBe("tt3230854");
		release.TmdbId.ShouldBe(76600);
	}

	[Fact]
	public void Parse_ShouldParseUsenetItem_WhenFeedIsNewznab()
	{
		var releases = TorznabFeedParser.Parse(NewznabFeedXml, Protocol.USENET);
		var release = releases[0];

		release.Protocol.ShouldBe(Protocol.USENET);
		release.Size.ShouldBe(5242880000);
		release.Grabs.ShouldBe(42);
		release.Files.ShouldBe(12);
		release.Categories.ShouldBe([4000]);
		release.ImdbId.ShouldBe("tt0133093");
		release.Seeders.ShouldBeNull();
		release.PublishDate.ShouldNotBeNull().ShouldBe(new DateTime(2027, 3, 1, 11, 0, 0, DateTimeKind.Utc));
	}

	[Fact]
	public void Parse_ShouldSkipItem_WhenTitleOrGuidMissing()
		=> TorznabFeedParser.Parse(NewznabFeedXml, Protocol.USENET).Count.ShouldBe(2);

	[Fact]
	public void Parse_ShouldLeaveOptionalFieldsNull_WhenMissing()
	{
		var release = TorznabFeedParser.Parse(TorznabFeedXml, Protocol.BITTORRENT)[1];
		release.Seeders.ShouldBeNull();
		release.Peers.ShouldBeNull();
		release.Leechers.ShouldBeNull();
		release.Size.ShouldBe(2147483648);
		release.Categories.ShouldBeEmpty();
		release.TvdbId.ShouldBeNull();
		release.IndexerFlags.ShouldBeEmpty();
		release.PublishDate.ShouldBeNull();
	}

	[Fact]
	public void Parse_ShouldThrowAuthException_WhenErrorCodeReportsBadCredentials()
	{
		const string xml = """
			<error code="100" description="Incorrect user credentials"/>
			""";

		Should.Throw<IndexerAuthException>(() => TorznabFeedParser.Parse(xml, Protocol.USENET))
			.Message.ShouldBe("Incorrect user credentials");
	}

	[Fact]
	public void Parse_ShouldThrowIndexerException_WhenErrorCodeIsOther()
	{
		const string xml = """
			<error code="200" description="Missing parameter"/>
			""";

		Should.Throw<IndexerException>(() => TorznabFeedParser.Parse(xml, Protocol.USENET))
			.Message.ShouldContain("code 200");
	}

	[Fact]
	public void Parse_ShouldNotContainErrorItems_WhenFeedHasNoError()
		=> TorznabFeedParser.Parse(TorznabFeedXml, Protocol.BITTORRENT).ShouldNotContain(release => release.Title.Contains("error"));
}
