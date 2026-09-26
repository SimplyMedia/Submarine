using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Submarine.Core.Indexers;

namespace Submarine.Infrastructure.Indexers;

/// <summary>
///     Creates configured http clients for indexers
/// </summary>
/// <param name="httpClientFactory">The application http client factory, used for stateless clients</param>
/// <param name="logger">The logger</param>
public sealed class IndexerHttpClientFactory(IHttpClientFactory httpClientFactory, ILogger<IndexerHttpClientFactory> logger)
{
	/// <summary>
	///     Creates a client for one indexer instance
	/// </summary>
	/// <param name="indexerId">Stable id used for FlareSolverr session naming, if any</param>
	/// <param name="requestDelaySeconds">Minimum seconds between two requests, null for the default of 2</param>
	/// <param name="proxy">The proxy settings, if any</param>
	/// <param name="userAgent">The user agent override, if any</param>
	/// <param name="timeoutSeconds">The request timeout, defaults to 100 seconds</param>
	/// <returns>The client, dispose with the indexer</returns>
	public IIndexerHttpClient Create(
		string? indexerId = null,
		double? requestDelaySeconds = null,
		IndexerProxySettings? proxy = null,
		string? userAgent = null,
		int timeoutSeconds = 100)
		=> new DefaultIndexerHttpClient(
			indexerId,
			requestDelaySeconds ?? 2,
			proxy,
			userAgent,
			timeoutSeconds,
			logger,
			httpClientFactory);
}

/// <summary>
///     The default implementation: one HttpClient per indexer instance because cookies and
///     rate limit state are per indexer, stateless flows go through IHttpClientFactory
/// </summary>
internal sealed class DefaultIndexerHttpClient : IIndexerHttpClient
{
	private const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36";

	private readonly CookieContainer _cookies = new();
	private readonly HttpClient _client;
	private readonly FlareSolverrClient? _flareSolverr;
	private readonly IndexerRateLimiter _rateLimiter;
	private bool _disposed;

	public DefaultIndexerHttpClient(
		string? indexerId,
		double requestDelaySeconds,
		IndexerProxySettings? proxy,
		string? userAgent,
		int timeoutSeconds,
		ILogger logger,
		IHttpClientFactory httpClientFactory,
		TimeProvider? timeProvider = null)
	{
		_rateLimiter = new IndexerRateLimiter(timeProvider ?? TimeProvider.System, requestDelaySeconds);
		if (proxy is { Type: IndexerProxyType.FLARESOLVERR })
		{
			_flareSolverr = new FlareSolverrClient(
				httpClientFactory.CreateClient("indexer-flaresolverr"),
				$"http://{proxy.Host}:{proxy.Port}",
				indexerId ?? "submarine",
				proxy.RequestTimeoutSeconds,
				_cookies,
				logger);
			_client = CreateHandlerChain(proxy: null, userAgent, timeoutSeconds);
			return;
		}

		_client = CreateHandlerChain(proxy, userAgent, timeoutSeconds);
	}

	public CookieContainer Cookies => _cookies;

	public async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null,
		CancellationToken cancellationToken = default)
	{
		if (_flareSolverr is { } flareSolverr)
			return await flareSolverr.RequestAsync(request, headers, cancellationToken).ConfigureAwait(false);

		await _rateLimiter.WaitForSlotAsync(cancellationToken).ConfigureAwait(false);

		var cloned = await CloneWithCookiesAsync(request, headers, cancellationToken).ConfigureAwait(false);
		return await _client.SendAsync(cloned, cancellationToken).ConfigureAwait(false);
	}

	public Task<HttpResponseMessage> GetAsync(
		Uri uri,
		IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null,
		CancellationToken cancellationToken = default)
		=> SendAsync(new HttpRequestMessage(HttpMethod.Get, uri), headers, cancellationToken);

	public Task<HttpResponseMessage> PostFormAsync(
		Uri uri,
		IReadOnlyDictionary<string, string> fields,
		IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null,
		CancellationToken cancellationToken = default)
	{
		var request = new HttpRequestMessage(HttpMethod.Post, uri)
		{
			Content = new FormUrlEncodedContent(fields)
		};
		return SendAsync(request, headers, cancellationToken);
	}

	private async Task<HttpRequestMessage> CloneWithCookiesAsync(
		HttpRequestMessage request,
		IReadOnlyDictionary<string, IReadOnlyList<string>>? headers,
		CancellationToken cancellationToken)
	{
		var cloned = new HttpRequestMessage(request.Method, request.RequestUri)
		{
			Version = request.Version,
			Content = request.Content
		};

		foreach (var header in request.Headers)
			cloned.Headers.TryAddWithoutValidation(header.Key, header.Value);

		if (headers is { } extra)
		{
			foreach (var (name, values) in extra)
			{
				cloned.Headers.TryAddWithoutValidation(name, values);
			}
		}

		if (!cloned.Headers.Contains("User-Agent"))
			cloned.Headers.TryAddWithoutValidation("User-Agent", DefaultUserAgent);

		var cookieHeader = _cookies.GetCookieHeader(request.RequestUri!);
		if (cookieHeader.Length > 0 && !cloned.Headers.Contains("Cookie"))
			cloned.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

		if (request.Content is { } content)
		{
			var buffer = await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
			var replacement = new ByteArrayContent(buffer);
			foreach (var header in content.Headers)
				replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
			cloned.Content = replacement;
		}

		return cloned;
	}

	private HttpClient CreateHandlerChain(IndexerProxySettings? proxy, string? userAgent, int timeoutSeconds)
	{
		_ = userAgent; // user agent is applied per request so proxied flows keep it too

		var handler = new SocketsHttpHandler
		{
			CookieContainer = _cookies,
			UseCookies = true,
			AllowAutoRedirect = true,
			AutomaticDecompression = System.Net.DecompressionMethods.All,
			ConnectTimeout = TimeSpan.FromSeconds(15)
		};

		if (proxy is { } direct)
		{
			switch (direct.Type)
			{
				case IndexerProxyType.HTTP:
				{
					var webProxy = new WebProxy($"http://{direct.Host}:{direct.Port}");
					if (!string.IsNullOrEmpty(direct.Username))
						webProxy.Credentials = new NetworkCredential(direct.Username, direct.Password);
					handler.Proxy = webProxy;
					break;
				}
				case IndexerProxyType.SOCKS5:
				{
					var socks5 = new Socks5Proxy(direct.Host, direct.Port, direct.Username, direct.Password);
					handler.ConnectCallback = async (context, cancellationToken)
						=> new NetworkStream(await socks5.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false), ownsSocket: true);
					break;
				}
				case IndexerProxyType.SOCKS4:
				{
					var socks4 = new Socks4Proxy(direct.Host, direct.Port, direct.Username);
					handler.ConnectCallback = async (context, cancellationToken)
						=> new NetworkStream(await socks4.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false), ownsSocket: true);
					break;
				}
			}
		}

		return new HttpClient(handler)
		{
			Timeout = TimeSpan.FromSeconds(timeoutSeconds)
		};
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		_client.Dispose();
		_rateLimiter.Dispose();
	}
}

/// <summary>
///     SOCKS4 connect handshake used as a SocketsHttpHandler connect callback
/// </summary>
/// <param name="proxyHost">The proxy host</param>
/// <param name="proxyPort">The proxy port</param>
/// <param name="userId">The optional SOCKS4 userid</param>
internal sealed class Socks4Proxy(string proxyHost, int proxyPort, string? userId)
{
	/// <summary>
	///     Builds the SOCKS4 connect request bytes
	/// </summary>
	/// <param name="host">The target host, resolved to an ip by the caller for socks4a-less proxies</param>
	/// <param name="port">The target port</param>
	/// <param name="userId">The userid, if any</param>
	/// <returns>The request bytes</returns>
	public static byte[] BuildConnectRequest(IPAddress host, int port, string? userId)
	{
		var userBytes = Encoding.ASCII.GetBytes(userId ?? string.Empty);
		var request = new byte[8 + userBytes.Length + 1];
		request[0] = 4; // version
		request[1] = 1; // connect
		request[2] = (byte)(port >> 8);
		request[3] = (byte)port;
		host.GetAddressBytes().CopyTo(request, 4);
		userBytes.CopyTo(request, 8);
		request[^1] = 0; // null terminator
		return request;
	}

	/// <summary>
	///     Connects through the proxy to the target
	/// </summary>
	/// <param name="target">The target endpoint</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The connected socket</returns>
	public async Task<Socket> ConnectAsync(DnsEndPoint target, CancellationToken cancellationToken)
	{
		var addresses = await Dns.GetHostAddressesAsync(target.Host, cancellationToken).ConfigureAwait(false);
		var address = Array.Find(addresses, a => a.AddressFamily == AddressFamily.InterNetwork)
			?? throw new IndexerException($"SOCKS4 does not support IPv6 addresses ({target.Host} resolved to IPv6 only)");
		var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		await socket.ConnectAsync(proxyHost, proxyPort, cancellationToken).ConfigureAwait(false);

		var request = BuildConnectRequest(address, target.Port, userId);
		await socket.SendAsync(request, SocketFlags.None, cancellationToken).ConfigureAwait(false);

		var response = new byte[8];
		var read = 0;
		while (read < response.Length)
		{
			var chunk = await socket.ReceiveAsync(response.AsMemory(read), SocketFlags.None, cancellationToken).ConfigureAwait(false);
			if (chunk == 0)
				throw new IndexerException("SOCKS4 proxy closed the connection during handshake");
			read += chunk;
		}

		if (response[1] != 90)
			throw new IndexerException($"SOCKS4 proxy rejected the connect request with code {response[1]}");

		return socket;
	}
}

/// <summary>
///     SOCKS5 connect handshake (RFC 1928) with optional username/password authentication
///     (RFC 1929), used as a SocketsHttpHandler connect callback. The target host is sent to
///     the proxy as a domain name so the proxy performs DNS resolution.
/// </summary>
/// <param name="proxyHost">The proxy host</param>
/// <param name="proxyPort">The proxy port</param>
/// <param name="username">The optional SOCKS5 username</param>
/// <param name="password">The optional SOCKS5 password</param>
internal sealed class Socks5Proxy(string proxyHost, int proxyPort, string? username, string? password)
{
	/// <summary>
	///     Connects through the proxy to the target
	/// </summary>
	/// <param name="target">The target endpoint</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The connected socket</returns>
	public async Task<Socket> ConnectAsync(DnsEndPoint target, CancellationToken cancellationToken)
	{
		var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		await socket.ConnectAsync(proxyHost, proxyPort, cancellationToken).ConfigureAwait(false);

		var hasCredentials = !string.IsNullOrEmpty(username);
		var greeting = hasCredentials ? new byte[] { 5, 2, 0, 2 } : new byte[] { 5, 1, 0 };
		await socket.SendAsync(greeting, SocketFlags.None, cancellationToken).ConfigureAwait(false);

		var chosenMethod = await ReceiveExactAsync(socket, 2, cancellationToken).ConfigureAwait(false);
		if (chosenMethod[0] != 5 || chosenMethod[1] == 0xFF)
			throw new IndexerException("SOCKS5 proxy rejected all authentication methods");

		if (chosenMethod[1] == 2)
		{
			if (!hasCredentials)
				throw new IndexerException("SOCKS5 proxy requires username/password authentication");
			await AuthenticateAsync(socket, cancellationToken).ConfigureAwait(false);
		}

		await ConnectTargetAsync(socket, target, cancellationToken).ConfigureAwait(false);
		return socket;
	}

	private async Task AuthenticateAsync(Socket socket, CancellationToken cancellationToken)
	{
		var userBytes = Encoding.UTF8.GetBytes(username ?? string.Empty);
		var passBytes = Encoding.UTF8.GetBytes(password ?? string.Empty);
		var request = new byte[3 + userBytes.Length + passBytes.Length];
		request[0] = 1; // subnegotiation version
		request[1] = (byte)userBytes.Length;
		userBytes.CopyTo(request, 2);
		request[2 + userBytes.Length] = (byte)passBytes.Length;
		passBytes.CopyTo(request, 3 + userBytes.Length);
		await socket.SendAsync(request, SocketFlags.None, cancellationToken).ConfigureAwait(false);

		var response = await ReceiveExactAsync(socket, 2, cancellationToken).ConfigureAwait(false);
		if (response[1] != 0)
			throw new IndexerException("SOCKS5 proxy rejected the username/password credentials");
	}

	private static async Task ConnectTargetAsync(Socket socket, DnsEndPoint target, CancellationToken cancellationToken)
	{
		var hostBytes = Encoding.ASCII.GetBytes(target.Host);
		var request = new byte[7 + hostBytes.Length];
		request[0] = 5; // version
		request[1] = 1; // connect
		request[2] = 0; // reserved
		request[3] = 3; // atyp: domain name, let the proxy resolve it
		request[4] = (byte)hostBytes.Length;
		hostBytes.CopyTo(request, 5);
		request[5 + hostBytes.Length] = (byte)(target.Port >> 8);
		request[6 + hostBytes.Length] = (byte)target.Port;
		await socket.SendAsync(request, SocketFlags.None, cancellationToken).ConfigureAwait(false);

		var header = await ReceiveExactAsync(socket, 4, cancellationToken).ConfigureAwait(false);
		if (header[1] != 0)
			throw new IndexerException($"SOCKS5 proxy rejected the connect request with code {header[1]}");

		var addressLength = header[3] switch
		{
			1 => 4,
			4 => 16,
			3 => (await ReceiveExactAsync(socket, 1, cancellationToken).ConfigureAwait(false))[0],
			_ => throw new IndexerException($"SOCKS5 proxy returned an unknown address type {header[3]}")
		};
		await ReceiveExactAsync(socket, addressLength + 2, cancellationToken).ConfigureAwait(false); // bound address + port, unused
	}

	private static async Task<byte[]> ReceiveExactAsync(Socket socket, int count, CancellationToken cancellationToken)
	{
		var buffer = new byte[count];
		var read = 0;
		while (read < count)
		{
			var chunk = await socket.ReceiveAsync(buffer.AsMemory(read), SocketFlags.None, cancellationToken).ConfigureAwait(false);
			if (chunk == 0)
				throw new IndexerException("SOCKS5 proxy closed the connection during handshake");
			read += chunk;
		}

		return buffer;
	}
}

/// <summary>
///     FlareSolverr client, routes GET and POST through a FlareSolverr instance and mirrors
///     the solution cookies into the cookie container
/// </summary>
internal sealed class FlareSolverrClient(
	HttpClient client,
	string baseUrl,
	string sessionId,
	int maxTimeoutSeconds,
	CookieContainer cookies,
	ILogger logger)
{
	private bool _sessionCreated;

	/// <summary>
	///     Performs a request through FlareSolverr
	/// </summary>
	/// <param name="request">The original request</param>
	/// <param name="headers">Extra headers</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>A synthetic response built from the solution</returns>
	public async Task<HttpResponseMessage> RequestAsync(
		HttpRequestMessage request,
		IReadOnlyDictionary<string, IReadOnlyList<string>>? headers,
		CancellationToken cancellationToken)
	{
		await EnsureSessionAsync(cancellationToken).ConfigureAwait(false);

		var isPost = request.Method == HttpMethod.Post;
		string? postData = null;
		if (isPost && request.Content is { } content)
			postData = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

		var payload = new Dictionary<string, object?>
		{
			["cmd"] = isPost ? "request.post" : "request.get",
			["url"] = request.RequestUri!.ToString(),
			["session"] = sessionId,
			["maxTimeout"] = maxTimeoutSeconds * 1000
		};
		if (isPost)
			payload["postData"] = postData ?? string.Empty;

		using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/v1")
		{
			Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
		};
		using var response = await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
		var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

		FlareSolverrResponse? solution;
		try
		{
			solution = JsonSerializer.Deserialize<FlareSolverrResponse>(body);
		}
		catch (JsonException exception)
		{
			throw new IndexerException($"FlareSolverr returned an invalid response: {exception.Message}", exception);
		}

		if (solution?.Status != "ok" || solution.Solution is null)
			throw new IndexerException($"FlareSolverr failed: {solution?.Message ?? body}");

		foreach (var cookie in solution.Solution.Cookies ?? [])
		{
			try
			{
				cookies.Add(new Cookie(cookie.Name, cookie.Value, cookie.Path ?? "/", cookie.Domain)
				{
					Secure = string.Equals(cookie.Secure, "true", StringComparison.OrdinalIgnoreCase) || cookie.Secure == "True"
				});
			}
			catch (CookieException exception)
			{
				logger.LogWarning(exception, "FlareSolverr cookie {Name} could not be stored", cookie.Name);
			}
		}

		var result = new HttpResponseMessage((HttpStatusCode)(solution.Solution.Status ?? 200))
		{
			Content = new StringContent(solution.Solution.Response ?? string.Empty, Encoding.UTF8, "text/html")
		};
		result.RequestMessage = request;
		if (headers is not null)
		{
			foreach (var (name, values) in headers)
				result.Headers.TryAddWithoutValidation(name, values);
		}

		return result;
	}

	private async Task EnsureSessionAsync(CancellationToken cancellationToken)
	{
		if (_sessionCreated)
			return;

		var payload = new Dictionary<string, object?>
		{
			["cmd"] = "sessions.create",
			["session"] = sessionId
		};
		using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/v1")
		{
			Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
		};
		try
		{
			using var response = await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
			// an existing session returns an error status, that is fine
		}
		catch (HttpRequestException)
		{
			// the actual request will surface the connection problem
		}

		_sessionCreated = true;
	}
}

internal sealed record FlareSolverrResponse(
	[property: JsonPropertyName("status")] string? Status,
	[property: JsonPropertyName("message")] string? Message,
	[property: JsonPropertyName("solution")] FlareSolverrSolution? Solution);

internal sealed record FlareSolverrSolution(
	[property: JsonPropertyName("status")] int? Status,
	[property: JsonPropertyName("response")] string? Response,
	[property: JsonPropertyName("cookies")] List<FlareSolverrCookie>? Cookies);

internal sealed record FlareSolverrCookie(
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("value")] string Value,
	[property: JsonPropertyName("domain")] string? Domain,
	[property: JsonPropertyName("path")] string? Path,
	[property: JsonPropertyName("secure")] string? Secure);
