using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> for Synology Download Station over its web api
/// </summary>
public sealed class DownloadStationClient(
	DownloadStationSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : DownloadClientBase<DownloadStationSettings>(settings, clientId, clientName, httpClient)
{
	private const string TaskApi = "SYNO.DownloadStation.Task";
	private const string AuthApi = "SYNO.API.Auth";

	private string? _sid;

	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.DOWNLOAD_STATION;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.BITTORRENT;

	private string WebApi => Settings.Build("/webapi");

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		var url = release.MagnetUrl ?? release.DownloadUrl
			?? throw new DownloadClientException(
				$"Download Station {ClientName} requires a magnet or download url for {release.Title}");

		var query = new Dictionary<string, string>
		{
			["api"] = TaskApi,
			["version"] = "1",
			["method"] = "create",
			["uri"] = url
		};
		if (Settings.Directory is { Length: > 0 })
			query["destination"] = Settings.Directory;
		else if (Settings.Category is { Length: > 0 })
			query["destination"] = Settings.Category;

		await ApiGetAsync("DownloadStation/task.cgi", query, cancellationToken);

		// create does not return the task id, resolve it by listing and matching the title
		var items = await GetItemsAsyncCore(cancellationToken);

		return items.FirstOrDefault(item => item.Title == release.Title)?.DownloadId
			?? throw new DownloadClientException(
				$"Download Station {ClientName} created the task but it could not be resolved");
	}

	/// <inheritdoc />
	protected override async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(
		CancellationToken cancellationToken)
	{
		var data = await ApiGetAsync("DownloadStation/task.cgi", new Dictionary<string, string>
		{
			["api"] = TaskApi, ["version"] = "1", ["method"] = "list", ["additional"] = "transfer,detail"
		}, cancellationToken);

		return [.. data.GetProperty("tasks").EnumerateArray().Select(MapItem)];
	}

	/// <inheritdoc />
	protected override Task RemoveAsyncCore(string downloadId, bool deleteData, CancellationToken cancellationToken)
		=> ApiGetAsync("DownloadStation/task.cgi", new Dictionary<string, string>
		{
			["api"] = TaskApi, ["version"] = "1", ["method"] = "delete", ["id"] = downloadId,
			["force_complete"] = "false"
		}, cancellationToken);

	/// <inheritdoc />
	protected override async Task TestAsyncCore(CancellationToken cancellationToken)
	{
		var (_, errorCode) = await SendAsync("query.cgi", new Dictionary<string, string>
		{
			["api"] = "SYNO.API.Info", ["version"] = "1", ["method"] = "query",
			["query"] = $"{AuthApi},{TaskApi}"
		}, cancellationToken);

		if (errorCode is not null)
			throw new DownloadClientException(
				$"Download Station {ClientName} returned error {errorCode}, check the address and credentials");
	}

	private DownloadClientItem MapItem(JsonElement task)
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
				&& detail.TryGetProperty("destination", out var destinationElement))
				destination = destinationElement.GetString();
		}

		return new DownloadClientItem
		{
			DownloadId = task.GetProperty("id").GetString() ?? string.Empty,
			Title = task.GetProperty("title").GetString() ?? string.Empty,
			TotalSize = size,
			RemainingSize = size - downloaded,
			Status = MapStatus(task.GetProperty("status").GetString()),
			OutputPath = destination,
			IsReadOnly = Settings.Directory is { Length: > 0 } && destination != Settings.Directory
		};
	}

	private static DownloadItemStatus MapStatus(string? status)
		=> status switch
		{
			"downloading" or "hashing" or "connecting" => DownloadItemStatus.DOWNLOADING,
			"paused" => DownloadItemStatus.PAUSED,
			"finished" or "seeding" => DownloadItemStatus.COMPLETED,
			"error" => DownloadItemStatus.FAILED,
			_ => DownloadItemStatus.QUEUED
		};

	private async Task<JsonElement> ApiGetAsync(string cgi, Dictionary<string, string> query,
		CancellationToken cancellationToken)
	{
		await EnsureSessionAsync(cancellationToken);

		var (data, errorCode) = await SendAsync(cgi, WithSid(query), cancellationToken);

		// session expired or invalid: log in again once and retry
		if (errorCode is 105 or 106 or 107)
		{
			_sid = null;
			await EnsureSessionAsync(cancellationToken);
			(data, errorCode) = await SendAsync(cgi, WithSid(query), cancellationToken);
		}

		if (errorCode is not null)
			throw new DownloadClientException($"Download Station {ClientName} returned error {errorCode}");

		return data;
	}

	private async Task EnsureSessionAsync(CancellationToken cancellationToken)
	{
		if (_sid is not null)
			return;

		var (data, errorCode) = await SendAsync("auth.cgi", new Dictionary<string, string>
		{
			["api"] = AuthApi, ["version"] = "3", ["method"] = "login",
			["account"] = Settings.Username, ["passwd"] = Settings.Password,
			["session"] = "DownloadStation", ["format"] = "sid"
		}, cancellationToken);

		if (errorCode is not null || data.ValueKind != JsonValueKind.Object
			|| !data.TryGetProperty("sid", out var sidElement))
			throw new DownloadClientException(
				$"Download Station {ClientName} authentication failed"
				+ (errorCode is null ? string.Empty : $" (error {errorCode})")
				+ ", check the username and password");

		_sid = sidElement.GetString();
	}

	private Dictionary<string, string> WithSid(Dictionary<string, string> query)
		=> new(query) { ["_sid"] = _sid! };

	private async Task<(JsonElement Data, int? ErrorCode)> SendAsync(string cgi, Dictionary<string, string> query,
		CancellationToken cancellationToken)
	{
		var queryString = string.Join('&', query.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));

		using var response = await SendAsync(
			() => new HttpRequestMessage(HttpMethod.Get, $"{WebApi}/{cgi}?{queryString}"), cancellationToken);
		EnsureSuccess(response, $"Download Station {ClientName} request");

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		var root = document.RootElement;

		if (root.TryGetProperty("success", out var success) && success.GetBoolean())
			return (root.TryGetProperty("data", out var data) ? data.Clone() : default, null);

		var code = root.TryGetProperty("error", out var error) && error.TryGetProperty("code", out var codeElement)
			? codeElement.GetInt32()
			: -1;

		return (default, code);
	}
}
