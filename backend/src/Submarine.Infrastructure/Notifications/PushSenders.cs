using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Submarine.Core.Enums;
using Submarine.Core.Notifications;

namespace Submarine.Infrastructure.Notifications;

/// <summary>Pushover sender using the messages API.</summary>
public sealed class PushoverSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	private const string ApiUrl = "https://api.pushover.net/1/messages.json";

	/// <inheritdoc />
	public NotificationType Type => NotificationType.PUSHOVER;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (PushoverSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		List<string?> devices = settings.Devices.Count > 0 ? [.. settings.Devices] : [null];
		foreach (var device in devices)
		{
			var fields = new Dictionary<string, string>
			{
				["token"] = settings.ApiKey,
				["user"] = settings.UserKey,
				["title"] = message.Title,
				["message"] = message.Body,
				["priority"] = settings.Priority.ToString()
			};
			if (!string.IsNullOrEmpty(settings.Sound))
			{
				fields["sound"] = settings.Sound;
			}

			if (!string.IsNullOrEmpty(device))
			{
				fields["device"] = device;
			}

			if (settings.Priority == 2)
			{
				if (settings.Retry is { } retry)
				{
					fields["retry"] = retry.ToString();
				}

				if (settings.Expire is { } expire)
				{
					fields["expire"] = expire.ToString();
				}
			}

			using var response = await client.PostAsync(ApiUrl, new FormUrlEncodedContent(fields), cancellationToken);
			response.EnsureSuccessStatusCode();
		}
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}

/// <summary>Pushbullet sender pushing notes.</summary>
public sealed class PushbulletSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	private const string ApiUrl = "https://api.pushbullet.com/v2/pushes";

	/// <inheritdoc />
	public NotificationType Type => NotificationType.PUSHBULLET;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (PushbulletSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);

		List<(string? Key, string? Value)> targets = settings.DeviceIds
			.Select(id => ((string?)"device_iden", (string?)id))
			.Concat(settings.ChannelTags.Select(tag => ((string?)"channel_tag", (string?)tag)))
			.ToList();
		if (targets.Count == 0)
		{
			targets.Add((null, null));
		}

		foreach (var (key, value) in targets)
		{
			var payload = new Dictionary<string, object?>
			{
				["type"] = "note",
				["title"] = message.Title,
				["body"] = message.Body
			};
			if (key is not null)
			{
				payload[key] = value;
			}

			if (!string.IsNullOrEmpty(settings.SenderId))
			{
				payload["source_device_iden"] = settings.SenderId;
			}

			using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
			{
				Content = JsonContent.Create(payload)
			};
			request.Headers.Add("Access-Token", settings.ApiKey);
			using var response = await client.SendAsync(request, cancellationToken);
			response.EnsureSuccessStatusCode();
		}
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}

/// <summary>Gotify sender posting to the message endpoint.</summary>
public sealed class GotifySender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	/// <inheritdoc />
	public NotificationType Type => NotificationType.GOTIFY;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (GotifySettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var payload = new Dictionary<string, object?>
		{
			["title"] = message.Title,
			["message"] = message.Body,
			["priority"] = settings.Priority
		};
		if (settings.IncludeSeriesPoster && message.ImageUrl is not null)
		{
			payload["extras"] = new Dictionary<string, object>
			{
				["client::notification"] = new { bigImageUrl = message.ImageUrl }
			};
		}

		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		var url = $"{settings.Server.TrimEnd('/')}/message?token={Uri.EscapeDataString(settings.AppToken)}";
		using var response = await client.PostAsJsonAsync(url, payload, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}

/// <summary>ntfy sender publishing to topics.</summary>
public sealed class NtfySender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	/// <inheritdoc />
	public NotificationType Type => NotificationType.NTFY;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (NtfySettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		foreach (var topic in settings.Topics)
		{
			using var request = new HttpRequestMessage(
				HttpMethod.Post,
				$"{settings.ServerUrl.TrimEnd('/')}/{Uri.EscapeDataString(topic)}")
			{
				Content = new StringContent($"{message.Title}\n{message.Body}", Encoding.UTF8, "text/plain")
			};
			request.Headers.Add("X-Title", message.Title);
			request.Headers.Add("X-Priority", settings.Priority.ToString());
			if (!string.IsNullOrEmpty(settings.ClickUrl))
			{
				request.Headers.Add("X-Click", settings.ClickUrl);
			}

			if (!string.IsNullOrEmpty(settings.AccessToken))
			{
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
			}
			else if (!string.IsNullOrEmpty(settings.Username))
			{
				request.Headers.Authorization = NotificationSenderHttp.BasicAuth(settings.Username, settings.Password);
			}

			using var response = await client.SendAsync(request, cancellationToken);
			response.EnsureSuccessStatusCode();
		}
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}

/// <summary>Apprise API sender.</summary>
public sealed class AppriseSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	/// <inheritdoc />
	public NotificationType Type => NotificationType.APPRISE;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (AppriseSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		var baseUrl = settings.ServerUrl.TrimEnd('/');

		Dictionary<string, object?> payload;
		string url;
		if (!string.IsNullOrEmpty(settings.ConfigurationKey))
		{
			url = $"{baseUrl}/notify/{Uri.EscapeDataString(settings.ConfigurationKey)}";
			payload = Payload(message, settings, includeUrls: false);
		}
		else
		{
			url = $"{baseUrl}/notify";
			payload = Payload(message, settings, includeUrls: true);
		}

		using var response = await client.PostAsJsonAsync(url, payload, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	private static Dictionary<string, object?> Payload(
		NotificationMessage message,
		AppriseSettings settings,
		bool includeUrls)
	{
		var payload = new Dictionary<string, object?>();
		if (includeUrls)
		{
			payload["urls"] = string.Join(", ", settings.StatelessUrls);
		}

		payload["title"] = message.Title;
		payload["body"] = message.Body;
		payload["type"] = settings.NotificationType;
		if (settings.Tags.Count > 0)
		{
			payload["tag"] = string.Join(", ", settings.Tags);
		}

		return payload;
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}
