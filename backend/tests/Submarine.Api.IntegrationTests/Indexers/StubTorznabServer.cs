using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Submarine.Api.IntegrationTests.Indexers;

/// <summary>
///     A minimal Torznab server hosted on a random port, used to exercise indexer create/test/search/download against
///     a real HTTP endpoint instead of mocking <c>IIndexer</c>.
/// </summary>
public sealed class StubTorznabServer : IAsyncDisposable
{
	private const string Caps = """
		<?xml version="1.0" encoding="UTF-8"?>
		<caps>
		  <searching>
		    <search available="yes" supportedParams="q"/>
		    <tv-search available="yes" supportedParams="q,season,ep,tvdbid"/>
		    <movie-search available="yes" supportedParams="q,imdbid"/>
		  </searching>
		  <categories>
		    <category id="5000" name="TV"><subcat id="5040" name="TV/HD"/></category>
		  </categories>
		</caps>
		""";

	private const string SearchTemplate = """
		<?xml version="1.0" encoding="UTF-8"?>
		<rss version="2.0" xmlns:torznab="http://torznab.com/schemas/2015/feed">
		<channel>
		<item>
		  <title>{0}</title>
		  <guid>stub-guid-1</guid>
		  <link>http://stub/details/1</link>
		  <pubDate>Mon, 01 Jan 2024 00:00:00 +0000</pubDate>
		  <size>1500000000</size>
		  <enclosure url="{1}/download/1" length="1500000000" type="application/x-bittorrent"/>
		  <torznab:attr name="seeders" value="50"/>
		  <torznab:attr name="peers" value="60"/>
		  <torznab:attr name="category" value="5040"/>
		</item>
		</channel>
		</rss>
		""";

	private readonly WebApplication _app;

	/// <summary>Release title returned by every search response.</summary>
	public string ReleaseTitle { get; set; } = "Some.Show.S01E01.1080p.WEB-DL.x264-GROUP";

	/// <summary>Base url of the running server, e.g. http://127.0.0.1:54321.</summary>
	public string BaseUrl { get; private set; } = string.Empty;

	private StubTorznabServer(WebApplication app) => _app = app;

	/// <summary>Starts the stub server on a random loopback port.</summary>
	public static async Task<StubTorznabServer> StartAsync()
	{
		var builder = WebApplication.CreateBuilder();
		builder.Logging.ClearProviders();
		builder.WebHost.UseUrls("http://127.0.0.1:0");
		var app = builder.Build();
		var stub = new StubTorznabServer(app);

		app.MapGet("/api", (HttpContext context) =>
		{
			var type = context.Request.Query["t"].ToString();
			var body = type == "caps"
				? Caps
				: string.Format(SearchTemplate, stub.ReleaseTitle, stub.BaseUrl);
			return Results.Text(body, "application/rss+xml");
		});

		app.MapGet("/download/1", () => Results.Bytes("FAKE_TORRENT_BYTES"u8.ToArray(), "application/x-bittorrent"));

		await app.StartAsync();
		stub.BaseUrl = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
			.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!
			.Addresses.First();

		return stub;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}
