using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.Indexers.Cardigann;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Indexers;

public class CardigannIndexerSearchTests
{
	private const string HtmlDefinition = """
		---
		id: testhtml
		name: TestHtml
		description: A test html tracker
		language: en-US
		type: public
		encoding: UTF-8
		links:
		  - https://tracker.example/
		caps:
		  categorymappings:
		    - {id: movies-cat, cat: Movies/HD, desc: hd}
		    - {id: tv-cat, cat: TV/HD, desc: tvhd}
		  modes:
		    search: [q]
		    tv-search: [q, season, ep]
		settings: []
		search:
		  paths:
		    - path: "{{ if .Keywords }}search/{{ .Keywords }}?cat={{ join .Categories \",\" }}{{ else }}browse{{ end }}"
		  inputs:
		    q: "{{ .Keywords }}"
		  rows:
		    selector: table.results > tbody > tr
		  fields:
		    category:
		      selector: td.cat
		    title:
		      selector: td.title a
		    details:
		      selector: td.title a
		      attribute: href
		    size:
		      selector: td.size
		    seeders:
		      selector: td.seeders
		    date:
		      selector: td.date
		      filters:
		        - name: timeago
		""";

	private const string HtmlResults = """
		<html><body>
		<table class="results"><tbody>
		<tr><td class="cat">movies-cat</td><td class="title"><a href="/details/1">Alpha 1080p</a></td><td class="size">1.4 GB</td><td class="seeders">10</td><td class="date">2 hours ago</td></tr>
		<tr><td class="cat">tv-cat</td><td class="title"><a href="/details/2">Beta S01</a></td><td class="size">512 MB</td><td class="seeders">25</td><td class="date">3 days ago</td></tr>
		<tr><td class="cat">unknown-cat</td><td class="title"><a href="/details/3">Gamma</a></td><td class="size">2 GB</td><td class="seeders">1</td><td class="date">now</td></tr>
		</tbody></table>
		</body></html>
		""";

	private const string JsonDefinition = """
		---
		id: testjson
		name: TestJson
		language: en-US
		type: public
		encoding: UTF-8
		links:
		  - https://api.example/
		caps:
		  categorymappings:
		    - {id: 45, cat: Movies/HD, desc: hd}
		    - {id: 44, cat: Movies/HD, desc: fhd}
		    - {id: 46, cat: Movies/UHD, desc: uhd}
		  modes:
		    search: [q]
		settings:
		  - name: apiurl
		    type: text
		    default: api.example
		search:
		  paths:
		    - path: "https://{{ .Config.apiurl }}/list?q={{ .Keywords }}"
		      response:
		        type: json
		  rows:
		    selector: data.movies
		    attribute: torrents
		    multiple: true
		    missingAttributeEqualsNoResults: true
		  fields:
		    category:
		      selector: quality
		      case:
		        "720p": 45
		        "1080p": 44
		        "2160p": 46
		        "*": 45
		    year:
		      selector: ..year
		    title:
		      selector: ..title
		      filters:
		        - name: append
		          args: " ({{ .Result.year }})"
		    slug:
		      selector: ..slug
		    details:
		      text: "{{ .Config.sitelink }}movie/{{ .Result.slug }}"
		    download:
		      selector: url
		    infohash:
		      selector: hash
		    size:
		      selector: size_bytes
		    seeders:
		      selector: seeds
		    date:
		      selector: date_added_unix
		""";

	private const string JsonResults = """
		{"data": {"movies": [
			{"title": "Alpha", "slug": "alpha", "year": 2020, "torrents": [
				{"quality": "1080p", "url": "https://api.example/dl/aaa", "hash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "size_bytes": 2100000000, "seeds": 5, "date_added_unix": 1700000000}
			]},
			{"title": "Empty", "slug": "empty", "year": 2021, "torrents": null}
		]}}
		""";

	private const string XmlDefinition = """
		---
		id: testxml
		name: TestXml
		language: en-US
		type: semi-private
		encoding: UTF-8
		links:
		  - https://feed.example/
		caps:
		  categorymappings:
		    - {id: Anime, cat: TV/Anime, desc: anime}
		  modes:
		    search: [q]
		settings:
		  - name: apikey
		    type: text
		search:
		  paths:
		    - path: "https://feed.example/api/torznab"
		      response:
		        type: xml
		  inputs:
		    apikey: "{{ .Config.apikey }}"
		    t: "{{ if .Keywords }}{{ .Query.Type }}{{ else }}search{{ end }}"
		    q: "{{ .Keywords }}"
		  rows:
		    selector: rss > channel > item
		  fields:
		    category:
		      text: Anime
		    title:
		      selector: title
		    details:
		      selector: link
		    download:
		      selector: enclosure
		      attribute: url
		    infohash:
		      selector: "[name=infohash]"
		      attribute: value
		    date:
		      selector: pubDate
		      filters:
		        - name: dateparse
		          args: "ddd, dd MMM yyyy HH:mm:ss zzz"
		    size:
		      selector: "[name=size]"
		      attribute: value
		""";

	private const string XmlResults = """
		<?xml version="1.0" encoding="UTF-8"?>
		<rss xmlns:torznab="http://torznab.com/schemas/2015/feed" version="1.0">
			<channel>
				<item>
					<title>Anime Release</title>
					<link>https://feed.example/d/1</link>
					<enclosure url="https://feed.example/dl/1.torrent" type="application/x-bittorrent"/>
					<input name="infohash" value="1111111111111111111111111111111111111111"/>
					<input name="size" value="1024"/>
					<pubDate>Sat, 26 Sep 2026 10:00:00 +0000</pubDate>
				</item>
			</channel>
		</rss>
		""";

	[Fact]
	public async Task FetchAsync_ShouldBuildRequestAndMapCategories_WhenHtmlSearched()
	{
		var http = new FakeIndexerHttpClient(_ => FakeResponses.Html(HtmlResults));
		await using var indexer = CreateIndexer(HtmlDefinition, http);

		var releases = await indexer.Fetch(new TvSearchRequest("alpha", Categories: [5000]));

		http.Requests.ShouldHaveSingleItem();
		var request = http.Requests[0];
		request.Uri.ToString().ShouldBe("https://tracker.example/search/alpha?cat=tv-cat&q=alpha");

		releases.Count.ShouldBe(3);
	}

	[Fact]
	public async Task FetchAsync_ShouldMapCategoriesToStandardIds_WhenResultsParsed()
	{
		var http = new FakeIndexerHttpClient(_ => FakeResponses.Html(HtmlResults));
		await using var indexer = CreateIndexer(HtmlDefinition, http);

		var releases = await indexer.Fetch(new BasicSearchRequest("alpha"));

		releases[0].Categories.ShouldBe([2040]);
		releases[0].Title.ShouldBe("Alpha 1080p");
		releases[0].Guid.ShouldBe("https://tracker.example/details/1");
		releases[0].InfoUrl.ShouldBe("https://tracker.example/details/1");
		releases[0].Size.ShouldBe(1503238554);
		releases[0].Seeders.ShouldBe(10);
		releases[0].Protocol.ShouldBe(Submarine.Core.Provider.Protocol.BITTORRENT);

		releases[1].Categories.ShouldBe([5040]);

		// unmapped string categories are dropped
		releases[2].Categories.ShouldBeEmpty();
	}

	[Fact]
	public async Task FetchAsync_ShouldComputePublishDate_WhenTimeagoFilterUsed()
	{
		var http = new FakeIndexerHttpClient(_ => FakeResponses.Html(HtmlResults));
		await using var indexer = CreateIndexer(HtmlDefinition, http);

		var releases = await indexer.Fetch(new BasicSearchRequest("alpha"));
		var expected = DateTime.UtcNow - TimeSpan.FromHours(2);

		releases[0].PublishDate.ShouldNotBeNull();
		(releases[0].PublishDate!.Value - expected).Duration().ShouldBeLessThan(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public async Task FetchAsync_ShouldUseBrowsePath_WhenRssSync()
	{
		var http = new FakeIndexerHttpClient(_ => FakeResponses.Html(HtmlResults));
		await using var indexer = CreateIndexer(HtmlDefinition, http);

		await indexer.FetchRss();

		http.Requests.ShouldHaveSingleItem().Uri.ToString().ShouldBe("https://tracker.example/browse");
	}

	[Fact]
	public async Task FetchAsync_ShouldDeduplicateByGuid_WhenPathsOverlap()
	{
		var http = new FakeIndexerHttpClient(_ => FakeResponses.Html(HtmlResults));
		var definition = CardigannDefinitionParser.Parse(HtmlDefinition);
		var doubled = definition with
		{
			Search = definition.Search with
			{
				Paths =
				[
					.. definition.Search.Paths,
					.. definition.Search.Paths
				]
			}
		};
		await using var indexer = new CardigannIndexer(doubled, new CardigannSettings("testhtml"), http, new NullLogger<CardigannIndexer>());

		var releases = await indexer.Fetch(new BasicSearchRequest("alpha"));
		releases.Count.ShouldBe(3);
	}

	[Fact]
	public async Task FetchAsync_ShouldApplyCaseMapAndParentSelectors_WhenJsonSearched()
	{
		var http = new FakeIndexerHttpClient(_ => FakeResponses.Json(JsonResults));
		await using var indexer = CreateIndexer(JsonDefinition, http);

		var releases = await indexer.Fetch(new BasicSearchRequest("alpha"));

		releases.ShouldHaveSingleItem();
		var release = releases[0];
		release.Title.ShouldBe("Alpha (2020)");
		release.Categories.ShouldBe([2040]);
		release.Guid.ShouldBe("https://api.example/movie/alpha");
		release.DownloadUrl.ShouldBe("https://api.example/dl/aaa");
		release.InfoHash.ShouldBe("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
		release.MagnetUrl.ShouldBe("magnet:?xt=urn:btih:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa&dn=Alpha%20%282020%29");
		release.Size.ShouldBe(2100000000);
		release.Seeders.ShouldBe(5);
		release.PublishDate.ShouldNotBeNull().ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime);
	}

	[Fact]
	public async Task FetchAsync_ShouldSkipRowsWithoutAttribute_WhenMissingAttributeEqualsNoResults()
	{
		var http = new FakeIndexerHttpClient(_ => FakeResponses.Json(JsonResults));
		await using var indexer = CreateIndexer(JsonDefinition, http);

		var releases = await indexer.Fetch(new BasicSearchRequest("alpha"));
		releases.Count.ShouldBe(1);
	}

	[Fact]
	public async Task FetchAsync_ShouldParseXmlAttributeSelectors_WhenXmlSearched()
	{
		var http = new FakeIndexerHttpClient(_ => FakeResponses.Xml(XmlResults));
		await using var indexer = CreateIndexer(XmlDefinition, http, fields: new System.Collections.Generic.Dictionary<string, string> { ["apikey"] = "secret" });

		var releases = await indexer.Fetch(new BasicSearchRequest("anime"));

		http.Requests.ShouldHaveSingleItem().Uri.Query.ShouldBe("?apikey=secret&t=search&q=anime");
		var release = releases.ShouldHaveSingleItem();
		release.Title.ShouldBe("Anime Release");
		release.Categories.ShouldBe([5070]);
		release.DownloadUrl.ShouldBe("https://feed.example/dl/1.torrent");
		release.InfoHash.ShouldBe("1111111111111111111111111111111111111111");
		release.Size.ShouldBe(1024);
		release.PublishDate.ShouldNotBeNull().ShouldBe(new DateTime(2026, 9, 26, 10, 0, 0, DateTimeKind.Utc));
	}

	[Fact]
	public async Task GetCapabilitiesAsync_ShouldProjectModesAndCategories()
	{
		var http = new FakeIndexerHttpClient(_ => FakeResponses.Html(""));
		await using var indexer = CreateIndexer(HtmlDefinition, http);

		var capabilities = await indexer.Caps();

		capabilities.SearchAvailable.ShouldBeTrue();
		capabilities.TvSearchAvailable.ShouldBeTrue();
		capabilities.TvSearchParams.HasFlag(SearchParams.Season).ShouldBeTrue();
		capabilities.MovieSearchAvailable.ShouldBeFalse();
		capabilities.Categories.Select(category => category.Id).ShouldBe([2040, 5040]);
	}

	[Fact]
	public async Task Protocol_ShouldFollowDefinition()
	{
		var http = new FakeIndexerHttpClient(_ => FakeResponses.Html(""));
		await using var indexer = CreateIndexer(HtmlDefinition, http);
		indexer.Protocol.ShouldBe(Submarine.Core.Provider.Protocol.BITTORRENT);
		indexer.Name.ShouldBe("TestHtml");
	}

	private static CardigannIndexer CreateIndexer(
		string yaml,
		FakeIndexerHttpClient http,
		System.Collections.Generic.Dictionary<string, string>? fields = null)
	{
		var definition = CardigannDefinitionParser.Parse(yaml);
		return new CardigannIndexer(definition, new CardigannSettings(definition.Id, Fields: fields), http, new NullLogger<CardigannIndexer>());
	}
}
