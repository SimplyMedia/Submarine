using System.Text.Json;
using System.Text.Json.Serialization;

namespace Submarine.Core.Download;

/// <summary>
///     Connection settings for the uTorrent web ui
/// </summary>
[JsonConverter(typeof(UTorrentSettingsConverter))]
public record UTorrentSettings : DownloadClientSettings, IDownloadClientEndpoint
{
	/// <summary>Hostname or ip address of the uTorrent web ui</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>Port of the uTorrent web ui</summary>
	public int Port { get; init; } = 8080;

	/// <summary>Whether to connect using https</summary>
	public bool UseSsl { get; init; }

	/// <summary>Url base path the web ui is served under, defaults to /gui/</summary>
	public string? UrlBase { get; init; }

	/// <summary>Username, if the web ui requires authentication</summary>
	public string? Username { get; init; }

	/// <summary>Password, if the web ui requires authentication</summary>
	public string? Password { get; init; }

	/// <summary>Label torrents are added under and owned by</summary>
	public string? Category { get; init; }

	/// <summary>Label torrents are moved to after import, if different from <see cref="Category" /></summary>
	public string? PostImportCategory { get; init; }

	/// <summary>State torrents start in</summary>
	public UTorrentInitialState InitialState { get; init; } = UTorrentInitialState.START;

	/// <summary>Queue position for recent releases</summary>
	public DownloadClientItemPriority RecentPriority { get; init; } = DownloadClientItemPriority.LAST;

	/// <summary>Queue position for older releases</summary>
	public DownloadClientItemPriority OlderPriority { get; init; } = DownloadClientItemPriority.LAST;

	/// <summary>Effective url base; uTorrent serves its web ui under /gui/</summary>
	public string EffectiveUrlBase => UrlBase ?? "/gui/";
}

/// <summary>
///     State torrents added to uTorrent start in
/// </summary>
public enum UTorrentInitialState
{
	/// <summary>Start downloading</summary>
	START,

	/// <summary>Force start, ignoring queue limits</summary>
	FORCE_START,

	/// <summary>Add paused</summary>
	PAUSE,

	/// <summary>Add stopped</summary>
	STOP
}

/// <summary>
///     Reads/writes <see cref="UTorrentSettings" />; on read, a legacy <c>addStopped</c> boolean (the field
///     <see cref="UTorrentSettings.InitialState" /> replaced) is honoured when <c>initialState</c> is absent,
///     so settings saved before the replacement keep behaving the same: <c>addStopped: true</c> becomes
///     <see cref="UTorrentInitialState.STOP" />
/// </summary>
public sealed class UTorrentSettingsConverter : JsonConverter<UTorrentSettings>
{
	/// <inheritdoc />
	public override UTorrentSettings Read(ref Utf8JsonReader reader, Type typeToConvert,
		JsonSerializerOptions options)
	{
		using var document = JsonDocument.ParseValue(ref reader);
		var root = document.RootElement;

		return new UTorrentSettings
		{
			Host = GetString(root, "host") ?? string.Empty,
			Port = GetInt(root, "port") ?? 8080,
			UseSsl = GetBool(root, "useSsl") ?? false,
			UrlBase = GetString(root, "urlBase"),
			Username = GetString(root, "username"),
			Password = GetString(root, "password"),
			Category = GetString(root, "category"),
			PostImportCategory = GetString(root, "postImportCategory"),
			InitialState = ReadInitialState(root),
			RecentPriority = ParseEnum(root, "recentPriority", DownloadClientItemPriority.LAST),
			OlderPriority = ParseEnum(root, "olderPriority", DownloadClientItemPriority.LAST)
		};
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, UTorrentSettings value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		writer.WriteString("host", value.Host);
		writer.WriteNumber("port", value.Port);
		writer.WriteBoolean("useSsl", value.UseSsl);
		WriteNullableString(writer, "urlBase", value.UrlBase);
		WriteNullableString(writer, "username", value.Username);
		WriteNullableString(writer, "password", value.Password);
		WriteNullableString(writer, "category", value.Category);
		WriteNullableString(writer, "postImportCategory", value.PostImportCategory);
		writer.WriteString("initialState", value.InitialState.ToString());
		writer.WriteString("recentPriority", value.RecentPriority.ToString());
		writer.WriteString("olderPriority", value.OlderPriority.ToString());
		writer.WriteEndObject();
	}

	private static UTorrentInitialState ReadInitialState(JsonElement root)
	{
		if (root.TryGetProperty("initialState", out var stateElement))
			return ParseEnum(stateElement, UTorrentInitialState.START);

		// legacy shape: AddStopped=true meant the torrent was added and immediately stopped
		return GetBool(root, "addStopped") is true ? UTorrentInitialState.STOP : UTorrentInitialState.START;
	}

	private static TEnum ParseEnum<TEnum>(JsonElement root, string name, TEnum fallback) where TEnum : struct, Enum
		=> root.TryGetProperty(name, out var element) ? ParseEnum(element, fallback) : fallback;

	private static TEnum ParseEnum<TEnum>(JsonElement element, TEnum fallback) where TEnum : struct, Enum
	{
		if (element.ValueKind == JsonValueKind.String)
			return Enum.TryParse<TEnum>(element.GetString(), ignoreCase: true, out var parsed) ? parsed : fallback;

		if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var ordinal))
			return (TEnum)Enum.ToObject(typeof(TEnum), ordinal);

		return fallback;
	}

	private static string? GetString(JsonElement root, string name)
		=> root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
			? element.GetString()
			: null;

	private static int? GetInt(JsonElement root, string name)
		=> root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Number
			? element.GetInt32()
			: null;

	private static bool? GetBool(JsonElement root, string name)
		=> root.TryGetProperty(name, out var element) && element.ValueKind is JsonValueKind.True or JsonValueKind.False
			? element.GetBoolean()
			: null;

	private static void WriteNullableString(Utf8JsonWriter writer, string name, string? value)
	{
		if (value is null)
			writer.WriteNull(name);
		else
			writer.WriteString(name, value);
	}
}
