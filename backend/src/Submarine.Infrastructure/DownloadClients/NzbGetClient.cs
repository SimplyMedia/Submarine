using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> for the NZBGet JSON-RPC api
/// </summary>
public sealed class NzbGetClient(
	NzbGetSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : DownloadClientBase<NzbGetSettings>(settings, clientId, clientName, httpClient)
{
	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.NZBGET;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.USENET;

	private string RpcUrl => Settings.Build("/jsonrpc");

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		var nzbData = await GetNzbDataAsync(release, cancellationToken);

		// the release carries no publish date, so recent and older releases cannot be distinguished;
		// the recent priority is applied to every add
		var parameters = new JsonArray(
			JsonValue.Create($"{SanitizeFileName(release.Title)}.nzb"),
			JsonValue.Create(Convert.ToBase64String(nzbData)),
			JsonValue.Create(Settings.Category ?? string.Empty),
			JsonValue.Create(MapPriority(Settings.RecentPriority)),
			JsonValue.Create(false),
			JsonValue.Create(Settings.AddPaused),
			JsonValue.Create(string.Empty),
			JsonValue.Create(0),
			JsonValue.Create("SCORE"),
			new JsonArray());

		var result = await CallAsync("append", parameters, cancellationToken);
		var id = result?.GetValue<int>();

		if (id is null or <= 0)
			throw new DownloadClientException(
				$"NZBGet {ClientName} did not accept the download for {release.Title}");

		return id.Value.ToString(CultureInfo.InvariantCulture);
	}

	private static int MapPriority(DownloadClientPriority priority)
		=> priority switch
		{
			DownloadClientPriority.PAUSED => -50,
			DownloadClientPriority.LOW => -10,
			DownloadClientPriority.NORMAL => 0,
			DownloadClientPriority.HIGH => 10,
			DownloadClientPriority.FORCED => 100,
			_ => 0
		};

	/// <inheritdoc />
	protected override async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(
		CancellationToken cancellationToken)
	{
		var items = new List<DownloadClientItem>();

		var groups = await CallAsync("listgroups", new JsonArray(JsonValue.Create(0)), cancellationToken);
		if (groups is JsonArray groupArray)
			items.AddRange(groupArray.Where(group => group is not null).Select(group => MapGroup(group!)));

		var history = await CallAsync("history", new JsonArray(JsonValue.Create(false)), cancellationToken);
		if (history is JsonArray historyArray)
			items.AddRange(historyArray.Where(entry => entry is not null).Select(entry => MapHistoryEntry(entry!)));

		return items;
	}

	/// <inheritdoc />
	protected override async Task RemoveAsyncCore(string downloadId, bool deleteData,
		CancellationToken cancellationToken)
	{
		if (!int.TryParse(downloadId, CultureInfo.InvariantCulture, out var id))
			throw new DownloadClientException($"NZBGet {ClientName} received an invalid download id {downloadId}");

		var command = deleteData ? "GroupFinalDelete" : "GroupDelete";
		var result = await CallAsync("editqueue", new JsonArray(
			JsonValue.Create(command),
			JsonValue.Create(0),
			JsonValue.Create(string.Empty),
			new JsonArray(JsonValue.Create(id))), cancellationToken);

		if (result?.GetValue<bool>() == true)
			return;

		var historyResult = await CallAsync("historydelete", new JsonArray(
			new JsonArray(JsonValue.Create(id)),
			JsonValue.Create(deleteData)), cancellationToken);

		if (historyResult?.GetValue<bool>() != true)
			throw new DownloadClientException($"NZBGet {ClientName} could not remove download {downloadId}");
	}

	/// <inheritdoc />
	protected override async Task TestAsyncCore(CancellationToken cancellationToken)
	{
		var result = await CallAsync("version", null, cancellationToken);
		if (result is null)
			throw new DownloadClientException(
				$"NZBGet {ClientName} did not return a version, check the username and password");
	}

	private async Task<JsonNode?> CallAsync(string method, JsonArray? parameters,
		CancellationToken cancellationToken)
	{
		var payload = new JsonObject { ["method"] = method };
		if (parameters is not null)
			payload["params"] = parameters;

		using var response = await SendAsync(
			() =>
			{
				var request = new HttpRequestMessage(HttpMethod.Post, RpcUrl)
				{
					Content = JsonContent.Create(payload, options: JsonOptions)
				};

				if (BasicAuth(Settings.Username, Settings.Password) is { } auth)
					request.Headers.Authorization = auth;

				return request;
			}, cancellationToken);

		EnsureSuccess(response, $"NZBGet {ClientName} request");

		var body = await ReadJsonAsync<JsonNode>(response.Content, cancellationToken);

		if (body["error"] is { } error)
			throw new DownloadClientException(
				$"NZBGet {ClientName} request failed: {error["message"]?.GetValue<string>() ?? error.ToJsonString()}");

		return body["result"];
	}

	private DownloadClientItem MapGroup(JsonNode group)
	{
		var category = group["Category"]?.GetValue<string>();

		return new DownloadClientItem
		{
			DownloadId = group["NZBID"]?.GetValue<int>().ToString(CultureInfo.InvariantCulture) ?? string.Empty,
			Title = group["NZBName"]?.GetValue<string>() ?? string.Empty,
			TotalSize = CombineSize(group["FileSizeLo"]?.GetValue<long>() ?? 0,
				group["FileSizeHi"]?.GetValue<long>() ?? 0),
			RemainingSize = CombineSize(group["RemainingSizeLo"]?.GetValue<long>() ?? 0,
				group["RemainingSizeHi"]?.GetValue<long>() ?? 0),
			Status = group["Status"]?.GetValue<string>() switch
			{
				"PAUSED" => DownloadItemStatus.PAUSED,
				"DOWNLOADING" or "FETCHING" => DownloadItemStatus.DOWNLOADING,
				_ => DownloadItemStatus.QUEUED
			},
			OutputPath = group["DestDir"]?.GetValue<string>(),
			Category = string.IsNullOrEmpty(category) ? null : category,
			IsReadOnly = Settings.Category is { Length: > 0 } && category != Settings.Category
		};
	}

	private DownloadClientItem MapHistoryEntry(JsonNode entry)
	{
		var status = entry["Status"]?.GetValue<string>() ?? string.Empty;
		var category = entry["Category"]?.GetValue<string>();

		return new DownloadClientItem
		{
			DownloadId = entry["NZBID"]?.GetValue<int>().ToString(CultureInfo.InvariantCulture) ?? string.Empty,
			Title = entry["Name"]?.GetValue<string>() ?? string.Empty,
			TotalSize = CombineSize(entry["FileSizeLo"]?.GetValue<long>() ?? 0,
				entry["FileSizeHi"]?.GetValue<long>() ?? 0),
			RemainingSize = 0,
			Status = status.StartsWith("SUCCESS", StringComparison.OrdinalIgnoreCase)
				? DownloadItemStatus.COMPLETED
				: status.StartsWith("FAILURE", StringComparison.OrdinalIgnoreCase)
					? DownloadItemStatus.FAILED
					: DownloadItemStatus.WARNING,
			OutputPath = entry["DestDir"]?.GetValue<string>(),
			Category = string.IsNullOrEmpty(category) ? null : category,
			Message = status,
			IsReadOnly = Settings.Category is { Length: > 0 } && category != Settings.Category
		};
	}

	// NZBGet reports 64 bit sizes split into a signed high and low 32 bit part
	private static long CombineSize(long lo, long hi)
		=> (hi << 32) | (lo & 0xFFFFFFFF);
}
