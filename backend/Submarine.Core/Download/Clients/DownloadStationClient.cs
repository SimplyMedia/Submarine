using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Submarine.Core.Indexer;
using Submarine.Core.Provider;

namespace Submarine.Core.Download.Clients;

/// <summary>
///     Download client talking to Synology DownloadStation over its web api
/// </summary>
public class DownloadStationClient : IDownloadClient
{
	private const string TaskApi = "SYNO.DownloadStation.Task";

	private readonly DownloadStationSettings _settings;
	private readonly HttpClient _httpClient;
	private readonly ILogger<DownloadStationClient> _logger;

	private string? _sid;

	/// <summary>
	///     Creates a new instance of <see cref="DownloadStationClient" />
	/// </summary>
	/// <param name="settings">connection settings</param>
	/// <param name="httpClient">http client used for the web api calls</param>
	/// <param name="logger">logger</param>
	public DownloadStationClient(DownloadStationSettings settings, HttpClient httpClient,
		ILogger<DownloadStationClient> logger)
	{
		_settings = settings;
		_httpClient = httpClient;
		_logger = logger;
	}

	/// <inheritdoc />
	public Protocol Protocol => Protocol.BITTORRENT;

	/// <inheritdoc />
	public async Task<string> AddDownloadAsync(ReleaseInfo release, SeedCriteria? seedCriteria = default,
		CancellationToken cancellationToken = default)
	{
		var url = release.DownloadUrl
			?? throw new DownloadClientException("Release has no download url");

		if (seedCriteria != null)
			_logger.LogDebug("seed criteria not supported by DownloadStation");

		await ApiGetAsync("DownloadStation/task.cgi", new Dictionary<string, string>
		{
			["api"] = TaskApi, ["version"] = "1", ["method"] = "create", ["uri"] = url
		}, cancellationToken);

		var items = await GetItemsAsync(cancellationToken);

		return items.FirstOrDefault(i => i.Title == release.Title)?.DownloadId
			?? throw new DownloadClientException("DownloadStation task was created but could not be resolved");
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken = default)
	{
		var data = await ApiGetAsync("DownloadStation/task.cgi", new Dictionary<string, string>
		{
			["api"] = TaskApi, ["version"] = "1", ["method"] = "list", ["additional"] = "transfer,detail"
		}, cancellationToken);

		return data.GetProperty("tasks").EnumerateArray().Select(MapItem).ToList();
	}

	/// <inheritdoc />
	public async Task RemoveItemAsync(string downloadId, bool deleteData,
		CancellationToken cancellationToken = default)
		=> await ApiGetAsync("DownloadStation/task.cgi", new Dictionary<string, string>
		{
			["api"] = TaskApi, ["version"] = "1", ["method"] = "delete", ["id"] = downloadId,
			["force_complete"] = "false"
		}, cancellationToken);

	/// <inheritdoc />
	public async Task TestAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			var (_, errorCode) = await SendAsync("query.cgi", new Dictionary<string, string>
			{
				["api"] = "SYNO.API.Info", ["version"] = "1", ["method"] = "query",
				["query"] = "SYNO.API.Auth,SYNO.DownloadStation.Task"
			}, cancellationToken);

			if (errorCode is not null)
				throw new DownloadClientException($"DownloadStation returned error {errorCode}");
		}
		catch (DownloadClientException)
		{
			throw;
		}
		catch (Exception e)
		{
			throw new DownloadClientException("Failed to reach DownloadStation", e);
		}
	}

	private static DownloadClientItem MapItem(JsonElement task)
	{
		var size = task.GetProperty("size").GetInt64();
		long downloaded = 0;
		string? destination = null;

		if (task.TryGetProperty("additional", out var additional))
		{
			if (additional.TryGetProperty("transfer", out var transfer)
				&& transfer.TryGetProperty("size_downloaded", out var sizeDownloaded))
				downloaded = sizeDownloaded.GetInt64();

			if (additional.TryGetProperty("detail", out var detail)
				&& detail.TryGetProperty("destination", out var dest))
				destination = dest.GetString();
		}

		return new DownloadClientItem
		{
			DownloadId = task.GetProperty("id").GetString()!,
			Title = task.GetProperty("title").GetString()!,
			TotalSize = size,
			RemainingSize = size - downloaded,
			Status = MapStatus(task.GetProperty("status").GetString()),
			OutputPath = destination
		};
	}

	private static DownloadItemStatus MapStatus(string? status)
		=> status switch
		{
			"downloading" => DownloadItemStatus.DOWNLOADING,
			"paused" => DownloadItemStatus.PAUSED,
			"finished" => DownloadItemStatus.COMPLETED,
			"error" => DownloadItemStatus.FAILED,
			_ => DownloadItemStatus.QUEUED
		};

	private async Task<JsonElement> ApiGetAsync(string cgi, Dictionary<string, string> query,
		CancellationToken cancellationToken)
	{
		await EnsureSessionAsync(cancellationToken);

		var (data, errorCode) = await SendAsync(cgi, WithSid(query), cancellationToken);

		if (errorCode is 105 or 106 or 107)
		{
			_sid = null;
			await EnsureSessionAsync(cancellationToken);
			(data, errorCode) = await SendAsync(cgi, WithSid(query), cancellationToken);
		}

		if (errorCode is not null)
			throw new DownloadClientException($"DownloadStation returned error {errorCode}");

		return data;
	}

	private async Task EnsureSessionAsync(CancellationToken cancellationToken)
	{
		if (_sid is not null)
			return;

		var (data, errorCode) = await SendAsync("auth.cgi", new Dictionary<string, string>
		{
			["api"] = "SYNO.API.Auth", ["version"] = "3", ["method"] = "login",
			["account"] = _settings.Username, ["passwd"] = _settings.Password,
			["session"] = "DownloadStation", ["format"] = "sid"
		}, cancellationToken);

		if (errorCode is not null || !data.TryGetProperty("sid", out var sid))
			throw new DownloadClientException(
				$"DownloadStation authentication failed{(errorCode is null ? string.Empty : $" (error {errorCode})")}");

		_sid = sid.GetString();
	}

	private Dictionary<string, string> WithSid(Dictionary<string, string> query)
		=> new(query) { ["_sid"] = _sid! };

	private async Task<(JsonElement Data, int? ErrorCode)> SendAsync(string cgi, Dictionary<string, string> query,
		CancellationToken cancellationToken)
	{
		var queryString = string.Join('&',
			query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));

		using var response =
			await _httpClient.GetAsync($"{_settings.BaseUrl}/webapi/{cgi}?{queryString}", cancellationToken);
		response.EnsureSuccessStatusCode();

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		var root = document.RootElement;

		if (root.TryGetProperty("success", out var success) && success.GetBoolean())
			return (root.TryGetProperty("data", out var data) ? data.Clone() : default, null);

		var code = root.TryGetProperty("error", out var error) && error.TryGetProperty("code", out var c)
			? c.GetInt32()
			: -1;

		return (default, code);
	}
}
