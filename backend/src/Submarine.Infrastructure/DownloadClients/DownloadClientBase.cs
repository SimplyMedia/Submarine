using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     Shared plumbing for download clients: identity stamping of items, status aggregation,
///     release data resolution and http error mapping
/// </summary>
public abstract class DownloadClientBase<TSettings>(
	TSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : IDownloadClient
	where TSettings : DownloadClientSettings
{
	/// <summary>Serializer options for client api payloads</summary>
	protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	/// <summary>The validated settings of this client instance</summary>
	protected TSettings Settings { get; } = settings;

	/// <summary>Http client for this instance</summary>
	protected internal HttpClient Http { get; } = httpClient;

	/// <summary>Display name of this client instance, used in error messages</summary>
	protected string ClientName { get; } = clientName;

	/// <summary>Submarine id of this client instance</summary>
	protected int ClientId { get; } = clientId;

	/// <inheritdoc />
	public abstract DownloadClientType Type { get; }

	/// <inheritdoc />
	public abstract Protocol Protocol { get; }

	/// <inheritdoc />
	public Task<string> AddAsync(RemoteRelease release, SeedCriteria? seedCriteria, CancellationToken cancellationToken)
		=> AddAsyncCore(release, seedCriteria, cancellationToken);

	/// <inheritdoc />
	public async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken)
		=> (await GetItemsAsyncCore(cancellationToken)).Select(FinishItem).ToList();

	/// <inheritdoc />
	public virtual async Task<DownloadClientStatus> GetStatusAsync(CancellationToken cancellationToken)
	{
		var items = await GetItemsAsync(cancellationToken);

		return new DownloadClientStatus(
		[
			.. items
				.Where(item => !string.IsNullOrWhiteSpace(item.OutputPath))
				.Select(item => item.OutputPath!)
				.Distinct(StringComparer.Ordinal)
		]);
	}

	/// <inheritdoc />
	public Task RemoveAsync(string downloadId, bool deleteData, CancellationToken cancellationToken)
		=> RemoveAsyncCore(downloadId, deleteData, cancellationToken);

	/// <summary>No-op; only clients with post-import bookkeeping override this</summary>
	public virtual Task MarkImportedAsync(string downloadId, CancellationToken cancellationToken)
		=> Task.CompletedTask;

	/// <inheritdoc />
	public Task TestAsync(CancellationToken cancellationToken)
		=> TestAsyncCore(cancellationToken);

	/// <summary>Sends a release to the client and returns its download id</summary>
	protected abstract Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken);

	/// <summary>Lists raw client items; identity fields are stamped by the base class</summary>
	protected abstract Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(CancellationToken cancellationToken);

	/// <summary>Removes a download from the client</summary>
	protected abstract Task RemoveAsyncCore(string downloadId, bool deleteData, CancellationToken cancellationToken);

	/// <summary>Verifies reachability and credentials; throw <see cref="DownloadClientException" /> with an actionable message</summary>
	protected abstract Task TestAsyncCore(CancellationToken cancellationToken);

	private DownloadClientItem FinishItem(DownloadClientItem item)
		=> item with
		{
			Protocol = Protocol,
			DownloadClientId = ClientId,
			DownloadClientName = ClientName,
			CanBeRemoved = !item.IsReadOnly,
			CanMoveFiles = !item.IsReadOnly && item.SeedRatio is null && item.SeedTime is null
		};

	/// <summary>
	///     The magnet uri of the release: the dedicated magnet url, or the download url when it is a magnet
	/// </summary>
	protected string? MagnetUrl(RemoteRelease release)
		=> release.MagnetUrl
			?? (release.DownloadUrl is { } url && url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase)
				? url
				: null);

	/// <summary>
	///     Torrent file contents: the bytes provided with the release, or fetched from its download url
	/// </summary>
	/// <exception cref="DownloadClientException">No torrent data is available for the release</exception>
	protected async Task<byte[]> GetTorrentDataAsync(RemoteRelease release, CancellationToken cancellationToken)
	{
		if (release.TorrentFile is { Length: > 0 } file)
			return file;

		if (release.DownloadUrl is not { } url || url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
			throw new DownloadClientException(
				$"Release {release.Title} has no torrent file and its download url is not fetchable");

		return await DownloadFileAsync(url, release.Title, cancellationToken);
	}

	/// <summary>
	///     Nzb file contents: the bytes provided with the release, or fetched from its download url
	/// </summary>
	/// <exception cref="DownloadClientException">No nzb data is available for the release</exception>
	protected async Task<byte[]> GetNzbDataAsync(RemoteRelease release, CancellationToken cancellationToken)
	{
		if (release.NzbFile is { Length: > 0 } file)
			return file;

		if (release.DownloadUrl is not { } url)
			throw new DownloadClientException($"Release {release.Title} has no nzb file or download url");

		return await DownloadFileAsync(url, release.Title, cancellationToken);
	}

	private async Task<byte[]> DownloadFileAsync(string url, string title, CancellationToken cancellationToken)
	{
		try
		{
			return await Http.GetByteArrayAsync(url, cancellationToken);
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
			                           && !cancellationToken.IsCancellationRequested)
		{
			throw new DownloadClientException($"Could not download the file for release {title}: {ex.Message}", ex);
		}
	}

	/// <summary>
	///     Resolves the download id of a torrent release: the indexer-provided hash, the magnet hash, or the
	///     hash computed from the torrent file; always upper-case hex
	/// </summary>
	/// <exception cref="DownloadClientException">No info hash is resolvable</exception>
	protected string ResolveTorrentId(RemoteRelease release, string? magnetUrl, byte[]? torrentData)
	{
		if (release.InfoHash is { Length: > 0 } provided)
			return provided.ToUpperInvariant();

		if (magnetUrl is not null && TorrentInfoHash.FromMagnet(magnetUrl) is { } hash)
			return hash;

		if (torrentData is { Length: > 0 })
			return TorrentInfoHash.Compute(torrentData);

		throw new DownloadClientException($"Release {release.Title} has no resolvable info hash");
	}

	/// <summary>Sends a request, mapping transport failures to <see cref="DownloadClientException" /></summary>
	protected async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> requestFactory,
		CancellationToken cancellationToken)
	{
		try
		{
			return await Http.SendAsync(requestFactory(), cancellationToken);
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
			                           && !cancellationToken.IsCancellationRequested)
		{
			throw new DownloadClientException($"{Type} {ClientName} is unreachable: {ex.Message}", ex);
		}
	}

	/// <summary>Sends a request with method, url and optional content</summary>
	protected Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content,
		CancellationToken cancellationToken)
		=> SendAsync(() => new HttpRequestMessage(method, url) { Content = content }, cancellationToken);

	/// <summary>Http basic auth header for the given credentials, or null when username is empty</summary>
	protected static AuthenticationHeaderValue? BasicAuth(string? username, string? password)
		=> username is null
			? null
			: new AuthenticationHeaderValue("Basic",
				Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password ?? string.Empty}")));

	/// <summary>Reads a json body, failing on empty responses</summary>
	protected async Task<T> ReadJsonAsync<T>(HttpContent content, CancellationToken cancellationToken)
		=> await content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
			?? throw new DownloadClientException($"{Type} {ClientName} returned an empty response");

	/// <summary>Throws a <see cref="DownloadClientException" /> when the response is not successful</summary>
	protected static void EnsureSuccess(HttpResponseMessage response, string operation)
	{
		if (!response.IsSuccessStatusCode)
			throw new DownloadClientException($"{operation} failed with status {(int)response.StatusCode}");
	}

	/// <summary>Replaces characters that are invalid in file names with underscores</summary>
	protected static string SanitizeFileName(string title)
	{
		var invalid = Path.GetInvalidFileNameChars();
		return string.Concat(title.Select(c => invalid.Contains(c) ? '_' : c));
	}
}
