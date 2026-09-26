using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> for the NZBVortex api; NZBVortex is always reached over https and
///     authenticates with a nonce-challenge handshake that yields a short-lived session id
/// </summary>
public sealed class NzbVortexClient(
	NzbVortexSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : DownloadClientBase<NzbVortexSettings>(settings, clientId, clientName, httpClient)
{
	private string? _sessionId;

	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.NZBVORTEX;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.USENET;

	private string BaseUrl
		=> $"https://{Settings.Host}:{Settings.Port}{DownloadClientUrl.NormalizePath(Settings.UrlBase)}/api";

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		var nzbData = await GetNzbDataAsync(release, cancellationToken);
		var fileName = $"{SanitizeFileName(release.Title)}.nzb";

		var query = new List<KeyValuePair<string, string>>
		{
			new("priority", ((int)Settings.RecentPriority).ToString())
		};
		if (Settings.Category is { Length: > 0 })
			query.Add(new KeyValuePair<string, string>("groupname", Settings.Category));

		var result = await RequestAsync(HttpMethod.Post, "nzb/add", query,
			() => BuildNzbUpload(nzbData, fileName), requiresAuth: true, cancellationToken);

		return result.TryGetProperty("add_uuid", out var id) ? id.GetString() ?? string.Empty : string.Empty;
	}

	/// <inheritdoc />
	protected override async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(
		CancellationToken cancellationToken)
	{
		var items = await GetQueueAsync(cancellationToken);

		return [.. items.Select(MapItem)];
	}

	/// <inheritdoc />
	protected override async Task RemoveAsyncCore(string downloadId, bool deleteData,
		CancellationToken cancellationToken)
	{
		var id = await ResolveIdAsync(downloadId, cancellationToken);
		if (id is null)
			return;

		await RequestAsync(HttpMethod.Get, $"nzb/{id}/{(deleteData ? "cancelDelete" : "cancel")}", null, null,
			requiresAuth: true, cancellationToken);
	}

	/// <inheritdoc />
	protected override async Task TestAsyncCore(CancellationToken cancellationToken)
	{
		await RequestAsync(HttpMethod.Get, "app/appversion", null, null, requiresAuth: false, cancellationToken);
		await RequestAsync(HttpMethod.Get, "nzb", [new KeyValuePair<string, string>("limitDone", "1")], null,
			requiresAuth: true, cancellationToken);
	}

	private static MultipartFormDataContent BuildNzbUpload(byte[] nzbData, string fileName)
	{
		var content = new MultipartFormDataContent();
		var file = new ByteArrayContent(nzbData);
		file.Headers.ContentType = new MediaTypeHeaderValue("application/x-nzb");
		content.Add(file, "name", fileName);
		return content;
	}

	private async Task<string?> ResolveIdAsync(string downloadId, CancellationToken cancellationToken)
	{
		if (int.TryParse(downloadId, out _))
			return downloadId;

		var items = await GetQueueAsync(cancellationToken);
		foreach (var item in items)
			if (item.TryGetProperty("addUUID", out var uuid) && uuid.GetString() == downloadId)
				return item.TryGetProperty("id", out var id) ? id.GetInt32().ToString() : null;

		return null;
	}

	private async Task<List<JsonElement>> GetQueueAsync(CancellationToken cancellationToken)
	{
		var query = new List<KeyValuePair<string, string>> { new("limitDone", "30") };
		if (Settings.Category is { Length: > 0 })
			query.Add(new KeyValuePair<string, string>("groupName", Settings.Category));

		var result = await RequestAsync(HttpMethod.Get, "nzb", query, null, requiresAuth: true, cancellationToken);

		var items = new List<JsonElement>();
		if (result.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
			items.AddRange(itemsElement.EnumerateArray());

		return items;
	}

	private DownloadClientItem MapItem(JsonElement item)
	{
		var id = item.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
		var addUuid = item.TryGetProperty("addUUID", out var uuidEl) ? uuidEl.GetString() : null;
		var title = item.TryGetProperty("uiTitle", out var titleEl) ? titleEl.GetString() ?? string.Empty : string.Empty;
		var destination = item.TryGetProperty("destinationPath", out var destEl) ? destEl.GetString() : null;
		var isPaused = item.TryGetProperty("isPaused", out var pausedEl) && pausedEl.GetBoolean();
		var state = item.TryGetProperty("state", out var stateEl) ? stateEl.GetInt32() : 0;
		var totalSize = item.TryGetProperty("totalDownloadSize", out var totalEl) ? totalEl.GetInt64() : 0;
		var downloadedSize = item.TryGetProperty("downloadedSize", out var downloadedEl) ? downloadedEl.GetInt64() : 0;
		var groupName = item.TryGetProperty("groupName", out var groupEl) ? groupEl.GetString() : null;

		return new DownloadClientItem
		{
			DownloadId = string.IsNullOrEmpty(addUuid) ? id.ToString() : addUuid,
			Title = title,
			Category = groupName,
			TotalSize = totalSize,
			RemainingSize = totalSize - downloadedSize,
			OutputPath = destination,
			Status = isPaused ? DownloadItemStatus.PAUSED : MapState(state)
		};
	}

	private static DownloadItemStatus MapState(int state)
		=> state switch
		{
			0 => DownloadItemStatus.QUEUED, // Waiting
			20 => DownloadItemStatus.COMPLETED, // Done
			21 or 22 or 24 => DownloadItemStatus.FAILED, // UncompressFailed, CheckFailedDataCorrupt, BadlyEncoded
			_ => DownloadItemStatus.DOWNLOADING
		};

	private async Task<JsonElement> RequestAsync(HttpMethod method, string resource,
		IReadOnlyList<KeyValuePair<string, string>>? query, Func<HttpContent?>? contentFactory, bool requiresAuth,
		CancellationToken cancellationToken)
	{
		if (requiresAuth)
			await AuthenticateAsync(cancellationToken);

		var (root, result) = await SendAsync(method, resource, query, contentFactory, requiresAuth, cancellationToken);

		if (requiresAuth && result == "not_logged_in")
		{
			_sessionId = null;
			await AuthenticateAsync(cancellationToken);
			(root, result) = await SendAsync(method, resource, query, contentFactory, requiresAuth, cancellationToken);
		}

		if (result == "not_logged_in")
			throw new DownloadClientException($"NZBVortex {ClientName} authentication failed, check the api key");

		return root;
	}

	private async Task<(JsonElement Root, string? Result)> SendAsync(HttpMethod method, string resource,
		IReadOnlyList<KeyValuePair<string, string>>? query, Func<HttpContent?>? contentFactory, bool requiresAuth,
		CancellationToken cancellationToken)
	{
		var url = new StringBuilder($"{BaseUrl}/{resource}");
		var parameters = new List<KeyValuePair<string, string>>(query ?? []);
		if (requiresAuth && _sessionId is not null)
			parameters.Add(new KeyValuePair<string, string>("sessionid", _sessionId));

		if (parameters.Count > 0)
			url.Append('?').Append(string.Join('&',
				parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}")));

		using var response = await SendAsync(
			() => new HttpRequestMessage(method, url.ToString()) { Content = contentFactory?.Invoke() },
			cancellationToken);
		EnsureSuccess(response, $"NZBVortex {ClientName} request");

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		var root = document.RootElement.Clone();
		var result = root.TryGetProperty("result", out var resultEl) ? resultEl.GetString() : null;

		return (root, result);
	}

	private async Task AuthenticateAsync(CancellationToken cancellationToken)
	{
		if (_sessionId is not null)
			return;

		using var nonceResponse = await SendAsync(
			() => new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/auth/nonce"), cancellationToken);
		EnsureSuccess(nonceResponse, $"NZBVortex {ClientName} authentication");

		using var nonceDocument =
			JsonDocument.Parse(await nonceResponse.Content.ReadAsStringAsync(cancellationToken));
		var nonce = nonceDocument.RootElement.GetProperty("authNonce").GetString() ?? string.Empty;
		var cnonce = Guid.NewGuid().ToString();
		var hash = Convert.ToBase64String(
			System.Security.Cryptography.SHA256.HashData(
				Encoding.UTF8.GetBytes($"{nonce}:{cnonce}:{Settings.ApiKey}")));

		var loginUrl = $"{BaseUrl}/auth/login?nonce={Uri.EscapeDataString(nonce)}&cnonce={Uri.EscapeDataString(cnonce)}&hash={Uri.EscapeDataString(hash)}";
		using var loginResponse = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, loginUrl),
			cancellationToken);
		EnsureSuccess(loginResponse, $"NZBVortex {ClientName} authentication");

		using var loginDocument =
			JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync(cancellationToken));
		var loginResult = loginDocument.RootElement.TryGetProperty("loginResult", out var loginResultEl)
			? loginResultEl.GetString()
			: null;

		if (!string.Equals(loginResult, "ok", StringComparison.OrdinalIgnoreCase))
			throw new DownloadClientException($"NZBVortex {ClientName} authentication failed, check the api key");

		_sessionId = loginDocument.RootElement.TryGetProperty("sessionId", out var sessionIdEl)
			? sessionIdEl.GetString()
			: null;

		if (_sessionId is null)
			throw new DownloadClientException($"NZBVortex {ClientName} did not return a session id");
	}
}
