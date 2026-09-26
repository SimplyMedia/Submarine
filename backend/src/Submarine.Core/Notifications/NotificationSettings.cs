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

/// <summary>Join push notification settings.</summary>
public sealed record JoinSettings : NotificationSettings
{
	/// <summary>Join API key.</summary>
	public string ApiKey { get; init; } = string.Empty;

	/// <summary>Comma separated device names to push to, empty for all devices.</summary>
	public string? DeviceNames { get; init; }

	/// <summary>Message priority between -2 and 2.</summary>
	public int Priority { get; init; }
}

/// <summary>Mailgun email settings.</summary>
public sealed record MailgunSettings : NotificationSettings
{
	/// <summary>Mailgun API key.</summary>
	public string ApiKey { get; init; } = string.Empty;

	/// <summary>Use the EU API endpoint instead of the US one.</summary>
	public bool UseEuEndpoint { get; init; }

	/// <summary>From address.</summary>
	public string From { get; init; } = string.Empty;

	/// <summary>Verified sending domain configured in Mailgun.</summary>
	public string SenderDomain { get; init; } = string.Empty;

	/// <summary>Recipient addresses.</summary>
	public List<string> Recipients { get; init; } = [];
}

/// <summary>Notifiarr relay settings.</summary>
public sealed record NotifiarrSettings : NotificationSettings
{
	/// <summary>Notifiarr API key.</summary>
	public string ApiKey { get; init; } = string.Empty;
}

/// <summary>Prowl settings.</summary>
public sealed record ProwlSettings : NotificationSettings
{
	/// <summary>Prowl API key.</summary>
	public string ApiKey { get; init; } = string.Empty;

	/// <summary>Message priority between -2 and 2.</summary>
	public int Priority { get; init; }
}

/// <summary>Pushcut settings.</summary>
public sealed record PushcutSettings : NotificationSettings
{
	/// <summary>Name of the Pushcut notification to trigger.</summary>
	public string NotificationName { get; init; } = string.Empty;

	/// <summary>Pushcut API key.</summary>
	public string ApiKey { get; init; } = string.Empty;

	/// <summary>Mark the notification as time sensitive.</summary>
	public bool TimeSensitive { get; init; }
}

/// <summary>Pushsafer settings.</summary>
public sealed record PushsaferSettings : NotificationSettings
{
	/// <summary>Pushsafer API key.</summary>
	public string ApiKey { get; init; } = string.Empty;

	/// <summary>Device group id or device ids to push to, empty for all devices.</summary>
	public List<string> DeviceIds { get; init; } = [];

	/// <summary>Message priority between -2 and 2.</summary>
	public int Priority { get; init; }

	/// <summary>Retry interval in seconds for emergency priority, 60 to 10800.</summary>
	public int Retry { get; init; }

	/// <summary>Maximum retry duration in seconds for emergency priority, 60 to 10800.</summary>
	public int Expire { get; init; }

	/// <summary>Notification sound number, 0 to 62.</summary>
	public string? Sound { get; init; }

	/// <summary>Vibration pattern number, 1 to 3.</summary>
	public string? Vibration { get; init; }

	/// <summary>Icon number, 1 to 181.</summary>
	public string? Icon { get; init; }

	/// <summary>Icon color in hex format.</summary>
	public string? IconColor { get; init; }
}

/// <summary>SendGrid email settings.</summary>
public sealed record SendGridSettings : NotificationSettings
{
	/// <summary>SendGrid API key.</summary>
	public string ApiKey { get; init; } = string.Empty;

	/// <summary>From address.</summary>
	public string From { get; init; } = string.Empty;

	/// <summary>Recipient addresses.</summary>
	public List<string> Recipients { get; init; } = [];
}

/// <summary>Signal messenger settings, sent through a signal-cli REST API instance.</summary>
public sealed record SignalSettings : NotificationSettings
{
	/// <summary>Hostname or ip of the signal-cli REST API.</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>HTTP port.</summary>
	public int Port { get; init; } = 8080;

	/// <summary>Connect using https.</summary>
	public bool UseSsl { get; init; }

	/// <summary>Registered sender phone number.</summary>
	public string SenderNumber { get; init; } = string.Empty;

	/// <summary>Recipient phone number or group id.</summary>
	public string ReceiverId { get; init; } = string.Empty;

	/// <summary>Basic auth username.</summary>
	public string? AuthUsername { get; init; }

	/// <summary>Basic auth password.</summary>
	public string? AuthPassword { get; init; }
}

/// <summary>Simplepush settings.</summary>
public sealed record SimplepushSettings : NotificationSettings
{
	/// <summary>Simplepush key.</summary>
	public string Key { get; init; } = string.Empty;

	/// <summary>Optional encryption password configured in the Simplepush app.</summary>
	public string? Event { get; init; }
}

/// <summary>Synology DiskStation media indexer settings.</summary>
public sealed record SynologyIndexerSettings : NotificationSettings
{
	/// <summary>Update the Synology media index on media events.</summary>
	public bool UpdateLibrary { get; init; } = true;
}

/// <summary>Twitter/X settings. Requires a user supplied developer app and access tokens.</summary>
public sealed record TwitterSettings : NotificationSettings
{
	/// <summary>OAuth1 consumer key of the Twitter developer app.</summary>
	public string ConsumerKey { get; init; } = string.Empty;

	/// <summary>OAuth1 consumer secret of the Twitter developer app.</summary>
	public string ConsumerSecret { get; init; } = string.Empty;

	/// <summary>OAuth1 access token of the authorizing account.</summary>
	public string AccessToken { get; init; } = string.Empty;

	/// <summary>OAuth1 access token secret of the authorizing account.</summary>
	public string AccessTokenSecret { get; init; } = string.Empty;

	/// <summary>Screen name mentioned in tweets or messaged directly.</summary>
	public string? Mention { get; init; }

	/// <summary>Send a direct message to <see cref="Mention" /> instead of posting a tweet.</summary>
	public bool DirectMessage { get; init; } = true;
}
