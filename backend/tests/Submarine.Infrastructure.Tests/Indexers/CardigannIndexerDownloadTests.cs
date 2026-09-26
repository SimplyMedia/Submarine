using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.Indexers.Cardigann;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Indexers;

public class CardigannIndexerDownloadTests
{
	private const string DetailsPage = """
		<html><body>
		<ul><li><a href="http://itorrents.org/torrent/AAA.torrent">torrent</a></li>
		<li><a href="magnet:?xt=urn:btih:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa&amp;dn=alpha">magnet</a></li></ul>
		</body></html>
		""";

	private const string InfoHashPage = """
		<html><body>
		<a href="/hash/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa">hash</a>
		<input name="q" value="alpha release"/>
		</body></html>
		""";

	[Fact]
	public async Task DownloadAsync_ShouldResolveLinkFromSelectors_WhenDownloadBlockPresent()
	{
		const string definitionYaml = """
			---
			id: testdownload
			name: TestDownload
			language: en-US
			type: public
			encoding: UTF-8
			links:
			  - https://dl.example/
			caps:
			  modes:
			    search: [q]
			settings: []
			download:
			  selectors:
			    - selector: ul li a[href*="itorrents"]
			      attribute: href
			      filters:
			        - name: replace
			          args: ["http://itorrents.org/", "https://itorrents.net/"]
			    - selector: ul li a[href*="magnet:"]
			      attribute: href
			search:
			  paths:
			    - path: browse
			  rows:
			    selector: tr
			  fields:
			    title:
			      selector: td.title
			""";

		var http = new FakeIndexerHttpClient(request => request.PathAndQuery switch
		{
			"/details/1" => FakeResponses.Html(DetailsPage),
			_ => FakeResponses.Text("torrent-bytes", "application/x-bittorrent")
		});
		var definition = CardigannDefinitionParser.Parse(definitionYaml);
		await using var indexer = new CardigannIndexer(definition, new CardigannSettings("testdownload"), http, new NullLogger<CardigannIndexer>());

		var response = await indexer.Download(new Uri("https://dl.example/details/1"));

		http.Requests.Count.ShouldBe(2);
		http.Requests[0].Uri.ToString().ShouldBe("https://dl.example/details/1");
		http.Requests[1].Uri.ToString().ShouldBe("https://itorrents.net/torrent/AAA.torrent");
		(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("torrent-bytes");
	}

	[Fact]
	public async Task DownloadAsync_ShouldFallBackToNextSelector_WhenFirstMisses()
	{
		const string definitionYaml = """
			---
			id: testdownload2
			name: TestDownload2
			language: en-US
			type: public
			encoding: UTF-8
			links:
			  - https://dl.example/
			caps:
			  modes:
			    search: [q]
			settings: []
			download:
			  selectors:
			    - selector: a.missing-link
			      attribute: href
			    - selector: a[href^="magnet:"]
			      attribute: href
			search:
			  paths:
			    - path: browse
			  rows:
			    selector: tr
			  fields:
			    title:
			      selector: td.title
			""";

		var http = new FakeIndexerHttpClient(request => request.PathAndQuery switch
		{
			"/details/1" => FakeResponses.Html(DetailsPage),
			_ => FakeResponses.Text("magnet", "text/uri-list")
		});
		var definition = CardigannDefinitionParser.Parse(definitionYaml);
		await using var indexer = new CardigannIndexer(definition, new CardigannSettings("testdownload2"), http, new NullLogger<CardigannIndexer>());

		var response = await indexer.Download(new Uri("https://dl.example/details/1"));

		http.Requests.ShouldHaveSingleItem().Uri.ToString().ShouldBe("https://dl.example/details/1");
		(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("magnet:?xt=urn:btih:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa&dn=alpha");
	}

	[Fact]
	public async Task DownloadAsync_ShouldBuildMagnetFromInfoHash_WhenInfoHashBlockPresent()
	{
		const string definitionYaml = """
			---
			id: testdownload3
			name: TestDownload3
			language: en-US
			type: public
			encoding: UTF-8
			links:
			  - https://dl.example/
			caps:
			  modes:
			    search: [q]
			settings: []
			download:
			  infohash:
			    hash:
			      selector: a[href^="/hash/"]
			      attribute: href
			      filters:
			        - name: regexp
			          args: ([a-f0-9]{40})
			    title:
			      selector: input[name="q"]
			      attribute: value
			      filters:
			        - name: validfilename
			search:
			  paths:
			    - path: browse
			  rows:
			    selector: tr
			  fields:
			    title:
			      selector: td.title
			""";

		var http = new FakeIndexerHttpClient(request => request.PathAndQuery switch
		{
			"/details/1" => FakeResponses.Html(InfoHashPage),
			_ => throw new InvalidOperationException("unexpected request")
		});
		var definition = CardigannDefinitionParser.Parse(definitionYaml);
		await using var indexer = new CardigannIndexer(definition, new CardigannSettings("testdownload3"), http, new NullLogger<CardigannIndexer>());

		var response = await indexer.Download(new Uri("https://dl.example/details/1"));

		http.Requests.ShouldHaveSingleItem().Uri.ToString().ShouldBe("https://dl.example/details/1");
		(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("magnet:?xt=urn:btih:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa&dn=alpha%20release");
	}

	[Fact]
	public async Task DownloadAsync_ShouldPassThrough_WhenNoDownloadBlock()
	{
		const string definitionYaml = """
			---
			id: testdownload4
			name: TestDownload4
			language: en-US
			type: public
			encoding: UTF-8
			links:
			  - https://dl.example/
			caps:
			  modes:
			    search: [q]
			settings: []
			search:
			  paths:
			    - path: browse
			  rows:
			    selector: tr
			  fields:
			    title:
			      selector: td.title
			""";

		var http = new FakeIndexerHttpClient(request => FakeResponses.Text("direct", "application/x-bittorrent"));
		var definition = CardigannDefinitionParser.Parse(definitionYaml);
		await using var indexer = new CardigannIndexer(definition, new CardigannSettings("testdownload4"), http, new NullLogger<CardigannIndexer>());

		var response = await indexer.Download(new Uri("https://other.example/get/1"));

		http.Requests.ShouldHaveSingleItem().Uri.ToString().ShouldBe("https://other.example/get/1");
		(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("direct");
	}

	[Fact]
	public async Task DownloadAsync_ShouldThrow_WhenLinkIsMagnet()
	{
		const string definitionYaml = """
			---
			id: testdownload5
			name: TestDownload5
			language: en-US
			type: public
			encoding: UTF-8
			links:
			  - https://dl.example/
			caps:
			  modes:
			    search: [q]
			settings: []
			search:
			  paths:
			    - path: browse
			  rows:
			    selector: tr
			  fields:
			    title:
			      selector: td.title
			""";

		var http = new FakeIndexerHttpClient(_ => FakeResponses.NotFound());
		var definition = CardigannDefinitionParser.Parse(definitionYaml);
		await using var indexer = new CardigannIndexer(definition, new CardigannSettings("testdownload5"), http, new NullLogger<CardigannIndexer>());

		await Should.ThrowAsync<IndexerException>(async () =>
			await indexer.Download(new Uri("magnet:?xt=urn:btih:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")));
	}

	[Fact]
	public async Task DownloadAsync_ShouldThrow_WhenNoLinkFound()
	{
		const string definitionYaml = """
			---
			id: testdownload6
			name: TestDownload6
			language: en-US
			type: public
			encoding: UTF-8
			links:
			  - https://dl.example/
			caps:
			  modes:
			    search: [q]
			settings: []
			download:
			  selectors:
			    - selector: a.nothing
			      attribute: href
			search:
			  paths:
			    - path: browse
			  rows:
			    selector: tr
			  fields:
			    title:
			      selector: td.title
			""";

		var http = new FakeIndexerHttpClient(_ => FakeResponses.Html("<html><body>empty</body></html>"));
		var definition = CardigannDefinitionParser.Parse(definitionYaml);
		await using var indexer = new CardigannIndexer(definition, new CardigannSettings("testdownload6"), http, new NullLogger<CardigannIndexer>());

		await Should.ThrowAsync<IndexerException>(async () =>
			await indexer.Download(new Uri("https://dl.example/details/1")));
	}
}
