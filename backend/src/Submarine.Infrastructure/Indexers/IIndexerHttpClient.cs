using System;
using System.Collections.Generic;

namespace Submarine.Infrastructure.Indexers;

/// <summary>
///     Proxy settings of an indexer
/// </summary>
/// <param name="Type">The proxy kind</param>
/// <param name="Host">The proxy host</param>
/// <param name="Port">The proxy port</param>
/// <param name="Username">The proxy user, if any</param>
/// <param name="Password">The proxy password, if any</param>
/// <param name="RequestTimeoutSeconds">The FlareSolverr timeout, FlareSolverr only</param>
public record IndexerProxySettings(
	IndexerProxyType Type,
	string Host,
	int Port,
	string? Username = null,
	string? Password = null,
	int RequestTimeoutSeconds = 60);

/// <summary>
///     The supported proxy kinds
/// </summary>
public enum IndexerProxyType
{
	/// <summary>
	///     Plain http proxy
	/// </summary>
	HTTP,

	/// <summary>
	///     SOCKS4 proxy
	/// </summary>
	SOCKS4,

	/// <summary>
	///     SOCKS5 proxy
	/// </summary>
	SOCKS5,

	/// <summary>
	///     FlareSolverr cloudflare bypass service
	/// </summary>
	FLARESOLVERR
}

/// <summary>
///     Http access for one indexer: rate limited, cookie keeping and proxied
/// </summary>
public interface IIndexerHttpClient : IDisposable
{
	/// <summary>
	///     The cookie container shared by every request of this client
	/// </summary>
	System.Net.CookieContainer Cookies { get; }

	/// <summary>
	///     Sends a request, rate limited per indexer
	/// </summary>
	/// <param name="request">The request to send, absolute uri required</param>
	/// <param name="headers">Extra headers merged into the request</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The response</returns>
	Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Performs a GET request
	/// </summary>
	/// <param name="uri">The target uri</param>
	/// <param name="headers">Extra headers merged into the request</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The response</returns>
	Task<HttpResponseMessage> GetAsync(
		Uri uri,
		IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Performs a POST request with form url encoded fields
	/// </summary>
	/// <param name="uri">The target uri</param>
	/// <param name="fields">The form fields</param>
	/// <param name="headers">Extra headers merged into the request</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The response</returns>
	Task<HttpResponseMessage> PostFormAsync(
		Uri uri,
		IReadOnlyDictionary<string, string> fields,
		IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null,
		CancellationToken cancellationToken = default);
}
