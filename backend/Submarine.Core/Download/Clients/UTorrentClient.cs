using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Indexer;
using Submarine.Core.Provider;

namespace Submarine.Core.Download.Clients;

/// <summary>
///     Download client talking to the uTorrent web ui
/// </summary>
public class UTorrentClient : IDownloadClient
{
	private readonly UTorrentSettings _settings;
	private readonly HttpClient _httpClient;
	private readonly ILogger<UTorrentClient> _logger;

	private string? _token;

	/// <summary>
	///     Creates a new instance of <see cref="UTorrentClient" />
	/// </summary>
	/// <param name="settings">connection settings</param>
	/// <param name="httpClient">http client used for the web ui calls</param>
	/// <param name="logger">logger</param>
	public UTorrentClient(UTorrentSettings settings, HttpClient httpClient, ILogger<UTorrentClient> logger)
	{
		_settings = settings;
		_httpClient = httpClient;
		_logger = logger;

		if (settings.Username is { Length: > 0 })
			_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
				Convert.ToBase64String(Encoding.ASCII.GetBytes($"{settings.Username}:{settings.Password}")));
	}

	/// <inheritdoc />
	public Protocol Protocol => Protocol.BITTORRENT;

	/// <inheritdoc />
	public async Task<string> AddDownloadAsync(ReleaseInfo release, CancellationToken cancellationToken = default)
	{
		var url = release.DownloadUrl
			?? throw new DownloadClientException("Release has no download url");

		var hash = MagnetHash(url)
			?? throw new DownloadClientException("uTorrent add requires a magnet link to resolve the download id");

		await GuiRequestAsync($"action=add-url&s={Uri.EscapeDataString(url)}", cancellationToken);

		return hash;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken = default)
	{
		using var document = JsonDocument.Parse(await GuiRequestAsync("list=1", cancellationToken));

		return document.RootElement.GetProperty("torrents").EnumerateArray().Select(MapItem).ToList();
	}

	/// <inheritdoc />
	public async Task RemoveItemAsync(string downloadId, bool deleteData,
		CancellationToken cancellationToken = default)
		=> await GuiRequestAsync(
			$"action={(deleteData ? "removedata" : "remove")}&hash={downloadId}", cancellationToken);

	/// <inheritdoc />
	public async Task TestAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			await GuiRequestAsync("list=1", cancellationToken);
		}
		catch (DownloadClientException)
		{
			throw;
		}
		catch (Exception e)
		{
			throw new DownloadClientException("Failed to reach uTorrent", e);
		}
	}

	private static DownloadClientItem MapItem(JsonElement torrent)
	{
		var status = torrent[1].GetInt32();
		var percent = torrent[4].GetInt32();
		var eta = torrent[10].GetInt32();
		var label = torrent[11].GetString();

		return new DownloadClientItem
		{
			DownloadId = torrent[0].GetString()!,
			Title = torrent[2].GetString()!,
			TotalSize = torrent[3].GetInt64(),
			RemainingSize = torrent[18].GetInt64(),
			RemainingTime = eta > 0 ? TimeSpan.FromSeconds(eta) : null,
			Status = percent == 1000
				? DownloadItemStatus.COMPLETED
				: (status & 32) != 0
					? DownloadItemStatus.PAUSED
					: (status & 1) != 0
						? DownloadItemStatus.DOWNLOADING
						: DownloadItemStatus.QUEUED,
			OutputPath = torrent.GetArrayLength() > 26 ? torrent[26].GetString() : null,
			Category = string.IsNullOrEmpty(label) ? null : label
		};
	}

	private static string? MagnetHash(string url)
	{
		var match = Regex.Match(url, "xt=urn:btih:([^&]+)", RegexOptions.IgnoreCase);

		return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
	}

	private async Task<string> GuiRequestAsync(string query, CancellationToken cancellationToken)
	{
		var response = await _httpClient.GetAsync(
			$"{_settings.BaseUrl}/?token={await GetTokenAsync(false, cancellationToken)}&{query}", cancellationToken);

		if (response.StatusCode == HttpStatusCode.BadRequest)
		{
			response.Dispose();
			response = await _httpClient.GetAsync(
				$"{_settings.BaseUrl}/?token={await GetTokenAsync(true, cancellationToken)}&{query}",
				cancellationToken);
		}

		using (response)
		{
			response.EnsureSuccessStatusCode();

			return await response.Content.ReadAsStringAsync(cancellationToken);
		}
	}

	private async Task<string> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
	{
		if (!forceRefresh && _token is not null)
			return _token;

		using var response = await _httpClient.GetAsync($"{_settings.BaseUrl}/token.html", cancellationToken);
		response.EnsureSuccessStatusCode();

		var html = await response.Content.ReadAsStringAsync(cancellationToken);
		var match = Regex.Match(html, "id=['\"]token['\"][^>]*>([^<]+)<");

		if (!match.Success)
			throw new DownloadClientException("Failed to read uTorrent token");

		return _token = match.Groups[1].Value;
	}
}
