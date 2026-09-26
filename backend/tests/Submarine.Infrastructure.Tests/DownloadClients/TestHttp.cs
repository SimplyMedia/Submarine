using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.Tests.DownloadClients;

/// <summary>
///     Records requests and answers them via a callback, for asserting request shape and response mapping
/// </summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> responder)
	: HttpMessageHandler
{
	public List<StubRequest> Requests { get; } = [];

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
		Requests.Add(new StubRequest(request.Method, request.RequestUri!.ToString(), body, request.Headers));

		return responder(request, body);
	}

	public static HttpResponseMessage Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
		=> new(statusCode)
		{
			Content = new StringContent(json, Encoding.UTF8, "application/json")
		};

	public static HttpResponseMessage Text(string text, HttpStatusCode statusCode = HttpStatusCode.OK)
		=> new(statusCode)
		{
			Content = new StringContent(text, Encoding.UTF8, "text/plain")
		};

	public static HttpResponseMessage Xml(string xml, HttpStatusCode statusCode = HttpStatusCode.OK)
		=> new(statusCode)
		{
			Content = new StringContent(xml, Encoding.UTF8, "text/xml")
		};
}

internal sealed record StubRequest(HttpMethod Method, string Url, string? Body, HttpRequestHeaders Headers)
{
	public bool HasHeader(string name, string value)
		=> Headers.TryGetValues(name, out var values) && values.Contains(value);
}

/// <summary>
///     Canonical single-file torrent: announce http://a.co, name hello, length 5,
///     piece length 16384, 20 bytes of pieces; info hash 85E28F220D944DE9FAABEF75686A210C93A37933
/// </summary>
internal static class TestTorrent
{
	public const string InfoHash = "85E28F220D944DE9FAABEF75686A210C93A37933";

	public static byte[] Bytes { get; } = Encoding.ASCII.GetBytes(
		"d8:announce11:http://a.co4:info" +
		"d6:lengthi5e4:name5:hello12:piece lengthi16384e6:pieces40:" +
		new string('a', 40) + "ee");

	public static RemoteRelease Release(byte[]? torrentFile = null)
		=> new()
		{
			Title = "Some.Show.S01E01",
			DownloadUrl = "http://indexer/torrents/1",
			InfoHash = InfoHash,
			Size = 5,
			Protocol = Protocol.BITTORRENT,
			Category = RemoteReleaseCategory.SERIES,
			TorrentFile = torrentFile ?? Bytes
		};

	public static RemoteRelease MagnetRelease(string? infoHash = null)
		=> new()
		{
			Title = "Some.Show.S01E01",
			DownloadUrl = "http://indexer/torrents/1",
			MagnetUrl = "magnet:?xt=urn:btih:0123456789ABCDEF0123456789ABCDEF01234567&dn=x",
			InfoHash = infoHash,
			Size = 5,
			Protocol = Protocol.BITTORRENT,
			Category = RemoteReleaseCategory.SERIES
		};
}
