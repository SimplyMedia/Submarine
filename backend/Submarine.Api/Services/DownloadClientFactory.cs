using System.Text.Json;
using System.Text.Json.Serialization;
using Submarine.Api.Exceptions;
using Submarine.Core.Download;
using Submarine.Core.Download.Blackhole;
using Submarine.Core.Download.Clients;

namespace Submarine.Api.Services;

/// <summary>
///     Builds <see cref="IDownloadClient" /> instances from persisted <see cref="DownloadClientConfig" />
/// </summary>
public class DownloadClientFactory
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() }
	};

	private readonly IHttpClientFactory _httpClientFactory;
	private readonly ILoggerFactory _loggerFactory;

	public DownloadClientFactory(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
	{
		_httpClientFactory = httpClientFactory;
		_loggerFactory = loggerFactory;
	}

	/// <summary>
	///     Creates the download client described by the config
	/// </summary>
	public IDownloadClient Create(DownloadClientConfig config)
	{
		var http = _httpClientFactory.CreateClient("downloadclient");

		return config.Type switch
		{
			DownloadClientType.QBITTORRENT => new QBittorrentClient(
				Deserialize<QBittorrentSettings>(config.SettingsJson), http,
				_loggerFactory.CreateLogger<QBittorrentClient>()),
			DownloadClientType.TRANSMISSION => new TransmissionClient(
				Deserialize<TransmissionSettings>(config.SettingsJson), http,
				_loggerFactory.CreateLogger<TransmissionClient>()),
			DownloadClientType.DELUGE => new DelugeClient(
				Deserialize<DelugeSettings>(config.SettingsJson), http,
				_loggerFactory.CreateLogger<DelugeClient>()),
			DownloadClientType.RTORRENT => new RTorrentClient(
				Deserialize<RTorrentSettings>(config.SettingsJson), http,
				_loggerFactory.CreateLogger<RTorrentClient>()),
			DownloadClientType.UTORRENT => new UTorrentClient(
				Deserialize<UTorrentSettings>(config.SettingsJson), http,
				_loggerFactory.CreateLogger<UTorrentClient>()),
			DownloadClientType.ARIA2 => new Aria2Client(
				Deserialize<Aria2Settings>(config.SettingsJson), http,
				_loggerFactory.CreateLogger<Aria2Client>()),
			DownloadClientType.FLOOD => new FloodClient(
				Deserialize<FloodSettings>(config.SettingsJson), http,
				_loggerFactory.CreateLogger<FloodClient>()),
			DownloadClientType.DOWNLOAD_STATION => new DownloadStationClient(
				Deserialize<DownloadStationSettings>(config.SettingsJson), http,
				_loggerFactory.CreateLogger<DownloadStationClient>()),
			DownloadClientType.SABNZBD => new SabnzbdClient(
				Deserialize<SabnzbdSettings>(config.SettingsJson), http,
				_loggerFactory.CreateLogger<SabnzbdClient>()),
			DownloadClientType.NZBGET => new NzbGetClient(
				Deserialize<NzbGetSettings>(config.SettingsJson), http,
				_loggerFactory.CreateLogger<NzbGetClient>()),
			DownloadClientType.TORRENT_BLACKHOLE => new TorrentBlackholeClient(
				Deserialize<TorrentBlackholeSettings>(config.SettingsJson), http,
				_loggerFactory.CreateLogger<TorrentBlackholeClient>()),
			DownloadClientType.USENET_BLACKHOLE => new UsenetBlackholeClient(
				Deserialize<UsenetBlackholeSettings>(config.SettingsJson), http,
				_loggerFactory.CreateLogger<UsenetBlackholeClient>()),
			_ => throw new ArgumentOutOfRangeException(nameof(config))
		};
	}

	/// <summary>
	///     Validates that the settings JSON deserializes into the record matching the type
	/// </summary>
	/// <exception cref="BadRequestException">Thrown when the settings do not match the type</exception>
	public void ValidateSettings(DownloadClientType type, string settingsJson)
	{
		try
		{
			if (JsonSerializer.Deserialize(settingsJson ?? string.Empty, SettingsTypeFor(type), JsonOptions) == null)
				throw new BadRequestException($"settings for {type} could not be parsed");
		}
		catch (JsonException ex)
		{
			throw new BadRequestException($"invalid settings for {type}: {ex.Message}");
		}
	}

	private static Type SettingsTypeFor(DownloadClientType type)
		=> type switch
		{
			DownloadClientType.QBITTORRENT => typeof(QBittorrentSettings),
			DownloadClientType.TRANSMISSION => typeof(TransmissionSettings),
			DownloadClientType.DELUGE => typeof(DelugeSettings),
			DownloadClientType.RTORRENT => typeof(RTorrentSettings),
			DownloadClientType.UTORRENT => typeof(UTorrentSettings),
			DownloadClientType.ARIA2 => typeof(Aria2Settings),
			DownloadClientType.FLOOD => typeof(FloodSettings),
			DownloadClientType.DOWNLOAD_STATION => typeof(DownloadStationSettings),
			DownloadClientType.SABNZBD => typeof(SabnzbdSettings),
			DownloadClientType.NZBGET => typeof(NzbGetSettings),
			DownloadClientType.TORRENT_BLACKHOLE => typeof(TorrentBlackholeSettings),
			DownloadClientType.USENET_BLACKHOLE => typeof(UsenetBlackholeSettings),
			_ => throw new ArgumentOutOfRangeException(nameof(type))
		};

	private static T Deserialize<T>(string settingsJson)
		=> JsonSerializer.Deserialize<T>(settingsJson, JsonOptions)
		   ?? throw new DownloadClientException($"settings could not be deserialized to {typeof(T).Name}");
}
