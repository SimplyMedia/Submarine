using System.Text.Json;
using System.Text.Json.Serialization;

namespace Submarine.Core.Download;

/// <summary>
///     Deserialized and validated download client settings
/// </summary>
/// <param name="Settings">The settings, or null when the json could not be deserialized</param>
/// <param name="Errors">Field errors keyed by camel-case field name; "$" holds json-level errors</param>
public sealed record DownloadClientSettingsValidation(
	DownloadClientSettings? Settings,
	IReadOnlyDictionary<string, string[]> Errors)
{
	/// <summary>Whether the settings are valid</summary>
	public bool IsValid => Errors.Count == 0;
}

/// <summary>
///     Deserializes and validates download client settings per <see cref="DownloadClientType" />
/// </summary>
public static class DownloadClientSettingsJson
{
	/// <summary>
	///     Serializer options used for download client settings: camel-case properties, enums as strings
	/// </summary>
	public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() }
	};

	/// <summary>
	///     Deserializes the json for the client type and collects field errors without throwing
	/// </summary>
	public static DownloadClientSettingsValidation Validate(DownloadClientType type, string json)
	{
		DownloadClientSettings? settings = null;
		var errors = new Dictionary<string, List<string>>();

		try
		{
			settings = (DownloadClientSettings?)JsonSerializer.Deserialize(json, SettingsType(type), SerializerOptions);
		}
		catch (JsonException ex)
		{
			errors.Add("$", [ex.Message]);
		}

		if (settings is not null)
			ValidateFields(settings, errors);

		return new DownloadClientSettingsValidation(settings,
			errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
	}

	/// <summary>
	///     Deserializes and validates the json, throwing when it is not valid for the client type
	/// </summary>
	/// <exception cref="DownloadClientException">The settings are invalid</exception>
	public static DownloadClientSettings Parse(DownloadClientType type, string json)
	{
		var result = Validate(type, json);
		if (result.IsValid) return result.Settings!;

		var details = result.Errors.SelectMany(pair => pair.Key == "$"
				? pair.Value
				: pair.Value.Select(message => $"{pair.Key} {message}"))
			.ToList();

		throw new DownloadClientException($"Invalid {type} settings: {string.Join("; ", details)}");
	}

	/// <summary>
	///     Serializes settings to json
	/// </summary>
	public static string Serialize(DownloadClientSettings settings)
		=> JsonSerializer.Serialize(settings, settings.GetType(), SerializerOptions);

	private static Type SettingsType(DownloadClientType type)
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
			DownloadClientType.VUZE => typeof(TransmissionSettings),
			DownloadClientType.HADOUKEN => typeof(HadoukenSettings),
			DownloadClientType.NZBVORTEX => typeof(NzbVortexSettings),
			DownloadClientType.PNEUMATIC => typeof(PneumaticSettings),
			DownloadClientType.FREEBOX_DOWNLOAD => typeof(FreeboxDownloadSettings),
			DownloadClientType.RQBIT => typeof(RQbitSettings),
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown download client type")
		};

	private static void ValidateFields(DownloadClientSettings settings, Dictionary<string, List<string>> errors)
	{
		if (settings is IDownloadClientEndpoint endpoint && endpoint.Port is < 1 or > 65535)
			Add(errors, "port", "must be between 1 and 65535");

		switch (settings)
		{
			case QBittorrentSettings s:
				Require(errors, s.Host, "host");
				break;
			case TransmissionSettings s:
				Require(errors, s.Host, "host");
				break;
			case DelugeSettings s:
				Require(errors, s.Host, "host");
				Require(errors, s.Password, "password");
				break;
			case RTorrentSettings s:
				Require(errors, s.Host, "host");
				break;
			case UTorrentSettings s:
				Require(errors, s.Host, "host");
				break;
			case Aria2Settings s:
				Require(errors, s.Host, "host");
				break;
			case FloodSettings s:
				Require(errors, s.Host, "host");
				Require(errors, s.Username, "username");
				Require(errors, s.Password, "password");
				break;
			case DownloadStationSettings s:
				Require(errors, s.Host, "host");
				Require(errors, s.Username, "username");
				Require(errors, s.Password, "password");
				break;
			case SabnzbdSettings s:
				Require(errors, s.Host, "host");
				Require(errors, s.ApiKey, "apiKey");
				break;
			case NzbGetSettings s:
				Require(errors, s.Host, "host");
				Require(errors, s.Username, "username");
				Require(errors, s.Password, "password");
				break;
			case TorrentBlackholeSettings s:
				Require(errors, s.TorrentFolder, "torrentFolder");
				Require(errors, s.WatchFolder, "watchFolder");
				break;
			case UsenetBlackholeSettings s:
				Require(errors, s.NzbFolder, "nzbFolder");
				Require(errors, s.WatchFolder, "watchFolder");
				break;
			case HadoukenSettings s:
				Require(errors, s.Host, "host");
				Require(errors, s.Username, "username");
				Require(errors, s.Password, "password");
				break;
			case NzbVortexSettings s:
				Require(errors, s.Host, "host");
				Require(errors, s.ApiKey, "apiKey");
				break;
			case PneumaticSettings s:
				Require(errors, s.NzbFolder, "nzbFolder");
				Require(errors, s.StrmFolder, "strmFolder");
				break;
			case FreeboxDownloadSettings s:
				Require(errors, s.Host, "host");
				Require(errors, s.AppId, "appId");
				Require(errors, s.AppToken, "appToken");
				break;
			case RQbitSettings s:
				Require(errors, s.Host, "host");
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(settings), settings, "Unknown download client settings");
		}
	}

	private static void Require(Dictionary<string, List<string>> errors, string? value, string field)
	{
		if (string.IsNullOrWhiteSpace(value))
			Add(errors, field, "is required");
	}

	private static void Add(Dictionary<string, List<string>> errors, string field, string message)
	{
		if (!errors.TryGetValue(field, out var list))
			errors[field] = list = [];

		list.Add(message);
	}
}
