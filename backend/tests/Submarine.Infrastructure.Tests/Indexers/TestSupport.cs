using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.Indexers;
using Xunit;

namespace Submarine.Infrastructure.Tests.Indexers;

/// <summary>
///     Test friendly wrappers that pass the xunit cancellation token explicitly
/// </summary>
internal static class IndexerTestExtensions
{
	public static Task<IReadOnlyList<ReleaseInfo>> Fetch(this IIndexer indexer, SearchRequest request)
		=> indexer.FetchAsync(request, TestContext.Current.CancellationToken);

	public static Task<IReadOnlyList<ReleaseInfo>> FetchRss(this IIndexer indexer)
		=> indexer.FetchRssAsync(TestContext.Current.CancellationToken);

	public static Task<IndexerCapabilities> Caps(this IIndexer indexer)
		=> indexer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

	public static Task<HttpResponseMessage> Download(this IIndexer indexer, Uri link)
		=> indexer.DownloadAsync(link, TestContext.Current.CancellationToken);
}

/// <summary>
///     Canned in-memory http client for indexer tests, records every request
/// </summary>
internal sealed class FakeIndexerHttpClient(Func<FakeIndexerRequest, HttpResponseMessage> respond) : IIndexerHttpClient
{
	private static readonly IReadOnlyDictionary<string, string> EmptyFields = new Dictionary<string, string>();

	public CookieContainer Cookies { get; } = new();

	public List<FakeIndexerRequest> Requests { get; } = [];

	public Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null,
		CancellationToken cancellationToken = default)
		=> Task.FromResult(Send(request, EmptyFields, headers));

	public Task<HttpResponseMessage> GetAsync(
		Uri uri,
		IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null,
		CancellationToken cancellationToken = default)
		=> Task.FromResult(Send(new HttpRequestMessage(HttpMethod.Get, uri), EmptyFields, headers));

	public async Task<HttpResponseMessage> PostFormAsync(
		Uri uri,
		IReadOnlyDictionary<string, string> fields,
		IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null,
		CancellationToken cancellationToken = default)
		=> Send(new HttpRequestMessage(HttpMethod.Post, uri), fields, headers);

	private HttpResponseMessage Send(
		HttpRequestMessage request,
		IReadOnlyDictionary<string, string> fields,
		IReadOnlyDictionary<string, IReadOnlyList<string>>? headers)
	{
		var recorded = new FakeIndexerRequest(request.RequestUri!, request.Method, fields, headers);
		Requests.Add(recorded);
		return respond(recorded);
	}

	public void Dispose()
	{
	}
}

internal sealed record FakeIndexerRequest(
	Uri Uri,
	HttpMethod Method,
	IReadOnlyDictionary<string, string> Fields,
	IReadOnlyDictionary<string, IReadOnlyList<string>>? Headers)
{
	public string PathAndQuery => Uri.PathAndQuery;
}

internal static class FakeResponses
{
	public static HttpResponseMessage Html(string body, HttpStatusCode status = HttpStatusCode.OK)
		=> Text(body, "text/html", status);

	public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
		=> Text(body, "application/json", status);

	public static HttpResponseMessage Xml(string body, HttpStatusCode status = HttpStatusCode.OK)
		=> Text(body, "application/xml", status);

	public static HttpResponseMessage Text(string body, string contentType, HttpStatusCode status = HttpStatusCode.OK)
		=> new(status)
		{
			Content = new StringContent(body, System.Text.Encoding.UTF8, contentType)
		};

	public static HttpResponseMessage NotFound()
		=> new(HttpStatusCode.NotFound)
		{
			Content = new StringContent("")
		};
}

internal sealed class NullLogger<T> : ILogger<T>
{
	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

	public bool IsEnabled(LogLevel logLevel) => false;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> format)
	{
	}
}

internal static class TestJson
{
	public static IReadOnlyDictionary<string, object?> Object(params (string Key, object? Value)[] entries)
		=> entries.ToDictionary(entry => entry.Key, entry => entry.Value);
}
