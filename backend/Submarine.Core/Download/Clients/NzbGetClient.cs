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
///     <see cref="IDownloadClient" /> for the NZBGet JSON-RPC API
/// </summary>
public class NzbGetClient : IDownloadClient
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly NzbGetSettings _settings;
	private readonly HttpClient _httpClient;
	private readonly ILogger<NzbGetClient> _logger;
	private readonly string _rpcUrl;

	/// <summary>
	///     Creates a new <see cref="NzbGetClient" />
	/// </summary>
	/// <param name="settings">Connection settings</param>
	/// <param name="httpClient">http client used to talk to the JSON-RPC endpoint</param>
	/// <param name="logger">The logger of this <see cref="NzbGetClient" /></param>
	public NzbGetClient(NzbGetSettings settings, HttpClient httpClient, ILogger<NzbGetClient> logger)
	{
		_settings = settings;
		_httpClient = httpClient;
		_logger = logger;
		_rpcUrl = $"{(settings.UseSsl ? "https" : "http")}://{settings.Host}:{settings.Port}/jsonrpc";
	}

	/// <inheritdoc />
	public Protocol Protocol
		=> Protocol.USENET;

	/// <inheritdoc />
	public async Task<string> AddDownloadAsync(ReleaseInfo release, CancellationToken cancellationToken = default)
	{
		if (release.DownloadUrl == null)
			throw new DownloadClientException($"Release {release.Title} has no download url");

		var parameters = new JsonArray(
			JsonValue.Create($"{SanitizeFileName(release.Title)}.nzb"),
			JsonValue.Create(release.DownloadUrl),
			JsonValue.Create(_settings.Category ?? string.Empty),
			JsonValue.Create(0),
			JsonValue.Create(false),
			JsonValue.Create(false),
			JsonValue.Create(string.Empty),
			JsonValue.Create(0),
			JsonValue.Create("score"),
			new JsonArray());

		var result = await CallAsync("append", parameters, cancellationToken);
		var id = result?.GetValue<int>();

		if (id is null or <= 0)
			throw new DownloadClientException($"NZBGet did not accept the download for {release.Title}");

		return id.Value.ToString();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken = default)
	{
		var items = new List<DownloadClientItem>();

		var groups = await CallAsync("listgroups", new JsonArray(JsonValue.Create(0)), cancellationToken);
		if (groups is JsonArray groupArray)
			foreach (var group in groupArray)
				if (group != null)
					items.Add(MapGroup(group));

		var history = await CallAsync("history", new JsonArray(JsonValue.Create(false)), cancellationToken);
		if (history is JsonArray historyArray)
			foreach (var entry in historyArray)
				if (entry != null)
					items.Add(MapHistoryEntry(entry));

		return items;
	}

	/// <inheritdoc />
	public async Task RemoveItemAsync(string downloadId, bool deleteData, CancellationToken cancellationToken = default)
	{
		if (!int.TryParse(downloadId, out var id))
			throw new DownloadClientException($"Invalid NZBGet download id {downloadId}");

		var command = deleteData ? "GroupFinalDelete" : "GroupDelete";
		var editQueueParams = new JsonArray(
			JsonValue.Create(command),
			JsonValue.Create(0),
			JsonValue.Create(string.Empty),
			new JsonArray(JsonValue.Create(id)));

		var result = await CallAsync("editqueue", editQueueParams, cancellationToken);
		if (result?.GetValue<bool>() == true) return;

		_logger.LogDebug("Download {DownloadId} was not found in the NZBGet queue, falling back to history", downloadId);
		var historyParams = new JsonArray(new JsonArray(JsonValue.Create(id)), JsonValue.Create(deleteData));
		var historyResult = await CallAsync("historydelete", historyParams, cancellationToken);

		if (historyResult?.GetValue<bool>() != true)
			throw new DownloadClientException($"NZBGet could not remove download {downloadId}");
	}

	/// <inheritdoc />
	public async Task TestAsync(CancellationToken cancellationToken = default)
	{
		var result = await CallAsync("version", null, cancellationToken);
		if (result == null)
			throw new DownloadClientException("NZBGet did not return a version");
	}

	private async Task<JsonNode?> CallAsync(string method, JsonArray? parameters, CancellationToken cancellationToken)
	{
		var payload = new JsonObject { ["method"] = method };
		if (parameters != null) payload["params"] = parameters;

		var request = new HttpRequestMessage(HttpMethod.Post, _rpcUrl)
		{
			Content = JsonContent.Create(payload, options: JsonOptions)
		};

		if (_settings.Username != null)
			request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
				Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.Username}:{_settings.Password}")));

		var response = await _httpClient.SendAsync(request, cancellationToken);
		if (!response.IsSuccessStatusCode)
			throw new DownloadClientException($"NZBGet request failed with status {(int)response.StatusCode}");

		var body = await response.Content.ReadFromJsonAsync<JsonNode>(JsonOptions, cancellationToken)
			?? throw new DownloadClientException("NZBGet returned an empty response");

		if (body["error"] is { } error)
			throw new DownloadClientException($"NZBGet request failed: {error["message"]?.GetValue<string>() ?? error.ToJsonString()}");

		return body["result"];
	}

	private static DownloadClientItem MapGroup(JsonNode group)
		=> new()
		{
			DownloadId = group["NZBID"]?.GetValue<int>().ToString() ?? string.Empty,
			Title = group["NZBName"]?.GetValue<string>() ?? string.Empty,
			TotalSize = CombineSize(group["FileSizeLo"]?.GetValue<long>() ?? 0, group["FileSizeHi"]?.GetValue<long>() ?? 0),
			RemainingSize = CombineSize(group["RemainingSizeLo"]?.GetValue<long>() ?? 0,
				group["RemainingSizeHi"]?.GetValue<long>() ?? 0),
			Status = group["Status"]?.GetValue<string>() switch
			{
				"PAUSED" => DownloadItemStatus.PAUSED,
				"DOWNLOADING" => DownloadItemStatus.DOWNLOADING,
				_ => DownloadItemStatus.QUEUED
			},
			OutputPath = group["DestDir"]?.GetValue<string>(),
			Category = group["Category"]?.GetValue<string>()
		};

	private static DownloadClientItem MapHistoryEntry(JsonNode entry)
	{
		var status = entry["Status"]?.GetValue<string>() ?? string.Empty;

		return new DownloadClientItem
		{
			DownloadId = entry["NZBID"]?.GetValue<int>().ToString() ?? string.Empty,
			Title = entry["Name"]?.GetValue<string>() ?? string.Empty,
			TotalSize = CombineSize(entry["FileSizeLo"]?.GetValue<long>() ?? 0, entry["FileSizeHi"]?.GetValue<long>() ?? 0),
			RemainingSize = 0,
			Status = status.StartsWith("SUCCESS", StringComparison.OrdinalIgnoreCase) ? DownloadItemStatus.COMPLETED
				: status.StartsWith("FAILURE", StringComparison.OrdinalIgnoreCase) ? DownloadItemStatus.FAILED
				: DownloadItemStatus.WARNING,
			OutputPath = entry["DestDir"]?.GetValue<string>(),
			Category = entry["Category"]?.GetValue<string>(),
			Message = status
		};
	}

	// NZBGet reports 64 bit sizes split into a signed high and low 32 bit part
	private static long CombineSize(long lo, long hi)
		=> (hi << 32) | (lo & 0xFFFFFFFF);

	private static string SanitizeFileName(string title)
	{
		var invalid = Path.GetInvalidFileNameChars();
		return string.Concat(title.Select(c => invalid.Contains(c) ? '_' : c));
	}
}
