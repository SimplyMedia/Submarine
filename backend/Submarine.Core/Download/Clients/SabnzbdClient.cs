using System.Globalization;
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
///     <see cref="IDownloadClient" /> for the SABnzbd API
/// </summary>
public class SabnzbdClient : IDownloadClient
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly SabnzbdSettings _settings;
	private readonly HttpClient _httpClient;
	private readonly ILogger<SabnzbdClient> _logger;
	private readonly string _baseUrl;

	/// <summary>
	///     Creates a new <see cref="SabnzbdClient" />
	/// </summary>
	/// <param name="settings">Connection settings</param>
	/// <param name="httpClient">http client used to talk to the API</param>
	/// <param name="logger">The logger of this <see cref="SabnzbdClient" /></param>
	public SabnzbdClient(SabnzbdSettings settings, HttpClient httpClient, ILogger<SabnzbdClient> logger)
	{
		_settings = settings;
		_httpClient = httpClient;
		_logger = logger;
		_baseUrl = $"{(settings.UseSsl ? "https" : "http")}://{settings.Host}:{settings.Port}/api";
	}

	/// <inheritdoc />
	public Protocol Protocol
		=> Protocol.USENET;

	/// <inheritdoc />
	public async Task<string> AddDownloadAsync(ReleaseInfo release, SeedCriteria? seedCriteria = default,
		CancellationToken cancellationToken = default)
	{
		if (release.DownloadUrl == null)
			throw new DownloadClientException($"Release {release.Title} has no download url");

		var extra = new Dictionary<string, string> { ["name"] = release.DownloadUrl };
		if (_settings.Category != null) extra["cat"] = _settings.Category;

		var body = await RequestAsync("addurl", extra, cancellationToken);
		EnsureSuccess(body);

		var nzoId = body["nzo_ids"] is JsonArray { Count: > 0 } nzoIds ? nzoIds[0]?.GetValue<string>() : null;
		if (nzoId == null)
			throw new DownloadClientException($"SABnzbd did not return an nzo id for {release.Title}");

		return nzoId;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken = default)
	{
		var items = new List<DownloadClientItem>();

		var queue = await RequestAsync("queue", null, cancellationToken);
		if (queue["queue"]?["slots"] is JsonArray queueSlots)
			foreach (var slot in queueSlots)
				if (slot != null)
					items.Add(MapQueueSlot(slot));

		var history = await RequestAsync("history", null, cancellationToken);
		if (history["history"]?["slots"] is JsonArray historySlots)
			foreach (var slot in historySlots)
				if (slot != null)
					items.Add(MapHistorySlot(slot));

		return items;
	}

	/// <inheritdoc />
	public async Task RemoveItemAsync(string downloadId, bool deleteData, CancellationToken cancellationToken = default)
	{
		var extra = new Dictionary<string, string>
		{
			["name"] = "delete",
			["value"] = downloadId,
			["del_files"] = deleteData ? "1" : "0"
		};

		var queueResult = await RequestAsync("queue", extra, cancellationToken);
		if (queueResult["status"]?.GetValue<bool>() == true) return;

		_logger.LogDebug("Download {DownloadId} was not found in the SABnzbd queue, falling back to history", downloadId);
		var historyResult = await RequestAsync("history", extra, cancellationToken);
		if (historyResult["status"]?.GetValue<bool>() != true)
			throw new DownloadClientException($"SABnzbd could not remove download {downloadId}");
	}

	/// <inheritdoc />
	public async Task TestAsync(CancellationToken cancellationToken = default)
	{
		var body = await RequestAsync("version", null, cancellationToken);
		EnsureSuccess(body);

		if (body["version"] == null)
			throw new DownloadClientException("SABnzbd did not return a version");
	}

	private static void EnsureSuccess(JsonNode body)
	{
		if (body["status"]?.GetValue<bool>() == false)
			throw new DownloadClientException($"SABnzbd request failed: {body["error"]?.GetValue<string>() ?? "unknown error"}");
	}

	private async Task<JsonNode> RequestAsync(string mode, IDictionary<string, string>? extra,
		CancellationToken cancellationToken)
	{
		var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(mode, extra));
		if (_settings.Username != null)
			request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
				Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.Username}:{_settings.Password}")));

		using var response = await _httpClient.SendAsync(request, cancellationToken);
		if (!response.IsSuccessStatusCode)
			throw new DownloadClientException($"SABnzbd request failed with status {(int)response.StatusCode}");

		return await response.Content.ReadFromJsonAsync<JsonNode>(JsonOptions, cancellationToken)
			?? throw new DownloadClientException("SABnzbd returned an empty response");
	}

	private string BuildUrl(string mode, IDictionary<string, string>? extra)
	{
		var query = new List<string> { "output=json", $"apikey={Uri.EscapeDataString(_settings.ApiKey)}", $"mode={mode}" };
		if (extra != null)
			query.AddRange(extra.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));

		return $"{_baseUrl}?{string.Join('&', query)}";
	}

	private static DownloadClientItem MapQueueSlot(JsonNode slot)
	{
		var totalMb = ParseDouble(slot["mb"]?.GetValue<string>());
		var leftMb = ParseDouble(slot["mbleft"]?.GetValue<string>());

		return new DownloadClientItem
		{
			DownloadId = slot["nzo_id"]?.GetValue<string>() ?? string.Empty,
			Title = slot["filename"]?.GetValue<string>() ?? string.Empty,
			TotalSize = (long)(totalMb * 1024 * 1024),
			RemainingSize = (long)(leftMb * 1024 * 1024),
			RemainingTime = ParseTimeSpan(slot["timeleft"]?.GetValue<string>()),
			Status = MapQueueStatus(slot["status"]?.GetValue<string>()),
			Category = slot["cat"]?.GetValue<string>()
		};
	}

	private static DownloadClientItem MapHistorySlot(JsonNode slot)
	{
		var status = slot["status"]?.GetValue<string>();
		var failMessage = slot["fail_message"]?.GetValue<string>();

		return new DownloadClientItem
		{
			DownloadId = slot["nzo_id"]?.GetValue<string>() ?? string.Empty,
			Title = slot["name"]?.GetValue<string>() ?? string.Empty,
			TotalSize = slot["bytes"]?.GetValue<long>() ?? 0,
			RemainingSize = 0,
			Status = status switch
			{
				"Completed" => DownloadItemStatus.COMPLETED,
				"Failed" => DownloadItemStatus.FAILED,
				_ => DownloadItemStatus.WARNING
			},
			OutputPath = slot["storage"]?.GetValue<string>(),
			Category = slot["cat"]?.GetValue<string>(),
			Message = string.IsNullOrEmpty(failMessage) ? null : failMessage
		};
	}

	private static DownloadItemStatus MapQueueStatus(string? status)
		=> status switch
		{
			"Downloading" => DownloadItemStatus.DOWNLOADING,
			"Paused" => DownloadItemStatus.PAUSED,
			_ => DownloadItemStatus.QUEUED
		};

	private static double ParseDouble(string? value)
		=> double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;

	private static TimeSpan? ParseTimeSpan(string? value)
		=> TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var result) ? result : null;
}
