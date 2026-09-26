using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> for rTorrent over XML-RPC
/// </summary>
public sealed class RTorrentClient(
	RTorrentSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : DownloadClientBase<RTorrentSettings>(settings, clientId, clientName, httpClient)
{
	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.RTORRENT;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.BITTORRENT;

	private string Endpoint => Settings.Build(string.Empty);

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		var magnetUrl = MagnetUrl(release);

		if (magnetUrl is not null)
		{
			var parameters = LoadParameters(magnetUrl);
			await CallAsync(Settings.AddStopped ? "load.stop" : "load.start", parameters, cancellationToken);

			return ResolveTorrentId(release, magnetUrl, null);
		}

		var torrentData = await GetTorrentDataAsync(release, cancellationToken);
		var fileParameters = LoadParameters(torrentData);
		await CallAsync(Settings.AddStopped ? "load.raw" : "load.raw_start", fileParameters, cancellationToken);

		return ResolveTorrentId(release, magnetUrl, torrentData);
	}

	private List<object?> LoadParameters(object target)
	{
		var parameters = new List<object?> { string.Empty, target };

		if (Settings.Category is { Length: > 0 })
			parameters.Add($"d.custom1.set={Settings.Category}");

		if (Settings.Directory is { Length: > 0 })
			parameters.Add($"d.directory_base.set={Settings.Directory}");

		return parameters;
	}

	/// <inheritdoc />
	protected override async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(
		CancellationToken cancellationToken)
	{
		var response = await CallAsync("d.multicall2", new object?[]
		{
			"", "main",
			"d.hash=", "d.name=", "d.size_bytes=", "d.left_bytes=",
			"d.complete=", "d.is_active=", "d.state=", "d.directory=", "d.custom1="
		}, cancellationToken);

		var rows = (List<object?>)response!;

		return [.. rows.Select(row => MapItem((List<object?>)row!))];
	}

	/// <inheritdoc />
	protected override Task RemoveAsyncCore(string downloadId, bool deleteData, CancellationToken cancellationToken)
		// rTorrent cannot delete downloaded data through this call; files stay on disk
		=> CallAsync("d.erase", [downloadId], cancellationToken);

	/// <inheritdoc />
	public override Task MarkImportedAsync(string downloadId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(Settings.PostImportCategory) || Settings.PostImportCategory == Settings.Category)
			return Task.CompletedTask;

		return CallAsync("d.custom1.set", [downloadId, Settings.PostImportCategory], cancellationToken);
	}

	/// <inheritdoc />
	protected override Task TestAsyncCore(CancellationToken cancellationToken)
		=> CallAsync("system.client_version", [], cancellationToken);

	private async Task<object?> CallAsync(string methodName, IReadOnlyList<object?> parameters,
		CancellationToken cancellationToken)
	{
		using var response = await SendAsync(
			() =>
			{
				var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
				{
					Content = new StringContent(XmlRpcSerializer.BuildRequest(methodName, parameters), Encoding.UTF8,
						"text/xml")
				};

				if (BasicAuth(Settings.Username, Settings.Password) is { } auth)
					request.Headers.Authorization = auth;

				return request;
			}, cancellationToken);

		EnsureSuccess(response, $"rTorrent {ClientName} request");

		return XmlRpcSerializer.ParseResponse(await response.Content.ReadAsStringAsync(cancellationToken));
	}

	private DownloadClientItem MapItem(List<object?> row)
	{
		var complete = Convert.ToInt64(row[4], CultureInfo.InvariantCulture) == 1;
		var active = Convert.ToInt64(row[5], CultureInfo.InvariantCulture) == 1;
		var started = Convert.ToInt64(row[6], CultureInfo.InvariantCulture) == 1;
		var label = row[8] as string;

		return new DownloadClientItem
		{
			DownloadId = (string)row[0]!,
			Title = (string)row[1]!,
			TotalSize = Convert.ToInt64(row[2], CultureInfo.InvariantCulture),
			RemainingSize = Convert.ToInt64(row[3], CultureInfo.InvariantCulture),
			Status = complete
				? DownloadItemStatus.COMPLETED
				: !started
					? DownloadItemStatus.PAUSED
					: active
						? DownloadItemStatus.DOWNLOADING
						: DownloadItemStatus.QUEUED,
			OutputPath = row[7] as string,
			Category = string.IsNullOrEmpty(label) ? null : label,
			IsReadOnly = Settings.Category is { Length: > 0 } && label != Settings.Category
		};
	}
}
