using System.Diagnostics;
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
	private readonly ILoggerFactory _loggerFactory;

	/// <summary>
	///     Creates a new instance of <see cref="NotificationSenderFactory" />
	/// </summary>
	/// <param name="httpClientFactory">factory providing the "notification" http client</param>
	/// <param name="loggerFactory">factory providing loggers for the created senders</param>
	public NotificationSenderFactory(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
	{
		_httpClientFactory = httpClientFactory;
		_loggerFactory = loggerFactory;
	}

	/// <inheritdoc />
	public INotificationSender Create(Connection connection)
	{
		var httpClient = _httpClientFactory.CreateClient("notification");

		return connection switch
		{
			DiscordConnection discord => new DiscordNotificationSender(httpClient, discord),
			TelegramConnection telegram => new TelegramNotificationSender(httpClient, telegram),
			WebhookConnection webhook => new WebhookNotificationSender(httpClient, webhook),
			SlackConnection slack => new SlackNotificationSender(httpClient, slack),
			PushoverConnection pushover => new PushoverNotificationSender(httpClient, pushover),
			PushbulletConnection pushbullet => new PushbulletNotificationSender(httpClient, pushbullet),
			GotifyConnection gotify => new GotifyNotificationSender(httpClient, gotify),
			KodiConnection kodi => new KodiNotificationSender(httpClient, kodi),
			CustomScriptConnection customScript => new CustomScriptNotificationSender(customScript,
				_loggerFactory.CreateLogger<CustomScriptNotificationSender>()),
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
///     <see cref="INotificationSender" /> for Slack, posting a message to an incoming webhook
/// </summary>
public class SlackNotificationSender : INotificationSender
{
	private readonly HttpClient _httpClient;
	private readonly SlackConnection _connection;

	/// <summary>
	///     Creates a new instance of <see cref="SlackNotificationSender" />
	/// </summary>
	/// <param name="httpClient">http client to post the webhook with</param>
	/// <param name="connection">connection describing the Slack webhook</param>
	public SlackNotificationSender(HttpClient httpClient, SlackConnection connection)
	{
		_httpClient = httpClient;
		_connection = connection;
	}

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
	{
		using var response = await _httpClient.PostAsJsonAsync(_connection.WebhookUrl, new
		{
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
///     <see cref="INotificationSender" /> for Pushover, sending a push notification via the message API
/// </summary>
public class PushoverNotificationSender : INotificationSender
{
	private readonly HttpClient _httpClient;
	private readonly PushoverConnection _connection;

	/// <summary>
	///     Creates a new instance of <see cref="PushoverNotificationSender" />
	/// </summary>
	/// <param name="httpClient">http client to talk to the Pushover API with</param>
	/// <param name="connection">connection describing the Pushover application and user</param>
	public PushoverNotificationSender(HttpClient httpClient, PushoverConnection connection)
	{
		_httpClient = httpClient;
		_connection = connection;
	}

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
	{
		using var content = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["token"] = _connection.AppToken,
			["user"] = _connection.UserKey,
			["message"] = message.DisplayText,
			["title"] = "Submarine"
		});

		using var response = await _httpClient.PostAsync(new Uri("https://api.pushover.net/1/messages.json"), content,
			cancellationToken);

		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(CancellationToken cancellationToken = default)
		=> SendAsync(new NotificationMessage("test", "Submarine test notification", null, null, DateTimeOffset.UtcNow),
			cancellationToken);
}

/// <summary>
///     <see cref="INotificationSender" /> for Pushbullet, pushing a note via the pushes API
/// </summary>
public class PushbulletNotificationSender : INotificationSender
{
	private readonly HttpClient _httpClient;
	private readonly PushbulletConnection _connection;

	/// <summary>
	///     Creates a new instance of <see cref="PushbulletNotificationSender" />
	/// </summary>
	/// <param name="httpClient">http client to talk to the Pushbullet API with</param>
	/// <param name="connection">connection describing the Pushbullet account</param>
	public PushbulletNotificationSender(HttpClient httpClient, PushbulletConnection connection)
	{
		_httpClient = httpClient;
		_connection = connection;
	}

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("https://api.pushbullet.com/v2/pushes"))
		{
			Content = JsonContent.Create(new
			{
				type = "note",
				title = "Submarine",
				body = message.DisplayText
			})
		};

		request.Headers.Add("Access-Token", _connection.AccessToken);

		using var response = await _httpClient.SendAsync(request, cancellationToken);

		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(CancellationToken cancellationToken = default)
		=> SendAsync(new NotificationMessage("test", "Submarine test notification", null, null, DateTimeOffset.UtcNow),
			cancellationToken);
}

/// <summary>
///     <see cref="INotificationSender" /> for a self-hosted Gotify server
/// </summary>
public class GotifyNotificationSender : INotificationSender
{
	private readonly HttpClient _httpClient;
	private readonly GotifyConnection _connection;

	/// <summary>
	///     Creates a new instance of <see cref="GotifyNotificationSender" />
	/// </summary>
	/// <param name="httpClient">http client to talk to the Gotify server with</param>
	/// <param name="connection">connection describing the Gotify server and application</param>
	public GotifyNotificationSender(HttpClient httpClient, GotifyConnection connection)
	{
		_httpClient = httpClient;
		_connection = connection;
	}

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
	{
		var uri = new Uri($"{_connection.ServerUrl.TrimEnd('/')}/message?token={_connection.AppToken}");

		using var response = await _httpClient.PostAsJsonAsync(uri, new
		{
			title = "Submarine",
			message = message.DisplayText,
			priority = 5
		}, cancellationToken);

		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(CancellationToken cancellationToken = default)
		=> SendAsync(new NotificationMessage("test", "Submarine test notification", null, null, DateTimeOffset.UtcNow),
			cancellationToken);
}

/// <summary>
///     <see cref="INotificationSender" /> for Kodi, showing a GUI notification via JSON-RPC and triggering a
///     library scan on imports and upgrades
/// </summary>
public class KodiNotificationSender : INotificationSender
{
	private readonly HttpClient _httpClient;
	private readonly KodiConnection _connection;

	/// <summary>
	///     Creates a new instance of <see cref="KodiNotificationSender" />
	/// </summary>
	/// <param name="httpClient">http client to talk to the Kodi JSON-RPC API with</param>
	/// <param name="connection">connection describing the Kodi host</param>
	public KodiNotificationSender(HttpClient httpClient, KodiConnection connection)
	{
		_httpClient = httpClient;
		_connection = connection;
	}

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
	{
		await PostRpcAsync(new
		{
			jsonrpc = "2.0",
			method = "GUI.ShowNotification",
			@params = new { title = "Submarine", message = message.DisplayText },
			id = 1
		}, cancellationToken);

		if (message.EventType is "import" or "upgrade")
			await PostRpcAsync(new
			{
				jsonrpc = "2.0",
				method = "VideoLibrary.Scan",
				id = 2
			}, cancellationToken);
	}

	/// <inheritdoc />
	public Task TestAsync(CancellationToken cancellationToken = default)
		=> SendAsync(new NotificationMessage("test", "Submarine test notification", null, null, DateTimeOffset.UtcNow),
			cancellationToken);

	private async Task PostRpcAsync<TPayload>(TPayload payload, CancellationToken cancellationToken)
	{
		var scheme = _connection.UseSsl ? "https" : "http";
		var uri = new Uri($"{scheme}://{_connection.Host}:{_connection.Port}/jsonrpc");

		using var request = new HttpRequestMessage(HttpMethod.Post, uri)
		{
			Content = JsonContent.Create(payload)
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
}

/// <summary>
///     <see cref="INotificationSender" /> executing a local script with the event passed as environment variables
/// </summary>
public class CustomScriptNotificationSender : INotificationSender
{
	private static readonly TimeSpan ScriptTimeout = TimeSpan.FromSeconds(60);

	private readonly CustomScriptConnection _connection;
	private readonly ILogger<CustomScriptNotificationSender> _logger;

	/// <summary>
	///     Creates a new instance of <see cref="CustomScriptNotificationSender" />
	/// </summary>
	/// <param name="connection">connection describing the script to execute</param>
	/// <param name="logger">logger of this sender</param>
	public CustomScriptNotificationSender(CustomScriptConnection connection,
		ILogger<CustomScriptNotificationSender> logger)
	{
		_connection = connection;
		_logger = logger;
	}

	/// <inheritdoc />
	public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
		=> RunAsync(BuildStartInfo(message), cancellationToken);

	/// <inheritdoc />
	public Task TestAsync(CancellationToken cancellationToken = default)
		=> SendAsync(new NotificationMessage("test", "Submarine test notification", null, null, DateTimeOffset.UtcNow),
			cancellationToken);

	internal ProcessStartInfo BuildStartInfo(NotificationMessage message)
	{
		var startInfo = new ProcessStartInfo(_connection.ScriptPath)
		{
			UseShellExecute = false,
			CreateNoWindow = true
		};

		startInfo.EnvironmentVariables["SUBMARINE_EVENT_TYPE"] = message.EventType;
		startInfo.EnvironmentVariables["SUBMARINE_MEDIA_TITLE"] = message.Title;
		startInfo.EnvironmentVariables["SUBMARINE_MEDIA_PATH"] = message.Path ?? "";
		startInfo.EnvironmentVariables["SUBMARINE_SERIES_ID"] = message.SeriesId?.ToString() ?? "";
		startInfo.EnvironmentVariables["SUBMARINE_MOVIE_ID"] = message.MovieId?.ToString() ?? "";

		return startInfo;
	}

	internal async Task RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
	{
		using var process = Process.Start(startInfo)
		                    ?? throw new InvalidOperationException($"Failed to start script {startInfo.FileName}");

		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(ScriptTimeout);

		try
		{
			await process.WaitForExitAsync(timeout.Token);
		}
		catch (OperationCanceledException)
		{
			try
			{
				process.Kill(entireProcessTree: true);
			}
			catch (InvalidOperationException)
			{
				// process exited between the cancellation and the kill
			}

			if (cancellationToken.IsCancellationRequested)
				throw;

			_logger.LogWarning("Script {Script} timed out after {Timeout} and was killed", startInfo.FileName,
				ScriptTimeout);

			return;
		}

		if (process.ExitCode != 0)
			_logger.LogWarning("Script {Script} exited with code {ExitCode}", startInfo.FileName, process.ExitCode);
	}
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
