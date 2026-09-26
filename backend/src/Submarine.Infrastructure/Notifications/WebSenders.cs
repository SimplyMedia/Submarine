using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Submarine.Core.Common;
using Submarine.Core.Enums;
using Submarine.Core.Notifications;

namespace Submarine.Infrastructure.Notifications;

/// <summary>Discord webhook sender posting an embed.</summary>
public sealed class DiscordSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	/// <inheritdoc />
	public NotificationType Type => NotificationType.DISCORD;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (DiscordSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var embed = new Dictionary<string, object?>
		{
			["title"] = message.Title,
			["description"] = message.Body,
			["color"] = 0x3498DB
		};
		if (settings.ImportFields.Contains("poster", StringComparer.OrdinalIgnoreCase)
			&& message.ImageUrl is not null)
		{
			embed["image"] = new { url = message.ImageUrl };
		}

		var fieldNames = message.EventType == NotificationEventType.GRAB ? settings.GrabFields : settings.ImportFields;
		var fields = EmbedFields(message, fieldNames).ToList();
		if (fields.Count > 0)
		{
			embed["fields"] = fields;
		}

		var payload = new Dictionary<string, object?>();
		if (!string.IsNullOrEmpty(settings.Username))
		{
			payload["username"] = settings.Username;
		}

		if (!string.IsNullOrEmpty(settings.Avatar))
		{
			payload["avatar_url"] = settings.Avatar;
		}

		payload["embeds"] = new[] { embed };

		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		using var response = await client.PostAsJsonAsync(settings.WebhookUrl, payload, SubmarineJson.Default, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);

	internal static IEnumerable<object> EmbedFields(NotificationMessage message, IEnumerable<string> names)
	{
		foreach (var name in names)
		{
			var value = name switch
			{
				"year" => message.Year?.ToString(),
				"quality" => NotificationDispatcher.FormatQuality(message.Quality),
				"languages" => FormatLanguages(message),
				"releaseGroup" or "ReleaseGroup" => message.ReleaseGroup,
				"indexer" => message.Indexer,
				"downloadClient" => message.DownloadClient,
				"size" => message.Size is null ? null : $"{message.Size / 1024.0 / 1024.0 / 1024.0:0.00} GB",
				"links" => message.Links.Count == 0 ? null : string.Join("\n", message.Links.Select(l => $"[{l.Label}]({l.Url})")),
				"episodes" => message.Episodes.Count == 0 ? null : NotificationDispatcher.FormatEpisodes(message.Episodes),
				_ => null
			};
			if (!string.IsNullOrEmpty(value))
			{
				yield return new { name = char.ToUpperInvariant(name[0]) + name[1..], value, inline = true };
			}
		}
	}

	private static string FormatLanguages(NotificationMessage message)
		=> string.Join(", ", message.Languages.Select(l => l.ToString()));
}

/// <summary>Slack incoming webhook sender.</summary>
public sealed class SlackSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	/// <inheritdoc />
	public NotificationType Type => NotificationType.SLACK;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (SlackSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var payload = new Dictionary<string, object?> { ["text"] = $"{message.Title}\n{message.Body}" };
		if (!string.IsNullOrEmpty(settings.Username))
		{
			payload["username"] = settings.Username;
		}

		if (!string.IsNullOrEmpty(settings.Channel))
		{
			payload["channel"] = settings.Channel;
		}

		if (!string.IsNullOrEmpty(settings.Icon))
		{
			payload[settings.Icon.StartsWith(':') ? "icon_emoji" : "icon_url"] = settings.Icon;
		}

		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		using var response = await client.PostAsJsonAsync(settings.WebhookUrl, payload, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}

/// <summary>Telegram bot sender.</summary>
public sealed class TelegramSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	private const string ApiBase = "https://api.telegram.org";

	/// <inheritdoc />
	public NotificationType Type => NotificationType.TELEGRAM;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (TelegramSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var payload = new Dictionary<string, object?>
		{
			["chat_id"] = settings.ChatId,
			["text"] = $"{message.Title}\n{message.Body}",
			["disable_notification"] = settings.SendSilently
		};
		if (settings.TopicId is { } topic)
		{
			payload["message_thread_id"] = topic;
		}

		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		using var response = await client.PostAsJsonAsync(
			new Uri($"{ApiBase}/bot{settings.BotToken}/sendMessage"), payload, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}

/// <summary>Generic webhook sender posting the message as JSON.</summary>
public sealed class WebhookSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	/// <inheritdoc />
	public NotificationType Type => NotificationType.WEBHOOK;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (WebhookSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		using var request = new HttpRequestMessage(new HttpMethod(settings.Method), settings.Url)
		{
			Content = new StringContent(JsonSerializer.Serialize(message, SubmarineJson.Default), Encoding.UTF8, "application/json")
		};
		if (!string.IsNullOrEmpty(settings.Username))
		{
			request.Headers.Authorization = NotificationSenderHttp.BasicAuth(settings.Username, settings.Password);
		}

		foreach (var (name, value) in settings.Headers)
		{
			if (!request.Headers.TryAddWithoutValidation(name, value)
				&& request.Content is not null)
			{
				request.Content.Headers.TryAddWithoutValidation(name, value);
			}
		}

		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}

/// <summary>
///     Notifiarr relay sender. Posts the same generic webhook payload as <see cref="WebhookSender" /> to
///     Notifiarr's per-application ingest endpoint, authenticated with an API key header.
/// </summary>
public sealed class NotifiarrSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	// Notifiarr routes webhooks per source application; Submarine reports under the Sonarr integration
	// until Notifiarr ships a dedicated Submarine integration.
	private const string ApiUrl = "https://notifiarr.com/api/v1/notification/sonarr";

	/// <inheritdoc />
	public NotificationType Type => NotificationType.NOTIFIARR;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (NotifiarrSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
		{
			Content = new StringContent(JsonSerializer.Serialize(message, SubmarineJson.Default), Encoding.UTF8, "application/json")
		};
		request.Headers.Add("X-API-Key", settings.ApiKey);

		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}

/// <summary>Shared helpers for http based senders.</summary>
public static class NotificationSenderHttp
{
	/// <summary>Builds a basic auth header value.</summary>
	public static AuthenticationHeaderValue BasicAuth(string username, string? password)
		=> new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));

	/// <summary>The message sent by TestAsync.</summary>
	public static NotificationMessage TestMessage()
		=> new(
			NotificationEventType.TEST,
			"Test notification",
			"Submarine test notification",
			null,
			null,
			string.Empty,
			null,
			null,
			[],
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			[],
			[]);
}
