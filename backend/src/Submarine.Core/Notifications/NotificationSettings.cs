using Submarine.Core.Enums;

namespace Submarine.Core.Notifications;

/// <summary>
///     Base type of all notification settings records.
/// </summary>
public abstract record NotificationSettings;

/// <summary>Discord webhook settings.</summary>
public sealed record DiscordSettings : NotificationSettings
{
	/// <summary>Incoming webhook url.</summary>
	public string WebhookUrl { get; init; } = string.Empty;

	/// <summary>Override of the webhook username.</summary>
	public string? Username { get; init; }

	/// <summary>Override of the webhook avatar.</summary>
	public string? Avatar { get; init; }

	/// <summary>Embed fields included on grab events.</summary>
	public List<string> GrabFields { get; init; } = ["overview", "quality", "languages", "releaseGroup", "indexer", "size", "links"];

	/// <summary>Embed fields included on import and other events.</summary>
	public List<string> ImportFields { get; init; } = ["overview", "quality", "languages", "releaseGroup", "size", "links"];
}

/// <summary>Telegram bot settings.</summary>
public sealed record TelegramSettings : NotificationSettings
{
	/// <summary>Bot api token.</summary>
	public string BotToken { get; init; } = string.Empty;

	/// <summary>Target chat id.</summary>
	public string ChatId { get; init; } = string.Empty;

	/// <summary>Forum topic id, when the chat is a forum.</summary>
	public long? TopicId { get; init; }

	/// <summary>Send without sound.</summary>
	public bool SendSilently { get; init; }
}

/// <summary>Generic webhook settings.</summary>
public sealed record WebhookSettings : NotificationSettings
{
	/// <summary>Target url.</summary>
	public string Url { get; init; } = string.Empty;

	/// <summary>Http method, POST or PUT.</summary>
	public string Method { get; init; } = "POST";

	/// <summary>Basic auth username.</summary>
	public string? Username { get; init; }

	/// <summary>Basic auth password.</summary>
	public string? Password { get; init; }

	/// <summary>Extra request headers.</summary>
	public Dictionary<string, string> Headers { get; init; } = [];
}

/// <summary>Slack webhook settings.</summary>
public sealed record SlackSettings : NotificationSettings
{
	/// <summary>Incoming webhook url.</summary>
	public string WebhookUrl { get; init; } = string.Empty;

	/// <summary>Override of the sender username.</summary>
	public string? Username { get; init; }

	/// <summary>Icon, emoji like :rocket: or an image url.</summary>
	public string? Icon { get; init; }

	/// <summary>Override of the target channel.</summary>
	public string? Channel { get; init; }
}

/// <summary>Pushover settings.</summary>
public sealed record PushoverSettings : NotificationSettings
{
	/// <summary>Application api token.</summary>
	public string ApiKey { get; init; } = string.Empty;

	/// <summary>User key.</summary>
	public string UserKey { get; init; } = string.Empty;

	/// <summary>Specific devices to notify, empty for all.</summary>
	public List<string> Devices { get; init; } = [];

	/// <summary>Message priority between -2 and 2.</summary>
	public int Priority { get; init; }

	/// <summary>Notification sound name.</summary>
	public string? Sound { get; init; }

	/// <summary>Retry interval in seconds for emergency priority.</summary>
	public int? Retry { get; init; }

	/// <summary>Expiration in seconds for emergency priority.</summary>
	public int? Expire { get; init; }
}

/// <summary>Pushbullet settings.</summary>
public sealed record PushbulletSettings : NotificationSettings
{
	/// <summary>Account access token.</summary>
	public string ApiKey { get; init; } = string.Empty;

	/// <summary>Device idens to push to, empty for all.</summary>
	public List<string> DeviceIds { get; init; } = [];

	/// <summary>Channel tags to push to.</summary>
	public List<string> ChannelTags { get; init; } = [];

	/// <summary>Source device iden reported as the sender.</summary>
	public string? SenderId { get; init; }
}

/// <summary>Gotify settings.</summary>
public sealed record GotifySettings : NotificationSettings
{
	/// <summary>Base url of the Gotify server.</summary>
	public string Server { get; init; } = string.Empty;

	/// <summary>Application token.</summary>
	public string AppToken { get; init; } = string.Empty;

	/// <summary>Message priority.</summary>
	public int Priority { get; init; } = 5;

	/// <summary>Attach the series poster as the big image.</summary>
	public bool IncludeSeriesPoster { get; init; }
}

/// <summary>Kodi settings.</summary>
public sealed record KodiSettings : NotificationSettings
{
	/// <summary>Hostname or ip.</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>JSON-RPC port.</summary>
	public int Port { get; init; } = 8080;

	/// <summary>Connect using https.</summary>
	public bool UseSsl { get; init; }

	/// <summary>Http basic auth username.</summary>
	public string? Username { get; init; }

	/// <summary>Http basic auth password.</summary>
	public string? Password { get; init; }

	/// <summary>Notification display time in seconds.</summary>
	public int DisplayTime { get; init; } = 5;

	/// <summary>Show on screen notifications.</summary>
	public bool Notify { get; init; } = true;

	/// <summary>Trigger a library scan on media events.</summary>
	public bool UpdateLibrary { get; init; } = true;

	/// <summary>Trigger a library clean on delete events.</summary>
	public bool CleanLibrary { get; init; }

	/// <summary>Update the library even on non media events.</summary>
	public bool AlwaysUpdate { get; init; }
}

/// <summary>Custom script settings.</summary>
public sealed record CustomScriptSettings : NotificationSettings
{
	/// <summary>Absolute path of the script.</summary>
	public string Path { get; init; } = string.Empty;

	/// <summary>Arguments passed to the script.</summary>
	public string? Arguments { get; init; }
}

/// <summary>Plex server settings.</summary>
public sealed record PlexSettings : NotificationSettings
{
	/// <summary>Hostname or ip.</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>HTTP port.</summary>
	public int Port { get; init; } = 32400;

	/// <summary>Connect using https.</summary>
	public bool UseSsl { get; init; }

	/// <summary>Plex authentication token.</summary>
	public string AuthToken { get; init; } = string.Empty;

	/// <summary>Refresh the library on media events.</summary>
	public bool UpdateLibrary { get; init; } = true;

	/// <summary>Path prefix inside Submarine to translate, empty to disable.</summary>
	public string? MapFrom { get; init; }

	/// <summary>Path prefix the Plex server sees.</summary>
	public string? MapTo { get; init; }
}

/// <summary>Emby server settings.</summary>
public sealed record EmbySettings : NotificationSettings
{
	/// <summary>Hostname or ip.</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>HTTP port.</summary>
	public int Port { get; init; } = 8096;

	/// <summary>Connect using https.</summary>
	public bool UseSsl { get; init; }

	/// <summary>Server api key.</summary>
	public string ApiKey { get; init; } = string.Empty;

	/// <summary>Show server notifications.</summary>
	public bool Notify { get; init; } = true;

	/// <summary>Refresh the library on media events.</summary>
	public bool UpdateLibrary { get; init; } = true;

	/// <summary>Path prefix inside Submarine to translate, empty to disable.</summary>
	public string? MapFrom { get; init; }

	/// <summary>Path prefix the server sees.</summary>
	public string? MapTo { get; init; }
}

/// <summary>Jellyfin server settings.</summary>
public sealed record JellyfinSettings : NotificationSettings
{
	/// <summary>Hostname or ip.</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>HTTP port.</summary>
	public int Port { get; init; } = 8096;

	/// <summary>Connect using https.</summary>
	public bool UseSsl { get; init; }

	/// <summary>Server api key.</summary>
	public string ApiKey { get; init; } = string.Empty;

	/// <summary>Show server notifications.</summary>
	public bool Notify { get; init; } = true;

	/// <summary>Refresh the library on media events.</summary>
	public bool UpdateLibrary { get; init; } = true;

	/// <summary>Path prefix inside Submarine to translate, empty to disable.</summary>
	public string? MapFrom { get; init; }

	/// <summary>Path prefix the server sees.</summary>
	public string? MapTo { get; init; }
}

/// <summary>SMTP email settings.</summary>
public sealed record EmailSettings : NotificationSettings
{
	/// <summary>Encryption mode.</summary>
	public enum EncryptionType
	{
		/// <summary>No encryption.</summary>
		NONE,

		/// <summary>STARTTLS upgrade.</summary>
		START_TLS,

		/// <summary>Implicit TLS.</summary>
		SSL
	}

	/// <summary>SMTP server host.</summary>
	public string Server { get; init; } = string.Empty;

	/// <summary>SMTP server port.</summary>
	public int Port { get; init; } = 587;

	/// <summary>Connection encryption.</summary>
	public EncryptionType UseEncryption { get; init; } = EncryptionType.START_TLS;

	/// <summary>SMTP username.</summary>
	public string? Username { get; init; }

	/// <summary>SMTP password.</summary>
	public string? Password { get; init; }

	/// <summary>From address.</summary>
	public string From { get; init; } = string.Empty;

	/// <summary>To addresses.</summary>
	public List<string> To { get; init; } = [];

	/// <summary>Cc addresses.</summary>
	public List<string> Cc { get; init; } = [];

	/// <summary>Bcc addresses.</summary>
	public List<string> Bcc { get; init; } = [];
}

/// <summary>ntfy settings.</summary>
public sealed record NtfySettings : NotificationSettings
{
	/// <summary>Base url of the ntfy server.</summary>
	public string ServerUrl { get; init; } = "https://ntfy.sh";

	/// <summary>Topics to publish to.</summary>
	public List<string> Topics { get; init; } = [];

	/// <summary>Username for access control.</summary>
	public string? Username { get; init; }

	/// <summary>Password for access control.</summary>
	public string? Password { get; init; }

	/// <summary>Access token, preferred over username and password.</summary>
	public string? AccessToken { get; init; }

	/// <summary>Message priority between 1 and 5.</summary>
	public int Priority { get; init; } = 3;

	/// <summary>Url opened when the notification is clicked.</summary>
	public string? ClickUrl { get; init; }
}

/// <summary>Apprise settings.</summary>
public sealed record AppriseSettings : NotificationSettings
{
	/// <summary>Base url of the Apprise API server.</summary>
	public string ServerUrl { get; init; } = string.Empty;

	/// <summary>Persistent configuration key, empty for stateless notifications.</summary>
	public string? ConfigurationKey { get; init; }

	/// <summary>Stateless target urls, used when no configuration key is set.</summary>
	public List<string> StatelessUrls { get; init; } = [];

	/// <summary>Apprise notification type.</summary>
	public string NotificationType { get; init; } = "info";

	/// <summary>Limit delivery to targets with these tags.</summary>
	public List<string> Tags { get; init; } = [];
}
