using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> for the Transmission RPC API
/// </summary>
public sealed class TransmissionClient(
	TransmissionSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : DownloadClientBase<TransmissionSettings>(settings, clientId, clientName, httpClient)
{
	private static readonly string[] TorrentFields =
	[
		"id", "hashString", "name", "totalSize", "leftUntilDone", "eta", "status", "downloadDir", "errorString",
		"labels"
	];

	private string? _sessionId;

	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.TRANSMISSION;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.BITTORRENT;

	private string RpcUrl => Settings.Build("/rpc");

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		var magnetUrl = MagnetUrl(release);

		var arguments = new JsonObject();
		if (magnetUrl is not null)
			arguments["filename"] = magnetUrl;
		else
			arguments["metainfo"] = Convert.ToBase64String(await GetTorrentDataAsync(release, cancellationToken));

		if (Settings.AddPaused)
			arguments["paused"] = true;

		if (Settings.Directory is { Length: > 0 })
			arguments["download-dir"] = Settings.Directory;

		if (Settings.Category is { Length: > 0 })
			arguments["labels"] = new JsonArray(JsonValue.Create(Settings.Category));

		var result = await CallAsync("torrent-add", arguments, cancellationToken);

		var torrent = result["torrent-added"] ?? result["torrent-duplicate"];
		if (torrent?["hashString"]?.GetValue<string>() is not { } hash)
			throw new DownloadClientException(
				$"Transmission {ClientName} did not return a torrent hash for {release.Title}");

		await ApplySeedCriteriaAsync(torrent, hash, seedCriteria, cancellationToken);

		return hash.ToUpperInvariant();
	}

	private async Task ApplySeedCriteriaAsync(JsonNode torrent, string hash, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		if (seedCriteria is null)
			return;

		var arguments = new JsonObject();

		if (seedCriteria.Ratio is { } ratio)
		{
			arguments["seedRatioLimit"] = ratio;
			arguments["seedRatioMode"] = 1;
		}

		if ((seedCriteria.SeedTimeMinutes ?? seedCriteria.SeasonPackSeedTimeMinutes) is { } minutes)
		{
			arguments["seedIdleLimit"] = minutes;
			arguments["seedIdleMode"] = 1;
		}

		if (arguments.Count == 0)
			return;

		arguments["ids"] = new JsonArray(torrent["id"]?.DeepClone() ?? JsonValue.Create(hash));
		await CallAsync("torrent-set", arguments, cancellationToken);
	}

	/// <inheritdoc />
	protected override async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(
		CancellationToken cancellationToken)
	{
		var arguments = new JsonObject
		{
			["fields"] = new JsonArray([.. TorrentFields.Select(field => JsonValue.Create(field))])
		};

		var result = await CallAsync("torrent-get", arguments, cancellationToken);

		var items = new List<DownloadClientItem>();
		if (result["torrents"] is JsonArray torrents)
			items.AddRange(torrents.Where(torrent => torrent is not null).Select(torrent => MapItem(torrent!)));

		return items;
	}

	/// <inheritdoc />
	protected override Task RemoveAsyncCore(string downloadId, bool deleteData, CancellationToken cancellationToken)
		=> CallAsync("torrent-remove", new JsonObject
		{
			["ids"] = new JsonArray(JsonValue.Create(downloadId)),
			["delete-local-data"] = deleteData
		}, cancellationToken);

	/// <inheritdoc />
	protected override Task TestAsyncCore(CancellationToken cancellationToken)
		=> CallAsync("session-get", null, cancellationToken);

	private async Task<JsonNode> CallAsync(string method, JsonObject? arguments, CancellationToken cancellationToken)
	{
		var payload = new JsonObject { ["method"] = method };
		if (arguments is not null)
			payload["arguments"] = arguments;

		var response = await SendRequestAsync(payload, cancellationToken);

		if (response.StatusCode == HttpStatusCode.Conflict)
		{
			if (response.Headers.TryGetValues("X-Transmission-Session-Id", out var values))
				_sessionId = values.FirstOrDefault();

			response.Dispose();
			response = await SendRequestAsync(payload, cancellationToken);
		}

		using (response)
		{
			EnsureSuccess(response, $"Transmission {ClientName} request");

			var body = await response.Content.ReadFromJsonAsync<JsonNode>(JsonOptions, cancellationToken)
				?? throw new DownloadClientException($"Transmission {ClientName} returned an empty response");

			var result = body["result"]?.GetValue<string>();
			if (result != "success")
				throw new DownloadClientException($"Transmission {ClientName} request failed: {result}");

			return body["arguments"] ?? new JsonObject();
		}
	}

	private async Task<HttpResponseMessage> SendRequestAsync(JsonObject payload, CancellationToken cancellationToken)
	{
		var request = new HttpRequestMessage(HttpMethod.Post, RpcUrl)
		{
			Content = JsonContent.Create(payload, options: JsonOptions)
		};

		if (_sessionId is not null)
			request.Headers.Add("X-Transmission-Session-Id", _sessionId);

		if (BasicAuth(Settings.Username, Settings.Password) is { } auth)
			request.Headers.Authorization = auth;

		return await SendAsync(() => request, cancellationToken);
	}

	private DownloadClientItem MapItem(JsonNode torrent)
	{
		var hash = torrent["hashString"]?.GetValue<string>()
			?? torrent["id"]?.GetValue<int>().ToString()
			?? string.Empty;
		var eta = torrent["eta"]?.GetValue<long>() ?? -1;
		var errorString = torrent["errorString"]?.GetValue<string>();
		var status = torrent["status"]?.GetValue<int>() ?? 0;
		var leftUntilDone = torrent["leftUntilDone"]?.GetValue<long>() ?? 0;

		return new DownloadClientItem
		{
			DownloadId = hash,
			Title = torrent["name"]?.GetValue<string>() ?? string.Empty,
			TotalSize = torrent["totalSize"]?.GetValue<long>() ?? 0,
			RemainingSize = leftUntilDone,
			RemainingTime = eta >= 0 ? TimeSpan.FromSeconds(eta) : null,
			Status = string.IsNullOrEmpty(errorString) ? MapStatus(status, leftUntilDone) : DownloadItemStatus.FAILED,
			OutputPath = torrent["downloadDir"]?.GetValue<string>(),
			Category = Settings.Category,
			Message = string.IsNullOrEmpty(errorString) ? null : errorString,
			IsReadOnly = Settings.Category is { Length: > 0 } && !HasLabel(torrent["labels"], Settings.Category)
		};
	}

	private static bool HasLabel(JsonNode? labels, string category)
		=> labels is JsonArray array && array.Any(label => label?.GetValue<string>() == category);

	private static DownloadItemStatus MapStatus(int status, long leftUntilDone)
		=> status switch
		{
			0 => leftUntilDone == 0 ? DownloadItemStatus.COMPLETED : DownloadItemStatus.PAUSED,
			1 or 3 => DownloadItemStatus.QUEUED,
			2 or 4 => DownloadItemStatus.DOWNLOADING,
			5 or 6 => DownloadItemStatus.COMPLETED,
			_ => DownloadItemStatus.WARNING
		};
}
