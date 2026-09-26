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
///     <see cref="IDownloadClient" /> for the SABnzbd api
/// </summary>
public sealed class SabnzbdClient(
	SabnzbdSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : DownloadClientBase<SabnzbdSettings>(settings, clientId, clientName, httpClient)
{
	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.SABNZBD;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.USENET;

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		var nzbData = release.NzbFile is { Length: > 0 } ? release.NzbFile : null;
		var fileName = $"{SanitizeFileName(release.Title)}.nzb";

		JsonNode body;
		if (nzbData is not null)
		{
			var content = new MultipartFormDataContent
			{
				{ new ByteArrayContent(nzbData), "name", fileName },
				{ new StringContent(release.Title), "nzbname" }
			};
			foreach (var (key, value) in AddFields())
				content.Add(new StringContent(value), key);

			body = await RequestAsync("addfile", string.Empty, content, cancellationToken);
		}
		else
		{
			if (release.DownloadUrl is null)
				throw new DownloadClientException($"Release {release.Title} has no nzb file or download url");

			var query = string.Join('&',
				AddFields().Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}")
					.Prepend($"nzbname={Uri.EscapeDataString(release.Title)}")
					.Prepend($"name={Uri.EscapeDataString(release.DownloadUrl)}"));

			body = await RequestAsync("addurl", query, null, cancellationToken);
		}

		EnsureSuccess(body);

		var nzoId = body["nzo_ids"] is JsonArray { Count: > 0 } nzoIds ? nzoIds[0]?.GetValue<string>() : null;
		if (nzoId is null)
			throw new DownloadClientException(
				$"SABnzbd {ClientName} did not return an nzo id for {release.Title}");

		return nzoId;
	}

	private List<KeyValuePair<string, string>> AddFields()
	{
		var fields = new List<KeyValuePair<string, string>>();

		if (Settings.Category is { Length: > 0 })
			fields.Add(new KeyValuePair<string, string>("cat", Settings.Category));

		// the release carries no publish date, so recent and older releases cannot be distinguished;
		// the recent priority is applied to every add
		fields.Add(new KeyValuePair<string, string>("priority", ((int)Settings.RecentPriority).ToString(CultureInfo.InvariantCulture)));

		return fields;
	}

	/// <inheritdoc />
	protected override async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(
		CancellationToken cancellationToken)
	{
		var items = new List<DownloadClientItem>();

		var queue = await RequestAsync("queue", string.Empty, null, cancellationToken);
		if (queue["queue"]?["slots"] is JsonArray queueSlots)
			items.AddRange(queueSlots.Where(slot => slot is not null).Select(slot => MapQueueSlot(slot!)));

		var history = await RequestAsync("history", string.Empty, null, cancellationToken);
		if (history["history"]?["slots"] is JsonArray historySlots)
			items.AddRange(historySlots.Where(slot => slot is not null).Select(slot => MapHistorySlot(slot!)));

		return items;
	}

	/// <inheritdoc />
	protected override async Task RemoveAsyncCore(string downloadId, bool deleteData,
		CancellationToken cancellationToken)
	{
		var extra = $"name=delete&value={Uri.EscapeDataString(downloadId)}&del_files={(deleteData ? 1 : 0)}";

		var queueResult = await RequestAsync("queue", extra, null, cancellationToken);
		if (queueResult["status"]?.GetValue<bool>() == true)
			return;

		var historyResult = await RequestAsync("history", extra, null, cancellationToken);
		if (historyResult["status"]?.GetValue<bool>() != true)
			throw new DownloadClientException(
				$"SABnzbd {ClientName} could not remove download {downloadId}");
	}

	/// <inheritdoc />
	protected override async Task TestAsyncCore(CancellationToken cancellationToken)
	{
		var body = await RequestAsync("version", string.Empty, null, cancellationToken);
		EnsureSuccess(body);

		if (body["version"] is null)
			throw new DownloadClientException(
				$"SABnzbd {ClientName} did not return a version, check the api key");
	}

	private static void EnsureSuccess(JsonNode body)
	{
		if (body["status"]?.GetValue<bool>() == false)
			throw new DownloadClientException(
				$"SABnzbd request failed: {body["error"]?.GetValue<string>() ?? "unknown error"}");
	}

	private async Task<JsonNode> RequestAsync(string mode, string extraQuery, HttpContent? content,
		CancellationToken cancellationToken)
	{
		var query = $"output=json&apikey={Uri.EscapeDataString(Settings.ApiKey)}&mode={mode}";
		if (extraQuery.Length > 0)
			query += $"&{extraQuery}";

		using var response = await SendAsync(
			() =>
			{
				var request = content is null
					? new HttpRequestMessage(HttpMethod.Get, Settings.Build($"/api?{query}"))
					: new HttpRequestMessage(HttpMethod.Post, Settings.Build($"/api?{query}")) { Content = content };

				if (BasicAuth(Settings.Username, Settings.Password) is { } auth)
					request.Headers.Authorization = auth;

				return request;
			}, cancellationToken);

		EnsureSuccess(response, $"SABnzbd {ClientName} request");

		return await ReadJsonAsync<JsonNode>(response.Content, cancellationToken);
	}

	private DownloadClientItem MapQueueSlot(JsonNode slot)
	{
		var totalMb = ParseDouble(slot["mb"]?.GetValue<string>());
		var leftMb = ParseDouble(slot["mbleft"]?.GetValue<string>());
		var category = slot["cat"]?.GetValue<string>();

		return new DownloadClientItem
		{
			DownloadId = slot["nzo_id"]?.GetValue<string>() ?? string.Empty,
			Title = slot["filename"]?.GetValue<string>() ?? string.Empty,
			TotalSize = (long)(totalMb * 1024 * 1024),
			RemainingSize = (long)(leftMb * 1024 * 1024),
			RemainingTime = ParseTimeSpan(slot["timeleft"]?.GetValue<string>()),
			Status = MapQueueStatus(slot["status"]?.GetValue<string>()),
			Category = string.IsNullOrEmpty(category) ? null : category,
			IsReadOnly = Settings.Category is { Length: > 0 } && category != Settings.Category
		};
	}

	private DownloadClientItem MapHistorySlot(JsonNode slot)
	{
		var status = slot["status"]?.GetValue<string>();
		var failMessage = slot["fail_message"]?.GetValue<string>();
		var category = slot["cat"]?.GetValue<string>();

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
			Category = string.IsNullOrEmpty(category) ? null : category,
			Message = string.IsNullOrEmpty(failMessage) ? null : failMessage,
			IsReadOnly = Settings.Category is { Length: > 0 } && category != Settings.Category
		};
	}

	private static DownloadItemStatus MapQueueStatus(string? status)
		=> status switch
		{
			"Downloading" or "Fetching" => DownloadItemStatus.DOWNLOADING,
			"Paused" => DownloadItemStatus.PAUSED,
			_ => DownloadItemStatus.QUEUED
		};

	private static double ParseDouble(string? value)
		=> double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;

	private static TimeSpan? ParseTimeSpan(string? value)
		=> TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var result) ? result : null;
}
