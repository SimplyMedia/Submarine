using System.Net.Http.Json;
using Submarine.Core.Enums;
using Submarine.Core.Notifications;

namespace Submarine.Infrastructure.Notifications;

/// <summary>Join push notification sender.</summary>
public sealed class JoinSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	private const string ApiUrl = "https://joinjoaomgcd.appspot.com/_ah/api/messaging/v1/sendPush";

	/// <inheritdoc />
	public NotificationType Type => NotificationType.JOIN;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (JoinSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		var fields = new Dictionary<string, string>
		{
			["apikey"] = settings.ApiKey,
			["title"] = message.Title,
			["text"] = message.Body,
			["priority"] = settings.Priority.ToString()
		};
		fields[string.IsNullOrEmpty(settings.DeviceNames) ? "deviceId" : "deviceNames"]
			= string.IsNullOrEmpty(settings.DeviceNames) ? "group.all" : settings.DeviceNames;

		using var response = await client.PostAsync(new Uri($"{ApiUrl}?{string.Join('&', fields.Select(f => $"{f.Key}={Uri.EscapeDataString(f.Value)}"))}"), null, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}

/// <summary>Prowl push notification sender.</summary>
public sealed class ProwlSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	private const string ApiUrl = "https://api.prowlapp.com/publicapi/add";

	/// <inheritdoc />
	public NotificationType Type => NotificationType.PROWL;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (ProwlSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		var fields = new Dictionary<string, string>
		{
			["apikey"] = settings.ApiKey,
			["application"] = "Submarine",
			["event"] = message.Title,
			["description"] = message.Body,
			["priority"] = settings.Priority.ToString()
		};

		using var response = await client.PostAsync(ApiUrl, new FormUrlEncodedContent(fields), cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}

/// <summary>Pushcut push notification sender.</summary>
public sealed class PushcutSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	/// <inheritdoc />
	public NotificationType Type => NotificationType.PUSHCUT;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (PushcutSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		var payload = new Dictionary<string, object?>
		{
			["title"] = message.Title,
			["text"] = message.Body,
			["isTimeSensitive"] = settings.TimeSensitive
		};

		using var request = new HttpRequestMessage(
			HttpMethod.Post,
			$"https://api.pushcut.io/v1/notifications/{Uri.EscapeDataString(settings.NotificationName)}")
		{
			Content = JsonContent.Create(payload)
		};
		request.Headers.Add("API-Key", settings.ApiKey);
		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}

/// <summary>Pushsafer push notification sender.</summary>
public sealed class PushsaferSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	private const string ApiUrl = "https://pushsafer.com/api";
	private const int EmergencyPriority = 2;

	/// <inheritdoc />
	public NotificationType Type => NotificationType.PUSHSAFER;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (PushsaferSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		var fields = new Dictionary<string, string>
		{
			["k"] = settings.ApiKey,
			["d"] = string.Join('|', settings.DeviceIds),
			["t"] = message.Title,
			["m"] = message.Body,
			["pr"] = settings.Priority.ToString()
		};
		if (settings.Priority == EmergencyPriority)
		{
			fields["re"] = settings.Retry.ToString();
			fields["ex"] = settings.Expire.ToString();
		}

		if (!string.IsNullOrEmpty(settings.Sound))
		{
			fields["s"] = settings.Sound;
		}

		if (!string.IsNullOrEmpty(settings.Vibration))
		{
			fields["v"] = settings.Vibration;
		}

		if (!string.IsNullOrEmpty(settings.Icon))
		{
			fields["i"] = settings.Icon;
		}

		if (!string.IsNullOrEmpty(settings.IconColor))
		{
			fields["c"] = settings.IconColor;
		}

		using var response = await client.PostAsync(ApiUrl, new FormUrlEncodedContent(fields), cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}

/// <summary>Signal messenger sender, posting to a signal-cli REST API instance.</summary>
public sealed class SignalSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	/// <inheritdoc />
	public NotificationType Type => NotificationType.SIGNAL;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (SignalSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		var url = $"{(settings.UseSsl ? "https" : "http")}://{settings.Host}:{settings.Port}/v2/send";

		using var request = new HttpRequestMessage(HttpMethod.Post, url)
		{
			Content = JsonContent.Create(new
			{
				message = $"{message.Title}\n{message.Body}",
				number = settings.SenderNumber,
				recipients = new[] { settings.ReceiverId }
			})
		};
		if (!string.IsNullOrEmpty(settings.AuthUsername) && !string.IsNullOrEmpty(settings.AuthPassword))
		{
			request.Headers.Authorization = NotificationSenderHttp.BasicAuth(settings.AuthUsername, settings.AuthPassword);
		}

		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}

/// <summary>Simplepush push notification sender.</summary>
public sealed class SimplepushSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	private const string ApiUrl = "https://api.simplepush.io/send";

	/// <inheritdoc />
	public NotificationType Type => NotificationType.SIMPLEPUSH;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (SimplepushSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		var fields = new Dictionary<string, string>
		{
			["key"] = settings.Key,
			["title"] = message.Title,
			["msg"] = message.Body
		};
		if (!string.IsNullOrEmpty(settings.Event))
		{
			fields["event"] = settings.Event;
		}

		using var response = await client.PostAsync(ApiUrl, new FormUrlEncodedContent(fields), cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}
