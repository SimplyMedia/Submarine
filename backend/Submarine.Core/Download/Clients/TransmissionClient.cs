using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Submarine.Core.Indexer;
using Submarine.Core.Provider;

namespace Submarine.Core.Download.Clients;

/// <summary>
///     <see cref="IDownloadClient" /> for the Transmission RPC API
/// </summary>
public class TransmissionClient : IDownloadClient
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private static readonly string[] TorrentFields =
		["id", "hashString", "name", "totalSize", "leftUntilDone", "eta", "status", "downloadDir", "errorString"];

	private readonly TransmissionSettings _settings;
	private readonly HttpClient _httpClient;
	private readonly ILogger<TransmissionClient> _logger;
	private readonly string _rpcUrl;

	private string? _sessionId;

	/// <summary>
	///     Creates a new <see cref="TransmissionClient" />
	/// </summary>
	/// <param name="settings">Connection settings</param>
	/// <param name="httpClient">http client used to talk to the RPC endpoint</param>
	/// <param name="logger">The logger of this <see cref="TransmissionClient" /></param>
	public TransmissionClient(TransmissionSettings settings, HttpClient httpClient, ILogger<TransmissionClient> logger)
	{
		_settings = settings;
		_httpClient = httpClient;
		_logger = logger;
		_rpcUrl = $"{(settings.UseSsl ? "https" : "http")}://{settings.Host}:{settings.Port}/transmission/rpc";
	}

	/// <inheritdoc />
	public Protocol Protocol
		=> Protocol.BITTORRENT;

	/// <inheritdoc />
	public async Task<string> AddDownloadAsync(ReleaseInfo release, CancellationToken cancellationToken = default)
	{
		if (release.DownloadUrl == null)
			throw new DownloadClientException($"Release {release.Title} has no download url");

		var arguments = new JsonObject { ["filename"] = release.DownloadUrl };
		var result = await CallAsync("torrent-add", arguments, cancellationToken);

		var torrent = result["torrent-added"] ?? result["torrent-duplicate"];
		var hash = torrent?["hashString"]?.GetValue<string>();

		if (hash == null)
			throw new DownloadClientException($"Transmission did not return a torrent hash for {release.Title}");

		return hash;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken = default)
	{
		var arguments = new JsonObject { ["fields"] = new JsonArray(TorrentFields.Select(field => JsonValue.Create(field)!).ToArray()) };
		var result = await CallAsync("torrent-get", arguments, cancellationToken);

		var items = new List<DownloadClientItem>();
		if (result["torrents"] is JsonArray torrents)
			foreach (var torrent in torrents)
				items.Add(MapItem(torrent));

		return items;
	}

	/// <inheritdoc />
	public async Task RemoveItemAsync(string downloadId, bool deleteData, CancellationToken cancellationToken = default)
	{
		var arguments = new JsonObject
		{
			["ids"] = new JsonArray(JsonValue.Create(downloadId)),
			["delete-local-data"] = deleteData
		};

		await CallAsync("torrent-remove", arguments, cancellationToken);
	}

	/// <inheritdoc />
	public async Task TestAsync(CancellationToken cancellationToken = default)
		=> await CallAsync("session-get", null, cancellationToken);

	private async Task<JsonNode> CallAsync(string method, JsonObject? arguments, CancellationToken cancellationToken)
	{
		var payload = new JsonObject { ["method"] = method };
		if (arguments != null) payload["arguments"] = arguments;

		var response = await SendRequestAsync(payload, cancellationToken);

		if (response.StatusCode == HttpStatusCode.Conflict)
		{
			if (response.Headers.TryGetValues("X-Transmission-Session-Id", out var values))
				_sessionId = values.FirstOrDefault();

			_logger.LogDebug("Refreshed Transmission session id after a 409 response");
			response.Dispose();
			response = await SendRequestAsync(payload, cancellationToken);
		}

		using (response)
		{
			if (!response.IsSuccessStatusCode)
				throw new DownloadClientException($"Transmission request failed with status {(int)response.StatusCode}");

			var body = await response.Content.ReadFromJsonAsync<JsonNode>(JsonOptions, cancellationToken)
				?? throw new DownloadClientException("Transmission returned an empty response");

			var result = body["result"]?.GetValue<string>();
			if (result != "success")
				throw new DownloadClientException($"Transmission request failed: {result}");

			return body["arguments"] ?? new JsonObject();
		}
	}

	private async Task<HttpResponseMessage> SendRequestAsync(JsonObject payload, CancellationToken cancellationToken)
	{
		var request = new HttpRequestMessage(HttpMethod.Post, _rpcUrl)
		{
			Content = JsonContent.Create(payload, options: JsonOptions)
		};

		if (_sessionId != null) request.Headers.Add("X-Transmission-Session-Id", _sessionId);
		if (_settings.Username != null)
			request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
				Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.Username}:{_settings.Password}")));

		return await _httpClient.SendAsync(request, cancellationToken);
	}

	private DownloadClientItem MapItem(JsonNode? torrent)
	{
		var hash = torrent?["hashString"]?.GetValue<string>() ?? torrent?["id"]?.GetValue<int>().ToString() ?? string.Empty;
		var eta = torrent?["eta"]?.GetValue<long>() ?? -1;
		var errorString = torrent?["errorString"]?.GetValue<string>();
		var status = torrent?["status"]?.GetValue<int>() ?? 0;

		return new DownloadClientItem
		{
			DownloadId = hash,
			Title = torrent?["name"]?.GetValue<string>() ?? string.Empty,
			TotalSize = torrent?["totalSize"]?.GetValue<long>() ?? 0,
			RemainingSize = torrent?["leftUntilDone"]?.GetValue<long>() ?? 0,
			RemainingTime = eta >= 0 ? TimeSpan.FromSeconds(eta) : null,
			Status = string.IsNullOrEmpty(errorString) ? MapStatus(status) : DownloadItemStatus.FAILED,
			OutputPath = torrent?["downloadDir"]?.GetValue<string>(),
			Category = _settings.Category,
			Message = string.IsNullOrEmpty(errorString) ? null : errorString
		};
	}

	private static DownloadItemStatus MapStatus(int status)
		=> status switch
		{
			0 => DownloadItemStatus.PAUSED,
			1 or 3 => DownloadItemStatus.QUEUED,
			2 or 4 => DownloadItemStatus.DOWNLOADING,
			5 or 6 => DownloadItemStatus.COMPLETED,
			_ => DownloadItemStatus.WARNING
		};
}
