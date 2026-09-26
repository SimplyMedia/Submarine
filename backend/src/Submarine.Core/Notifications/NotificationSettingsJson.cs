using System.Text.Json;
using System.Text.Json.Serialization;
using Submarine.Core.Enums;

namespace Submarine.Core.Notifications;

/// <summary>
///     Deserialized and validated notification settings.
/// </summary>
/// <param name="Settings">The settings, or null when the json could not be deserialized.</param>
/// <param name="Errors">Field errors keyed by camel-case field name; "$" holds json-level errors.</param>
public sealed record NotificationSettingsValidation(
	NotificationSettings? Settings,
	IReadOnlyDictionary<string, string[]> Errors)
{
	/// <summary>Whether the settings are valid.</summary>
	public bool IsValid => Errors.Count == 0;
}

/// <summary>
///     One configurable field of a notification type, used by the schema endpoint and validation.
/// </summary>
/// <param name="Name">Field name as it appears in the settings JSON.</param>
/// <param name="Label">Human readable label.</param>
/// <param name="Type">Input type: text, password, number, select, checkbox, tags or url.</param>
/// <param name="Options">Allowed values for select fields.</param>
/// <param name="Required">Whether the field must be set.</param>
/// <param name="HelpText">Short explanation.</param>
/// <param name="Default">Default value.</param>
public sealed record NotificationFieldDescriptor(
	string Name,
	string Label,
	string Type,
	string[]? Options,
	bool Required,
	string? HelpText,
	object? Default);

/// <summary>
///     Deserializes and validates notification settings per <see cref="NotificationType" />
///     and describes their fields.
/// </summary>
public static class NotificationSettingsJson
{
	/// <summary>
	///     Serializer options used for notification settings: camel-case properties, enums as strings.
	/// </summary>
	public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() }
	};

	/// <summary>
	///     Deserializes the json for the notification type and collects field errors without throwing.
	/// </summary>
	public static NotificationSettingsValidation Validate(NotificationType type, string json)
	{
		NotificationSettings? settings = null;
		var errors = new Dictionary<string, List<string>>();

		try
		{
			settings = (NotificationSettings?)JsonSerializer.Deserialize(json, SettingsType(type), SerializerOptions);
		}
		catch (JsonException ex)
		{
			errors.Add("$", [ex.Message]);
		}

		if (settings is not null)
		{
			ValidateFields(settings, errors);
		}

		return new NotificationSettingsValidation(settings,
			errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
	}

	/// <summary>
	///     Deserializes and validates the json, throwing when it is not valid for the notification type.
	/// </summary>
	/// <exception cref="InvalidOperationException">The settings are invalid.</exception>
	public static NotificationSettings Parse(NotificationType type, string json)
	{
		var result = Validate(type, json);
		if (result.IsValid)
		{
			return result.Settings!;
		}

		var details = result.Errors.SelectMany(pair => pair.Key == "$"
				? pair.Value
				: pair.Value.Select(message => $"{pair.Key} {message}"))
			.ToList();

		throw new InvalidOperationException($"Invalid {type} notification settings: {string.Join("; ", details)}");
	}

	/// <summary>
	///     Serializes settings to json.
	/// </summary>
	public static string Serialize(NotificationSettings settings)
		=> JsonSerializer.Serialize(settings, settings.GetType(), SerializerOptions);

	/// <summary>
	///     Field descriptors for every notification type, driving the schema endpoint.
	/// </summary>
	public static IReadOnlyDictionary<NotificationType, NotificationFieldDescriptor[]> DescribeAll()
		=> Enum.GetValues<NotificationType>().ToDictionary(type => type, Describe);

	/// <summary>
	///     Field descriptors of one notification type.
	/// </summary>
	public static NotificationFieldDescriptor[] Describe(NotificationType type)
		=> type switch
		{
			NotificationType.DISCORD =>
			[
				Url("webhookUrl", "Webhook URL", true),
				Text("username", "Username"),
				Url("avatar", "Avatar"),
				Tags("grabFields", "Grab fields", false, EmbedFieldOptions),
				Tags("importFields", "Import fields", false, EmbedFieldOptions)
			],
			NotificationType.TELEGRAM =>
			[
				Password("botToken", "Bot token", true, "Token of the bot from @BotFather"),
				Text("chatId", "Chat ID", true),
				Number("topicId", "Topic ID", false, "Forum topic id when the chat is a forum"),
				Checkbox("sendSilently", "Send silently")
			],
			NotificationType.WEBHOOK =>
			[
				Url("url", "URL", true),
				Select("method", "Method", true, ["POST", "PUT"], "POST"),
				Text("username", "Username"),
				Password("password", "Password", false),
				Tags("headers", "Headers", false, null, "Headers as Header:Value entries")
			],
			NotificationType.SLACK =>
			[
				Url("webhookUrl", "Webhook URL", true),
				Text("username", "Username"),
				Text("icon", "Icon"),
				Text("channel", "Channel")
			],
			NotificationType.PUSHOVER =>
			[
				Password("apiKey", "API Key", true, "Application token"),
				Password("userKey", "User Key", true),
				Tags("devices", "Devices"),
				Number("priority", "Priority", false, "Between -2 and 2", "0"),
				Text("sound", "Sound"),
				Number("retry", "Retry", false, "Retry interval in seconds for emergency priority"),
				Number("expire", "Expire", false, "Expiration in seconds for emergency priority")
			],
			NotificationType.PUSHBULLET =>
			[
				Password("apiKey", "API Key", true, "Access token"),
				Tags("deviceIds", "Devices"),
				Tags("channelTags", "Channels"),
				Text("senderId", "Sender")
			],
			NotificationType.GOTIFY =>
			[
				Url("server", "Server URL", true),
				Password("appToken", "App Token", true),
				Number("priority", "Priority", false, null, "5"),
				Checkbox("includeSeriesPoster", "Include series poster")
			],
			NotificationType.KODI =>
			[
				Text("host", "Host", true),
				Number("port", "Port", false, null, "8080"),
				Checkbox("useSsl", "Use SSL"),
				Text("username", "Username"),
				Password("password", "Password", false),
				Number("displayTime", "Display time", false, "Seconds the notification is shown", "5"),
				Checkbox("notify", "Notify"),
				Checkbox("updateLibrary", "Update library"),
				Checkbox("cleanLibrary", "Clean library"),
				Checkbox("alwaysUpdate", "Always update")
			],
			NotificationType.CUSTOM_SCRIPT =>
			[
				Text("path", "Path", true, "Absolute path of the script"),
				Text("arguments", "Arguments")
			],
			NotificationType.PLEX =>
			[
				Text("host", "Host", true),
				Number("port", "Port", false, null, "32400"),
				Checkbox("useSsl", "Use SSL"),
				Password("authToken", "Auth token", true, "Plex authentication token"),
				Checkbox("updateLibrary", "Update library"),
				Text("mapFrom", "Map from"),
				Text("mapTo", "Map to")
			],
			NotificationType.EMBY =>
			[
				Text("host", "Host", true),
				Number("port", "Port", false, null, "8096"),
				Checkbox("useSsl", "Use SSL"),
				Password("apiKey", "API Key", true),
				Checkbox("notify", "Notify"),
				Checkbox("updateLibrary", "Update library"),
				Text("mapFrom", "Map from"),
				Text("mapTo", "Map to")
			],
			NotificationType.JELLYFIN =>
			[
				Text("host", "Host", true),
				Number("port", "Port", false, null, "8096"),
				Checkbox("useSsl", "Use SSL"),
				Password("apiKey", "API Key", true),
				Checkbox("notify", "Notify"),
				Checkbox("updateLibrary", "Update library"),
				Text("mapFrom", "Map from"),
				Text("mapTo", "Map to")
			],
			NotificationType.EMAIL =>
			[
				Text("server", "Server", true),
				Number("port", "Port", false, null, "587"),
				Select("useEncryption", "Encryption", false, ["NONE", "START_TLS", "SSL"], "START_TLS"),
				Text("username", "Username"),
				Password("password", "Password", false),
				Text("from", "From", true),
				Tags("to", "To", true, null, "Recipient addresses"),
				Tags("cc", "Cc"),
				Tags("bcc", "Bcc")
			],
			NotificationType.NTFY =>
			[
				Url("serverUrl", "Server URL", false, null, "https://ntfy.sh"),
				Tags("topics", "Topics", true, null, "Topic names to publish to"),
				Text("username", "Username"),
				Password("password", "Password", false),
				Password("accessToken", "Access token", false),
				Number("priority", "Priority", false, "Between 1 and 5", "3"),
				Url("clickUrl", "Click URL")
			],
			NotificationType.APPRISE =>
			[
				Url("serverUrl", "Server URL", true),
				Text("configurationKey", "Configuration key"),
				Tags("statelessUrls", "Stateless URLs", false, null, "Notification urls, used without a configuration key"),
				Select("notificationType", "Notification type", false, ["info", "success", "warning", "failure"], "info"),
				Tags("tags", "Tags")
			],
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown notification type")
		};

	private static readonly string[] EmbedFieldOptions =
	[
		"overview", "year", "genres", "quality", "languages", "releaseGroup", "indexer",
		"downloadClient", "size", "links", "episodes", "poster"
	];

	private static Type SettingsType(NotificationType type)
		=> type switch
		{
			NotificationType.DISCORD => typeof(DiscordSettings),
			NotificationType.TELEGRAM => typeof(TelegramSettings),
			NotificationType.WEBHOOK => typeof(WebhookSettings),
			NotificationType.SLACK => typeof(SlackSettings),
			NotificationType.PUSHOVER => typeof(PushoverSettings),
			NotificationType.PUSHBULLET => typeof(PushbulletSettings),
			NotificationType.GOTIFY => typeof(GotifySettings),
			NotificationType.KODI => typeof(KodiSettings),
			NotificationType.CUSTOM_SCRIPT => typeof(CustomScriptSettings),
			NotificationType.PLEX => typeof(PlexSettings),
			NotificationType.EMBY => typeof(EmbySettings),
			NotificationType.JELLYFIN => typeof(JellyfinSettings),
			NotificationType.EMAIL => typeof(EmailSettings),
			NotificationType.NTFY => typeof(NtfySettings),
			NotificationType.APPRISE => typeof(AppriseSettings),
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown notification type")
		};

	private static void ValidateFields(NotificationSettings settings, Dictionary<string, List<string>> errors)
	{
		if (settings is IEndpoint endpoint && endpoint.Port is < 1 or > 65535)
		{
			Add(errors, "port", "must be between 1 and 65535");
		}

		switch (settings)
		{
			case DiscordSettings s:
				Require(errors, s.WebhookUrl, "webhookUrl");
				break;
			case TelegramSettings s:
				Require(errors, s.BotToken, "botToken");
				Require(errors, s.ChatId, "chatId");
				break;
			case WebhookSettings s:
				Require(errors, s.Url, "url");
				if (s.Method is not ("POST" or "PUT"))
				{
					Add(errors, "method", "must be POST or PUT");
				}

				break;
			case SlackSettings s:
				Require(errors, s.WebhookUrl, "webhookUrl");
				break;
			case PushoverSettings s:
				Require(errors, s.ApiKey, "apiKey");
				Require(errors, s.UserKey, "userKey");
				if (s.Priority is < -2 or > 2)
				{
					Add(errors, "priority", "must be between -2 and 2");
				}

				break;
			case PushbulletSettings s:
				Require(errors, s.ApiKey, "apiKey");
				break;
			case GotifySettings s:
				Require(errors, s.Server, "server");
				Require(errors, s.AppToken, "appToken");
				break;
			case KodiSettings s:
				Require(errors, s.Host, "host");
				break;
			case CustomScriptSettings s:
				Require(errors, s.Path, "path");
				break;
			case PlexSettings s:
				Require(errors, s.Host, "host");
				Require(errors, s.AuthToken, "authToken");
				break;
			case EmbySettings s:
				Require(errors, s.Host, "host");
				Require(errors, s.ApiKey, "apiKey");
				break;
			case JellyfinSettings s:
				Require(errors, s.Host, "host");
				Require(errors, s.ApiKey, "apiKey");
				break;
			case EmailSettings s:
				Require(errors, s.Server, "server");
				Require(errors, s.From, "from");
				if (s.To.Count == 0)
				{
					Add(errors, "to", "must not be empty");
				}

				break;
			case NtfySettings s:
				if (s.Topics.Count == 0)
				{
					Add(errors, "topics", "must not be empty");
				}

				if (s.Priority is < 1 or > 5)
				{
					Add(errors, "priority", "must be between 1 and 5");
				}

				break;
			case AppriseSettings s:
				Require(errors, s.ServerUrl, "serverUrl");
				if (string.IsNullOrWhiteSpace(s.ConfigurationKey) && s.StatelessUrls.Count == 0)
				{
					Add(errors, "statelessUrls", "must not be empty when no configuration key is set");
				}

				break;
		}
	}

	private interface IEndpoint
	{
		int Port { get; }
	}

	private static void Require(Dictionary<string, List<string>> errors, string? value, string field)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			Add(errors, field, "is required");
		}
	}

	private static void Add(Dictionary<string, List<string>> errors, string field, string message)
	{
		if (!errors.TryGetValue(field, out var list))
		{
			list = [];
			errors.Add(field, list);
		}

		list.Add(message);
	}

	private static NotificationFieldDescriptor Text(string name, string label, bool required = false, string? help = null)
		=> new(name, label, "text", null, required, help, null);

	private static NotificationFieldDescriptor Password(string name, string label, bool required = false, string? help = null)
		=> new(name, label, "password", null, required, help, null);

	private static NotificationFieldDescriptor Number(string name, string label, bool required = false, string? help = null, string? @default = null)
		=> new(name, label, "number", null, required, help, @default);

	private static NotificationFieldDescriptor Select(string name, string label, bool required, string[] options, string? @default = null)
		=> new(name, label, "select", options, required, null, @default);

	private static NotificationFieldDescriptor Checkbox(string name, string label, bool @default = false)
		=> new(name, label, "checkbox", null, false, null, @default);

	private static NotificationFieldDescriptor Tags(string name, string label, bool required = false, string[]? options = null, string? help = null)
		=> new(name, label, "tags", options, required, help, null);

	private static NotificationFieldDescriptor Url(string name, string label, bool required = false, string? help = null, string? @default = null)
		=> new(name, label, "url", null, required, help, @default);
}
