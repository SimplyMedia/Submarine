using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Submarine.Core.Enums;
using Submarine.Core.Notifications;

namespace Submarine.Infrastructure.Notifications;

/// <summary>
///     Shared HTTP access to Emby and Jellyfin style servers.
/// </summary>
public abstract class EmbyJellyfinSenderBase(IHttpClientFactory httpClientFactory) : INotificationSender
{
	private readonly JsonSerializerOptions _responseOptions = new(JsonSerializerDefaults.Web);

	/// <inheritdoc />
	public abstract NotificationType Type { get; }

	/// <summary>Builds the auth headers for a request.</summary>
	protected abstract void ApplyAuth(HttpRequestHeaders headers, string apiKey);

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = ParseSettings(settingsJson);
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		var baseUri = BaseUrl(settings);

		if (settings.Notify)
		{
			using var notification = new HttpRequestMessage(HttpMethod.Post, $"{baseUri}/Notifications/Admin")
			{
				Content = JsonContent.Create(new
				{
					Title = message.Title,
					Description = message.Body,
					ImageUrl = message.ImageUrl
				})
			};
			ApplyAuth(notification.Headers, settings.ApiKey);
			using var notificationResponse = await client.SendAsync(notification, cancellationToken);
			notificationResponse.EnsureSuccessStatusCode();
		}

		if (settings.UpdateLibrary && message.Path is not null)
		{
			using var update = new HttpRequestMessage(HttpMethod.Post, $"{baseUri}/Library/Media/Updated")
			{
				Content = JsonContent.Create(new
				{
					Updates = new[] { new { Path = MapPath(settings, message.Path) } }
				})
			};
			ApplyAuth(update.Headers, settings.ApiKey);
			using var updateResponse = await client.SendAsync(update, cancellationToken);
			updateResponse.EnsureSuccessStatusCode();
		}
	}

	/// <summary>Parses the type specific settings into the shared shape.</summary>
	protected abstract EmbySettings ParseSettings(string settingsJson);

	/// <inheritdoc />
	public async Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = ParseSettings(settingsJson);
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl(settings)}/System/Info");
		ApplyAuth(request.Headers, settings.ApiKey);
		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	internal static string MapPath(EmbySettings settings, string path)
	{
		if (string.IsNullOrEmpty(settings.MapFrom) || string.IsNullOrEmpty(settings.MapTo))
		{
			return path;
		}

		return path.StartsWith(settings.MapFrom, StringComparison.Ordinal)
			? settings.MapTo.TrimEnd('/') + path[settings.MapFrom.Length..]
			: path;
	}

	private static string BaseUrl(EmbySettings settings)
		=> $"{(settings.UseSsl ? "https" : "http")}://{settings.Host}:{settings.Port}";
}

/// <summary>Emby server sender.</summary>
public sealed class EmbySender(IHttpClientFactory httpClientFactory) : EmbyJellyfinSenderBase(httpClientFactory)
{
	/// <inheritdoc />
	public override NotificationType Type => NotificationType.EMBY;

	/// <inheritdoc />
	protected override void ApplyAuth(HttpRequestHeaders headers, string apiKey)
		=> headers.Add("X-Emby-Token", apiKey);

	/// <inheritdoc />
	protected override EmbySettings ParseSettings(string settingsJson)
		=> (EmbySettings)NotificationSettingsJson.Parse(NotificationType.EMBY, settingsJson);
}

/// <summary>Jellyfin server sender.</summary>
public sealed class JellyfinSender(IHttpClientFactory httpClientFactory) : EmbyJellyfinSenderBase(httpClientFactory)
{
	/// <inheritdoc />
	public override NotificationType Type => NotificationType.JELLYFIN;

	/// <inheritdoc />
	protected override void ApplyAuth(HttpRequestHeaders headers, string apiKey)
		=> headers.Add("Authorization", $"MediaBrowser Token=\"{apiKey}\"");

	/// <inheritdoc />
	protected override EmbySettings ParseSettings(string settingsJson)
	{
		var settings = (JellyfinSettings)NotificationSettingsJson.Parse(NotificationType.JELLYFIN, settingsJson);
		return new EmbySettings
		{
			Host = settings.Host,
			Port = settings.Port,
			UseSsl = settings.UseSsl,
			ApiKey = settings.ApiKey,
			Notify = settings.Notify,
			UpdateLibrary = settings.UpdateLibrary,
			MapFrom = settings.MapFrom,
			MapTo = settings.MapTo
		};
	}
}

/// <summary>Plex sender refreshing the matching library section.</summary>
public sealed class PlexSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	private static readonly JsonSerializerOptions ResponseOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	/// <inheritdoc />
	public NotificationType Type => NotificationType.PLEX;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = NotificationSettingsJson.Parse(Type, settingsJson) as PlexSettings
			?? throw new InvalidOperationException("Invalid Plex settings");
		if (!settings.UpdateLibrary || message.Path is null)
		{
			return;
		}

		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		var baseUri = BaseUrl(settings);
		var path = MapPath(settings, message.Path);

		using var sectionsRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUri}/library/sections");
		ApplyAuth(sectionsRequest.Headers, settings.AuthToken);
		sectionsRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		using var sectionsResponse = await client.SendAsync(sectionsRequest, cancellationToken);
		sectionsResponse.EnsureSuccessStatusCode();

		var sections = await sectionsResponse.Content.ReadFromJsonAsync<PlexSectionsResponse>(ResponseOptions, cancellationToken);
		var sectionKeys = sections?.MediaContainer?.Directory
			?.Where(section => section.Location?.Any(location => location.Path is { } sectionPath && IsUnder(sectionPath, path)) == true)
			.Select(section => section.Key)
			.Distinct()
			.ToList();
		if (sectionKeys is null || sectionKeys.Count == 0)
		{
			return;
		}

		foreach (var key in sectionKeys)
		{
			var refreshUrl = $"{baseUri}/library/sections/{key}/refresh?path={Uri.EscapeDataString(path)}";
			using var refreshRequest = new HttpRequestMessage(HttpMethod.Get, refreshUrl);
			ApplyAuth(refreshRequest.Headers, settings.AuthToken);
			using var refreshResponse = await client.SendAsync(refreshRequest, cancellationToken);
			refreshResponse.EnsureSuccessStatusCode();
		}
	}

	/// <inheritdoc />
	public async Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (PlexSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl(settings)}/identity");
		ApplyAuth(request.Headers, settings.AuthToken);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	internal static bool IsUnder(string sectionPath, string path)
		=> path.StartsWith(sectionPath.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(path.TrimEnd('/'), sectionPath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

	internal static string MapPath(PlexSettings settings, string path)
	{
		if (string.IsNullOrEmpty(settings.MapFrom) || string.IsNullOrEmpty(settings.MapTo))
		{
			return path;
		}

		return path.StartsWith(settings.MapFrom, StringComparison.Ordinal)
			? settings.MapTo.TrimEnd('/') + path[settings.MapFrom.Length..]
			: path;
	}

	private static void ApplyAuth(HttpRequestHeaders headers, string token)
		=> headers.Add("X-Plex-Token", token);

	private static string BaseUrl(PlexSettings settings)
		=> $"{(settings.UseSsl ? "https" : "http")}://{settings.Host}:{settings.Port}";

	internal sealed class PlexSectionsResponse
	{
		[JsonPropertyName("MediaContainer")]
		public PlexContainer? MediaContainer { get; set; }
	}

	internal sealed class PlexContainer
	{
		public List<PlexSection>? Directory { get; set; }
	}

	internal sealed class PlexSection
	{
		public string? Key { get; set; }

		public List<PlexLocation>? Location { get; set; }
	}

	internal sealed class PlexLocation
	{
		public string? Path { get; set; }
	}
}
