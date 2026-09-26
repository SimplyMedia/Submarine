using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> for the uTorrent web ui
/// </summary>
public sealed partial class UTorrentClient(
	UTorrentSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : DownloadClientBase<UTorrentSettings>(settings, clientId, clientName, httpClient)
{
	private string? _token;

	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.UTORRENT;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.BITTORRENT;

	private string Base => Settings.BaseUrl() + DownloadClientUrl.NormalizePath(Settings.EffectiveUrlBase);

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		var magnetUrl = MagnetUrl(release);
		string downloadId;

		if (magnetUrl is not null)
		{
			await GuiRequestAsync($"action=add-url&s={Uri.EscapeDataString(magnetUrl)}", cancellationToken);
			downloadId = ResolveTorrentId(release, magnetUrl, null);
		}
		else
		{
			var torrentData = await GetTorrentDataAsync(release, cancellationToken);
			await GuiUploadAsync($"action=add-file", torrentData, $"{SanitizeFileName(release.Title)}.torrent",
				cancellationToken);
			downloadId = ResolveTorrentId(release, magnetUrl, torrentData);
		}

		var stateAction = Settings.InitialState switch
		{
			UTorrentInitialState.FORCE_START => "forcestart",
			UTorrentInitialState.PAUSE => "pause",
			UTorrentInitialState.STOP => "stop",
			_ => null
		};
		if (stateAction is not null)
			await GuiRequestAsync($"action={stateAction}&hash={downloadId}", cancellationToken);

		if (Settings.Category is { Length: > 0 })
			await GuiRequestAsync(
				$"action=setprops&hash={downloadId}&s=label&v={Uri.EscapeDataString(Settings.Category)}",
				cancellationToken);

		var priority = release.IsRecentRelease ? Settings.RecentPriority : Settings.OlderPriority;
		if (priority == DownloadClientItemPriority.FIRST)
			await GuiRequestAsync($"action=queuetop&hash={downloadId}", cancellationToken);

		return downloadId;
	}

	/// <inheritdoc />
	protected override async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(
		CancellationToken cancellationToken)
	{
		var response = await GuiRequestAsync("list=1", cancellationToken);
		using var document = JsonDocument.Parse(response);

		return [.. document.RootElement.GetProperty("torrents").EnumerateArray().Select(MapItem)];
	}

	/// <inheritdoc />
	protected override Task RemoveAsyncCore(string downloadId, bool deleteData, CancellationToken cancellationToken)
		=> GuiRequestAsync($"action={(deleteData ? "removedata" : "remove")}&hash={downloadId}", cancellationToken);

	/// <inheritdoc />
	public override Task MarkImportedAsync(string downloadId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(Settings.PostImportCategory) || Settings.PostImportCategory == Settings.Category)
			return Task.CompletedTask;

		return GuiRequestAsync(
			$"action=setprops&hash={downloadId}&s=label&v={Uri.EscapeDataString(Settings.PostImportCategory)}",
			cancellationToken);
	}

	/// <inheritdoc />
	protected override Task TestAsyncCore(CancellationToken cancellationToken)
		=> GuiRequestAsync("list=1", cancellationToken);

	private DownloadClientItem MapItem(JsonElement torrent)
	{
		var status = torrent[1].GetInt32();
		var percent = torrent[4].GetInt32();
		var eta = torrent[10].GetInt32();
		var label = torrent[11].GetString();

		return new DownloadClientItem
		{
			DownloadId = torrent[0].GetString() ?? string.Empty,
			Title = torrent[2].GetString() ?? string.Empty,
			TotalSize = torrent[3].GetInt64(),
			RemainingSize = torrent[18].GetInt64(),
			RemainingTime = eta > 0 ? TimeSpan.FromSeconds(eta) : null,
			Status = percent == 1000
				? DownloadItemStatus.COMPLETED
				: (status & 16) != 0
					? DownloadItemStatus.FAILED
					: (status & 32) != 0
						? DownloadItemStatus.PAUSED
						: (status & 1) != 0
							? DownloadItemStatus.DOWNLOADING
							: DownloadItemStatus.QUEUED,
			OutputPath = torrent.GetArrayLength() > 26 ? torrent[26].GetString() : null,
			Category = string.IsNullOrEmpty(label) ? null : label,
			IsReadOnly = Settings.Category is { Length: > 0 } && label != Settings.Category
		};
	}

	private async Task<string> GuiRequestAsync(string query, CancellationToken cancellationToken)
	{
		var response = await GuiGetAsync(await GetTokenAsync(false, cancellationToken), query, cancellationToken);

		if (response.StatusCode == HttpStatusCode.BadRequest)
		{
			response.Dispose();
			response = await GuiGetAsync(await GetTokenAsync(true, cancellationToken), query, cancellationToken);
		}

		using (response)
		{
			EnsureSuccess(response, $"uTorrent {ClientName} request");

			return await response.Content.ReadAsStringAsync(cancellationToken);
		}
	}

	private async Task<HttpResponseMessage> GuiGetAsync(string token, string query, CancellationToken cancellationToken)
		=> await SendAsync(
			() =>
			{
				var request = new HttpRequestMessage(HttpMethod.Get, $"{Base}/?token={token}&{query}");
				if (BasicAuth(Settings.Username, Settings.Password) is { } auth)
					request.Headers.Authorization = auth;

				return request;
			}, cancellationToken);

	private async Task GuiUploadAsync(string query, byte[] data, string fileName, CancellationToken cancellationToken)
	{
		var response = await SendAsync(
			() =>
			{
				var request = new HttpRequestMessage(HttpMethod.Post, $"{Base}/?token={_token}&{query}")
				{
					Content = new MultipartFormDataContent
					{
						{ new ByteArrayContent(data), "torrent_file", fileName }
					}
				};

				if (BasicAuth(Settings.Username, Settings.Password) is { } auth)
					request.Headers.Authorization = auth;

				return request;
			}, cancellationToken);

		using (response)
			EnsureSuccess(response, $"uTorrent {ClientName} upload");
	}

	private async Task<string> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
	{
		if (!forceRefresh && _token is not null)
			return _token;

		using var response = await SendAsync(
			() =>
			{
				var request = new HttpRequestMessage(HttpMethod.Get, $"{Base}/token.html");
				if (BasicAuth(Settings.Username, Settings.Password) is { } auth)
					request.Headers.Authorization = auth;

				return request;
			}, cancellationToken);
		EnsureSuccess(response, $"uTorrent {ClientName} token request");

		var html = await response.Content.ReadAsStringAsync(cancellationToken);
		var match = TokenRegex().Match(html);

		if (!match.Success)
			throw new DownloadClientException(
				$"uTorrent {ClientName} did not return a web ui token, check that the web ui is enabled");

		return _token = match.Groups[1].Value;
	}

	[GeneratedRegex("id=['\"]token['\"][^>]*>([^<]+)<")]
	private static partial Regex TokenRegex();
}
