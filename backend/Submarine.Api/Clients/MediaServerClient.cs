using System.Net.Http.Json;
using System.Text.Json;
using Submarine.Core.Notification;

namespace Submarine.Api.Clients;

/// <summary>
///     Creates the matching <see cref="IMediaServerClient" /> for a <see cref="Connection" /> row
/// </summary>
public class MediaServerClientFactory : IMediaServerClientFactory
{
	private readonly IHttpClientFactory _httpClientFactory;

	/// <summary>
	///     Creates a new instance of <see cref="MediaServerClientFactory" />
	/// </summary>
	/// <param name="httpClientFactory">factory providing the "mediaserver" http client</param>
	public MediaServerClientFactory(IHttpClientFactory httpClientFactory)
		=> _httpClientFactory = httpClientFactory;

	/// <inheritdoc />
	public IMediaServerClient Create(Connection connection)
	{
		var httpClient = _httpClientFactory.CreateClient("mediaserver");

		return connection.Type switch
		{
			ConnectionType.PLEX => new PlexClient(httpClient, connection),
			ConnectionType.EMBY or ConnectionType.JELLYFIN => new EmbyJellyfinClient(httpClient, connection),
			_ => throw new ArgumentOutOfRangeException(nameof(connection),
				$"unknown connection type {connection.Type}")
		};
	}

	internal static Uri BaseUri(Connection connection)
		=> new($"{(connection.UseSsl ? "https" : "http")}://{connection.Host}:{connection.Port}");
}

/// <summary>
///     <see cref="IMediaServerClient" /> for Plex Media Server, refreshing the library section containing a path
/// </summary>
public class PlexClient : IMediaServerClient
{
	private readonly HttpClient _httpClient;
	private readonly Connection _connection;

	/// <summary>
	///     Creates a new instance of <see cref="PlexClient" />
	/// </summary>
	/// <param name="httpClient">http client to talk to Plex with</param>
	/// <param name="connection">connection describing the Plex server</param>
	public PlexClient(HttpClient httpClient, Connection connection)
	{
		_httpClient = httpClient;
		_connection = connection;
	}

	/// <inheritdoc />
	public async Task NotifyMediaUpdatedAsync(string path, CancellationToken cancellationToken = default)
	{
		using var sectionsResponse = await SendAsync(HttpMethod.Get, "/library/sections", cancellationToken);
		sectionsResponse.EnsureSuccessStatusCode();

		using var document = await JsonDocument.ParseAsync(
			await sectionsResponse.Content.ReadAsStreamAsync(cancellationToken),
			cancellationToken: cancellationToken);

		if (!document.RootElement.GetProperty("MediaContainer").TryGetProperty("Directory", out var directories))
			return;

		foreach (var directory in directories.EnumerateArray())
		{
			if (!directory.TryGetProperty("Location", out var locations))
				continue;

			var matches = locations.EnumerateArray().Any(l =>
				l.TryGetProperty("path", out var sectionPath) &&
				sectionPath.GetString() is { } prefix &&
				path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

			if (!matches)
				continue;

			var key = directory.GetProperty("key").GetString();

			using var refreshResponse = await SendAsync(HttpMethod.Get,
				$"/library/sections/{key}/refresh?path={Uri.EscapeDataString(path)}", cancellationToken);
			refreshResponse.EnsureSuccessStatusCode();
		}
	}

	/// <inheritdoc />
	public async Task TestAsync(CancellationToken cancellationToken = default)
	{
		using var response = await SendAsync(HttpMethod.Get, "/identity", cancellationToken);

		response.EnsureSuccessStatusCode();
	}

	private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path,
		CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(method,
			new Uri(MediaServerClientFactory.BaseUri(_connection), path));
		request.Headers.Add("X-Plex-Token", _connection.ApiKey);
		request.Headers.Add("Accept", "application/json");

		return await _httpClient.SendAsync(request, cancellationToken);
	}
}

/// <summary>
///     <see cref="IMediaServerClient" /> shared by Emby and Jellyfin, using the Media Updated library endpoint
/// </summary>
public class EmbyJellyfinClient : IMediaServerClient
{
	private readonly HttpClient _httpClient;
	private readonly Connection _connection;

	/// <summary>
	///     Creates a new instance of <see cref="EmbyJellyfinClient" />
	/// </summary>
	/// <param name="httpClient">http client to talk to the media server with</param>
	/// <param name="connection">connection describing the Emby or Jellyfin server</param>
	public EmbyJellyfinClient(HttpClient httpClient, Connection connection)
	{
		_httpClient = httpClient;
		_connection = connection;
	}

	/// <inheritdoc />
	public async Task NotifyMediaUpdatedAsync(string path, CancellationToken cancellationToken = default)
	{
		using var request = CreateRequest(HttpMethod.Post, "/Library/Media/Updated");
		request.Content = JsonContent.Create(new
		{
			Updates = new[] { new { Path = path, UpdateType = "Created" } }
		});

		using var response = await _httpClient.SendAsync(request, cancellationToken);

		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public async Task TestAsync(CancellationToken cancellationToken = default)
	{
		using var request = CreateRequest(HttpMethod.Get, "/System/Info");
		using var response = await _httpClient.SendAsync(request, cancellationToken);

		response.EnsureSuccessStatusCode();
	}

	private HttpRequestMessage CreateRequest(HttpMethod method, string path)
	{
		var request = new HttpRequestMessage(method, new Uri(MediaServerClientFactory.BaseUri(_connection), path));
		request.Headers.Add("X-Emby-Token", _connection.ApiKey);
		request.Headers.Add("X-MediaBrowser-Token", _connection.ApiKey);

		return request;
	}
}
