using Submarine.Core.Download;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     Creates download clients from persisted settings json
/// </summary>
public sealed class DownloadClientFactory(IHttpClientFactory httpClientFactory) : IDownloadClientFactory
{
	/// <summary>Name of the shared named http client; per-instance base addresses are applied on top</summary>
	public const string HttpClientName = "download-clients";

	/// <summary>Default timeout for download client api calls</summary>
	public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(15);

	/// <inheritdoc />
	public IDownloadClient Create(DownloadClientType type, string settingsJson, int clientId, string clientName)
	{
		DownloadClientSettings settings;
		try
		{
			settings = DownloadClientSettingsJson.Parse(type, settingsJson);
		}
		catch (DownloadClientException ex)
		{
			throw new DownloadClientException(
				$"Download client {clientName} ({type}) has invalid settings: {ex.Message}", ex);
		}

		var httpClient = httpClientFactory.CreateClient(HttpClientName);
		if (settings is IDownloadClientEndpoint endpoint)
			httpClient.BaseAddress = new Uri(endpoint.BaseUrl() + "/");

		return type switch
		{
			DownloadClientType.QBITTORRENT => new QBittorrentClient((QBittorrentSettings)settings, clientId, clientName, httpClient),
			DownloadClientType.TRANSMISSION => new TransmissionClient((TransmissionSettings)settings, clientId, clientName, httpClient),
			DownloadClientType.DELUGE => new DelugeClient((DelugeSettings)settings, clientId, clientName, httpClient),
			DownloadClientType.RTORRENT => new RTorrentClient((RTorrentSettings)settings, clientId, clientName, httpClient),
			DownloadClientType.UTORRENT => new UTorrentClient((UTorrentSettings)settings, clientId, clientName, httpClient),
			DownloadClientType.ARIA2 => new Aria2Client((Aria2Settings)settings, clientId, clientName, httpClient),
			DownloadClientType.FLOOD => new FloodClient((FloodSettings)settings, clientId, clientName, httpClient),
			DownloadClientType.DOWNLOAD_STATION => new DownloadStationClient((DownloadStationSettings)settings, clientId, clientName, httpClient),
			DownloadClientType.SABNZBD => new SabnzbdClient((SabnzbdSettings)settings, clientId, clientName, httpClient),
			DownloadClientType.NZBGET => new NzbGetClient((NzbGetSettings)settings, clientId, clientName, httpClient),
			DownloadClientType.TORRENT_BLACKHOLE => new TorrentBlackholeClient((TorrentBlackholeSettings)settings, clientId, clientName, httpClient),
			DownloadClientType.USENET_BLACKHOLE => new UsenetBlackholeClient((UsenetBlackholeSettings)settings, clientId, clientName, httpClient),
			DownloadClientType.VUZE => new VuzeClient((TransmissionSettings)settings, clientId, clientName, httpClient),
			DownloadClientType.HADOUKEN => new HadoukenClient((HadoukenSettings)settings, clientId, clientName, httpClient),
			DownloadClientType.NZBVORTEX => new NzbVortexClient((NzbVortexSettings)settings, clientId, clientName, httpClient),
			DownloadClientType.PNEUMATIC => new PneumaticClient((PneumaticSettings)settings, clientId, clientName, httpClient),
			DownloadClientType.FREEBOX_DOWNLOAD => new FreeboxDownloadClient((FreeboxDownloadSettings)settings, clientId, clientName, httpClient),
			DownloadClientType.RQBIT => new RQbitClient((RQbitSettings)settings, clientId, clientName, httpClient),
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown download client type")
		};
	}
}
