using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Download.Rpc;
using Submarine.Core.Indexer;
using Submarine.Core.Provider;

namespace Submarine.Core.Download.Clients;

/// <summary>
///     Download client talking to rTorrent over XML-RPC
/// </summary>
public class RTorrentClient : IDownloadClient
{
	private readonly RTorrentSettings _settings;
	private readonly HttpClient _httpClient;
	private readonly XmlRpcClient _rpc;
	private readonly ILogger<RTorrentClient> _logger;

	/// <summary>
	///     Creates a new instance of <see cref="RTorrentClient" />
	/// </summary>
	/// <param name="settings">connection settings</param>
	/// <param name="httpClient">http client used for XML-RPC and torrent downloads</param>
	/// <param name="logger">logger</param>
	public RTorrentClient(RTorrentSettings settings, HttpClient httpClient, ILogger<RTorrentClient> logger)
	{
		_settings = settings;
		_httpClient = httpClient;
		_rpc = new XmlRpcClient(httpClient);
		_logger = logger;
	}

	/// <inheritdoc />
	public Protocol Protocol => Protocol.BITTORRENT;

	/// <inheritdoc />
	public async Task<string> AddDownloadAsync(ReleaseInfo release, CancellationToken cancellationToken = default)
	{
		var url = release.DownloadUrl
			?? throw new DownloadClientException("Release has no download url");

		if (url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
		{
			var hash = MagnetHash(url)
				?? throw new DownloadClientException("Magnet link has no info hash");
			await _rpc.CallAsync(_settings.Endpoint, "load.start", BuildLoadParameters(url), cancellationToken);

			return hash;
		}

		var torrent = await _httpClient.GetByteArrayAsync(url, cancellationToken);
		await _rpc.CallAsync(_settings.Endpoint, "load.raw_start", BuildLoadParameters(torrent), cancellationToken);

		return TorrentInfoHash.Compute(torrent);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken = default)
	{
		var response = await _rpc.CallAsync(_settings.Endpoint, "d.multicall2", new object?[]
		{
			"", "main",
			"d.hash=", "d.name=", "d.size_bytes=", "d.left_bytes=",
			"d.complete=", "d.is_active=", "d.directory=", "d.custom1="
		}, cancellationToken);

		var rows = (List<object?>)response!;

		return rows.Select(row => MapItem((List<object?>)row!)).ToList();
	}

	/// <inheritdoc />
	public async Task RemoveItemAsync(string downloadId, bool deleteData,
		CancellationToken cancellationToken = default)
	{
		if (deleteData)
			_logger.LogWarning(
				"rTorrent cannot delete downloaded data; removing {Hash} without deleting its files", downloadId);

		await _rpc.CallAsync(_settings.Endpoint, "d.erase", new object?[] { downloadId }, cancellationToken);
	}

	/// <inheritdoc />
	public async Task TestAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			await _rpc.CallAsync(_settings.Endpoint, "system.api_version", [], cancellationToken);
		}
		catch (DownloadClientException)
		{
			throw;
		}
		catch (Exception e)
		{
			throw new DownloadClientException("Failed to reach rTorrent", e);
		}
	}

	private List<object?> BuildLoadParameters(object target)
	{
		var parameters = new List<object?> { "", target };

		if (_settings.Category is { Length: > 0 } category)
			parameters.Add("d.custom1.set=" + category);

		return parameters;
	}

	private static DownloadClientItem MapItem(List<object?> row)
	{
		var complete = Convert.ToInt64(row[4]) == 1;
		var active = Convert.ToInt64(row[5]) == 1;
		var label = row[7] as string;

		return new DownloadClientItem
		{
			DownloadId = (string)row[0]!,
			Title = (string)row[1]!,
			TotalSize = Convert.ToInt64(row[2]),
			RemainingSize = Convert.ToInt64(row[3]),
			Status = complete
				? DownloadItemStatus.COMPLETED
				: active
					? DownloadItemStatus.DOWNLOADING
					: DownloadItemStatus.PAUSED,
			OutputPath = row[6] as string,
			Category = string.IsNullOrEmpty(label) ? null : label
		};
	}

	private static string? MagnetHash(string url)
	{
		var match = Regex.Match(url, "xt=urn:btih:([^&]+)", RegexOptions.IgnoreCase);

		return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
	}
}
