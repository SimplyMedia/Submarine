using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Submarine.Core.Notification;

namespace Submarine.Api.Clients;

/// <summary>
///     Creates the matching <see cref="INotificationSender" /> for a notification <see cref="Connection" /> row
/// </summary>
public class NotificationSenderFactory : INotificationSenderFactory
{
	private readonly IHttpClientFactory _httpClientFactory;

	/// <summary>
	///     Creates a new instance of <see cref="NotificationSenderFactory" />
	/// </summary>
	/// <param name="httpClientFactory">factory providing the "notification" http client</param>
	public NotificationSenderFactory(IHttpClientFactory httpClientFactory)
		=> _httpClientFactory = httpClientFactory;

	/// <inheritdoc />
	public INotificationSender Create(Connection connection)
	{
		var httpClient = _httpClientFactory.CreateClient("notification");

		return connection switch
		{
			DiscordConnection discord => new DiscordNotificationSender(httpClient, discord),
			TelegramConnection telegram => new TelegramNotificationSender(httpClient, telegram),
			WebhookConnection webhook => new WebhookNotificationSender(httpClient, webhook),
			_ => throw new ArgumentOutOfRangeException(nameof(connection),
				$"unknown notification connection type {connection.Type}")
		};
	}
}

/// <summary>
///     <see cref="INotificationSender" /> for Discord, posting an embed to an incoming webhook
/// </summary>
public class DiscordNotificationSender : INotificationSender
{
	private const int EmbedColor = 0x3498DB;

	private readonly HttpClient _httpClient;
	private readonly DiscordConnection _connection;

	/// <summary>
	///     Creates a new instance of <see cref="DiscordNotificationSender" />
	/// </summary>
	/// <param name="httpClient">http client to post the webhook with</param>
	/// <param name="connection">connection describing the Discord webhook</param>
	public DiscordNotificationSender(HttpClient httpClient, DiscordConnection connection)
	{
		_httpClient = httpClient;
		_connection = connection;
	}

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
	{
		using var response = await _httpClient.PostAsJsonAsync(_connection.WebhookUrl, new
		{
			embeds = new[]
			{
				new { title = message.DisplayText, description = message.Path ?? string.Empty, color = EmbedColor }
			}
		}, cancellationToken);

		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(CancellationToken cancellationToken = default)
		=> SendAsync(new NotificationMessage("test", "Submarine test notification", null, null, DateTimeOffset.UtcNow),
			cancellationToken);
}

/// <summary>
///     <see cref="INotificationSender" /> for Telegram, sending a message via a bot
/// </summary>
public class TelegramNotificationSender : INotificationSender
{
	private readonly HttpClient _httpClient;
	private readonly TelegramConnection _connection;

	/// <summary>
	///     Creates a new instance of <see cref="TelegramNotificationSender" />
	/// </summary>
	/// <param name="httpClient">http client to talk to the Telegram Bot API with</param>
	/// <param name="connection">connection describing the Telegram bot and chat</param>
	public TelegramNotificationSender(HttpClient httpClient, TelegramConnection connection)
	{
		_httpClient = httpClient;
		_connection = connection;
	}

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
	{
		var uri = new Uri($"https://api.telegram.org/bot{_connection.BotToken}/sendMessage");

		using var response = await _httpClient.PostAsJsonAsync(uri, new
		{
			chat_id = _connection.ChatId,
			text = message.DisplayText
		}, cancellationToken);

		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(CancellationToken cancellationToken = default)
		=> SendAsync(new NotificationMessage("test", "Submarine test notification", null, null, DateTimeOffset.UtcNow),
			cancellationToken);
}

/// <summary>
///     <see cref="INotificationSender" /> for a generic webhook, posting a JSON payload with an optional basic auth
/// </summary>
public class WebhookNotificationSender : INotificationSender
{
	private readonly HttpClient _httpClient;
	private readonly WebhookConnection _connection;

	/// <summary>
	///     Creates a new instance of <see cref="WebhookNotificationSender" />
	/// </summary>
	/// <param name="httpClient">http client to send the webhook request with</param>
	/// <param name="connection">connection describing the webhook endpoint</param>
	public WebhookNotificationSender(HttpClient httpClient, WebhookConnection connection)
	{
		_httpClient = httpClient;
		_connection = connection;
	}

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(new HttpMethod(_connection.Method), _connection.Url)
		{
			Content = JsonContent.Create(new
			{
				eventType = message.EventType,
				title = message.Title,
				path = message.Path,
				quality = message.Quality,
				timestamp = message.Timestamp
			})
		};

		if (!string.IsNullOrEmpty(_connection.Username))
		{
			var credentials = Convert.ToBase64String(
				Encoding.UTF8.GetBytes($"{_connection.Username}:{_connection.Password}"));
			request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
		}

		using var response = await _httpClient.SendAsync(request, cancellationToken);

		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(CancellationToken cancellationToken = default)
		=> SendAsync(new NotificationMessage("test", "Submarine test notification", null, null, DateTimeOffset.UtcNow),
			cancellationToken);
}
