using Submarine.Api.Exceptions;
using Submarine.Core.Notification;

namespace Submarine.Api.Models.Request;

public record CreateConnectionRequest
{
	public string Name { get; set; }

	public ConnectionType Type { get; set; }

	public bool Enable { get; set; }

	public string Host { get; set; }

	public int Port { get; set; }

	public bool UseSsl { get; set; }

	public string ApiKey { get; set; }

	public bool OnGrab { get; set; }

	public bool OnImport { get; set; }

	public bool OnRename { get; set; }

	public List<string> Tags { get; set; } = new();

	public string? WebhookUrl { get; set; }

	public string? BotToken { get; set; }

	public string? ChatId { get; set; }

	public string? Url { get; set; }

	public string? Method { get; set; }

	public string? Username { get; set; }

	public string? Password { get; set; }

	public Connection ToConnection()
	{
		switch (Type)
		{
			case ConnectionType.DISCORD:
				if (string.IsNullOrWhiteSpace(WebhookUrl))
					throw new BadRequestException("WebhookUrl is required for Discord connections");

				return new DiscordConnection
				{
					Name = Name, Enable = Enable, WebhookUrl = WebhookUrl,
					OnGrab = OnGrab, OnImport = OnImport, OnRename = OnRename, Tags = Tags
				};
			case ConnectionType.TELEGRAM:
				if (string.IsNullOrWhiteSpace(BotToken) || string.IsNullOrWhiteSpace(ChatId))
					throw new BadRequestException("BotToken and ChatId are required for Telegram connections");

				return new TelegramConnection
				{
					Name = Name, Enable = Enable, BotToken = BotToken, ChatId = ChatId,
					OnGrab = OnGrab, OnImport = OnImport, OnRename = OnRename, Tags = Tags
				};
			case ConnectionType.WEBHOOK:
				if (string.IsNullOrWhiteSpace(Url))
					throw new BadRequestException("Url is required for Webhook connections");
				if (string.IsNullOrWhiteSpace(Username) != string.IsNullOrWhiteSpace(Password))
					throw new BadRequestException(
						"Username and Password must be provided together for Webhook connections");

				return new WebhookConnection
				{
					Name = Name, Enable = Enable, Url = Url, Method = string.IsNullOrWhiteSpace(Method) ? "POST" : Method,
					Username = Username, Password = Password,
					OnGrab = OnGrab, OnImport = OnImport, OnRename = OnRename, Tags = Tags
				};
			case ConnectionType.PLEX:
			case ConnectionType.EMBY:
			case ConnectionType.JELLYFIN:
				if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(ApiKey))
					throw new BadRequestException("Host and ApiKey are required for Plex, Emby and Jellyfin connections");

				return new Connection
				{
					Name = Name, Type = Type, Enable = Enable, Host = Host, Port = Port, UseSsl = UseSsl, ApiKey = ApiKey,
					OnGrab = OnGrab, OnImport = OnImport, OnRename = OnRename, Tags = Tags
				};
			default:
				throw new ArgumentOutOfRangeException();
		}
	}
}
