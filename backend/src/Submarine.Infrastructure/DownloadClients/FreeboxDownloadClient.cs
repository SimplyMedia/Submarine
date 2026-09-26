using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> for the Freebox Download api built into Freebox home gateways;
///     authenticates with a per-app challenge-response handshake that yields a short-lived session token
/// </summary>
public sealed class FreeboxDownloadClient(
	FreeboxDownloadSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : DownloadClientBase<FreeboxDownloadSettings>(settings, clientId, clientName, httpClient)
{
	private string? _sessionToken;

	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.FREEBOX_DOWNLOAD;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.BITTORRENT;

	private string ApiUrl(string resource) => Settings.Build($"{Settings.ApiUrl.TrimEnd('/')}{resource}");

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		var magnetUrl = MagnetUrl(release);
		var directory = await GetDownloadDirectoryAsync(release, cancellationToken);
		var seedRatio = seedCriteria?.Ratio is { } ratio ? ratio * 100 : (double?)null;

		string id;
		if (magnetUrl is not null)
		{
			var form = new List<KeyValuePair<string, string>>
			{
				new("download_url", Uri.EscapeDataString(magnetUrl))
			};
			if (directory is { Length: > 0 })
				form.Add(new KeyValuePair<string, string>("download_dir", directory));

			var result = await RequestAsync(HttpMethod.Post, "downloads/add",
				() => new FormUrlEncodedContent(form), cancellationToken);
			id = result.GetProperty("result").GetProperty("id").GetString() ?? string.Empty;
		}
		else
		{
			var torrentData = await GetTorrentDataAsync(release, cancellationToken);
			var result = await RequestAsync(HttpMethod.Post, "downloads/add",
				() => BuildTorrentUpload(torrentData, directory), cancellationToken);
			id = result.GetProperty("result").GetProperty("id").GetString() ?? string.Empty;
		}

		await ApplyTaskSettingsAsync(id, seedRatio, cancellationToken);

		return id;
	}

	private static MultipartFormDataContent BuildTorrentUpload(byte[] torrentData, string? directory)
	{
		var content = new MultipartFormDataContent();
		content.Add(new ByteArrayContent(torrentData), "download_file", "release.torrent");
		if (directory is { Length: > 0 })
			content.Add(new StringContent(directory), "download_dir");

		return content;
	}

	private async Task ApplyTaskSettingsAsync(string id, double? seedRatio, CancellationToken cancellationToken)
	{
		var body = new Dictionary<string, object>();
		if (Settings.AddPaused)
			body["status"] = "stopped";
		if (seedRatio is not null)
			body["stop_ratio"] = seedRatio;

		if (body.Count == 0)
			return;

		await RequestAsync(HttpMethod.Put, $"downloads/{id}",
			() => new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
			cancellationToken);
	}

	/// <inheritdoc />
	protected override async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(
		CancellationToken cancellationToken)
	{
		var result = await RequestAsync(HttpMethod.Get, "downloads/", null, cancellationToken);
		var items = new List<DownloadClientItem>();

		if (!result.TryGetProperty("result", out var tasks) || tasks.ValueKind != JsonValueKind.Array)
			return items;

		foreach (var task in tasks.EnumerateArray())
		{
			if (task.TryGetProperty("type", out var typeEl) && typeEl.GetString() != "bt")
				continue;

			if (MapItem(task) is { } item)
				items.Add(item);
		}

		return items;
	}

	private DownloadClientItem? MapItem(JsonElement task)
	{
		var directory = task.TryGetProperty("download_dir", out var dirEl)
			? DecodeBase64(dirEl.GetString())
			: null;

		if (Settings.DestinationDirectory is { Length: > 0 }
			&& (directory is null || !directory.StartsWith(Settings.DestinationDirectory, StringComparison.Ordinal)))
			return null;

		if (Settings.Category is { Length: > 0 }
			&& (directory is null || !directory.Split('/', '\\').Contains(Settings.Category)))
			return null;

		var id = task.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
		var totalSize = task.TryGetProperty("size", out var sizeEl) ? sizeEl.GetInt64() : 0;
		var receivedPercent = task.TryGetProperty("rx_pct", out var rxEl) ? rxEl.GetInt32() : 0;
		var eta = task.TryGetProperty("eta", out var etaEl) ? etaEl.GetInt32() : 0;
		var stopRatio = task.TryGetProperty("stop_ratio", out var ratioEl) ? ratioEl.GetDouble() : 0;
		var status = task.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
		var error = task.TryGetProperty("error", out var errorEl) ? errorEl.GetString() : null;

		return new DownloadClientItem
		{
			DownloadId = id,
			Title = task.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty,
			Category = Settings.Category,
			TotalSize = totalSize,
			RemainingSize = (long)(totalSize * (1 - receivedPercent / 10000.0)),
			RemainingTime = eta > 0 ? TimeSpan.FromSeconds(eta) : null,
			SeedRatio = stopRatio > 0 ? stopRatio / 100 : 0,
			OutputPath = directory,
			Status = MapStatus(status),
			Message = string.IsNullOrEmpty(error) || error == "none" ? null : error
		};
	}

	private static DownloadItemStatus MapStatus(string? status)
		=> status switch
		{
			"stopped" or "stopping" => DownloadItemStatus.PAUSED,
			"queued" => DownloadItemStatus.QUEUED,
			"starting" or "downloading" or "retry" or "checking" => DownloadItemStatus.DOWNLOADING,
			"error" => DownloadItemStatus.WARNING,
			"done" or "seeding" => DownloadItemStatus.COMPLETED,
			_ => DownloadItemStatus.DOWNLOADING
		};

	/// <inheritdoc />
	protected override async Task RemoveAsyncCore(string downloadId, bool deleteData,
		CancellationToken cancellationToken)
	{
		var resource = deleteData ? $"downloads/{downloadId}/erase" : $"downloads/{downloadId}";
		await RequestAsync(HttpMethod.Delete, resource, null, cancellationToken);
	}

	/// <inheritdoc />
	protected override async Task TestAsyncCore(CancellationToken cancellationToken)
		=> await AuthenticateAsync(cancellationToken);

	private async Task<string?> GetDownloadDirectoryAsync(RemoteRelease release, CancellationToken cancellationToken)
	{
		string destination;
		if (Settings.DestinationDirectory is { Length: > 0 } configured)
		{
			destination = configured.TrimEnd('/');
		}
		else
		{
			var config = await RequestAsync(HttpMethod.Get, "downloads/config/", null, cancellationToken);
			destination = (DecodeBase64(config.GetProperty("result").GetProperty("download_dir").GetString())
				?? string.Empty).TrimEnd('/');

			if (Settings.Category is { Length: > 0 })
				destination = $"{destination}/{Settings.Category}";
		}

		destination = $"{destination}/{SanitizeFileName(release.Title)}";

		return EncodeBase64(destination);
	}

	private static string? DecodeBase64(string? value)
		=> value is null ? null : Encoding.UTF8.GetString(Convert.FromBase64String(value));

	private static string? EncodeBase64(string? value)
		=> value is null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

	private async Task<JsonElement> RequestAsync(HttpMethod method, string resource,
		Func<HttpContent?>? contentFactory, CancellationToken cancellationToken)
	{
		await AuthenticateAsync(cancellationToken);

		using var response = await SendAsync(
			() =>
			{
				var request = new HttpRequestMessage(method, ApiUrl($"/{resource}"))
				{
					Content = contentFactory?.Invoke()
				};
				request.Headers.Add("X-Fbx-App-Auth", _sessionToken);

				return request;
			}, cancellationToken);

		if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
		{
			_sessionToken = null;
			response.Dispose();
			await AuthenticateAsync(cancellationToken);

			using var retry = await SendAsync(
				() =>
				{
					var request = new HttpRequestMessage(method, ApiUrl($"/{resource}"))
					{
						Content = contentFactory?.Invoke()
					};
					request.Headers.Add("X-Fbx-App-Auth", _sessionToken);

					return request;
				}, cancellationToken);

			return await ReadResultAsync(retry, cancellationToken);
		}

		return await ReadResultAsync(response, cancellationToken);
	}

	private async Task<JsonElement> ReadResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		EnsureSuccess(response, $"Freebox Download {ClientName} request");

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		var root = document.RootElement.Clone();

		if (root.TryGetProperty("success", out var success) && !success.GetBoolean())
		{
			var message = root.TryGetProperty("msg", out var msg) ? msg.GetString() : "unknown error";
			throw new DownloadClientException($"Freebox Download {ClientName} returned an error: {message}");
		}

		return root;
	}

	private async Task AuthenticateAsync(CancellationToken cancellationToken)
	{
		if (_sessionToken is not null)
			return;

		using var challengeResponse = await SendAsync(
			() => new HttpRequestMessage(HttpMethod.Get, ApiUrl("/login")), cancellationToken);
		EnsureSuccess(challengeResponse, $"Freebox Download {ClientName} authentication");

		using var challengeDocument =
			JsonDocument.Parse(await challengeResponse.Content.ReadAsStringAsync(cancellationToken));
		var challenge = challengeDocument.RootElement.GetProperty("result").GetProperty("challenge").GetString()
			?? string.Empty;

		var password = Convert.ToHexStringLower(
			HMACSHA1.HashData(Encoding.ASCII.GetBytes(Settings.AppToken), Encoding.ASCII.GetBytes(challenge)));

		var sessionRequest = JsonSerializer.Serialize(new { app_id = Settings.AppId, password });
		using var sessionResponse = await SendAsync(
			() => new HttpRequestMessage(HttpMethod.Post, ApiUrl("/login/session"))
			{
				Content = new StringContent(sessionRequest, Encoding.UTF8, "application/json")
			}, cancellationToken);
		EnsureSuccess(sessionResponse, $"Freebox Download {ClientName} authentication");

		using var sessionDocument =
			JsonDocument.Parse(await sessionResponse.Content.ReadAsStringAsync(cancellationToken));
		var sessionRoot = sessionDocument.RootElement;

		if (!sessionRoot.TryGetProperty("success", out var success) || !success.GetBoolean())
			throw new DownloadClientException(
				$"Freebox Download {ClientName} authentication failed, check the app id and app token");

		_sessionToken = sessionRoot.GetProperty("result").GetProperty("session_token").GetString();

		if (_sessionToken is null)
			throw new DownloadClientException($"Freebox Download {ClientName} did not return a session token");
	}
}
