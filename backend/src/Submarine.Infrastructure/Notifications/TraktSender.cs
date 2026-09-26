using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Submarine.Core.Enums;
using Submarine.Core.Notifications;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Notifications;

/// <summary>
///     Adds imported episodes/movies to the user's Trakt collection and removes them again on delete.
///     Access tokens are refreshed proactively by <see cref="ITraktTokenRefresher" /> before the dispatcher
///     calls this sender, so <see cref="SendAsync" /> assumes the token in the settings json passed in is
///     already valid.
/// </summary>
public sealed class TraktSender(SubmarineDbContext db, IHttpClientFactory httpClientFactory, IConfiguration configuration) : INotificationSender
{
	private const string BaseUrl = "https://api.trakt.tv";

	/// <inheritdoc />
	public NotificationType Type => NotificationType.TRAKT;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (TraktSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var payload = await BuildPayloadAsync(message, cancellationToken);
		if (payload is null)
		{
			return;
		}

		var resource = message.EventType == NotificationEventType.DELETE ? "sync/collection/remove" : "sync/collection";
		var clientId = TraktCredentials.RequireClientId(settings.ClientId, configuration);

		using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{resource}")
		{
			Content = JsonContent.Create(payload)
		};
		ApplyAuth(request.Headers, settings.AccessToken, clientId);

		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public async Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (TraktSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var clientId = TraktCredentials.RequireClientId(settings.ClientId, configuration);

		using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/users/settings");
		ApplyAuth(request.Headers, settings.AccessToken, clientId);

		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	private static void ApplyAuth(HttpRequestHeaders headers, string accessToken, string clientId)
	{
		headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		headers.Add("trakt-api-version", "2");
		headers.Add("trakt-api-key", clientId);
	}

	private async Task<object?> BuildPayloadAsync(NotificationMessage message, CancellationToken cancellationToken)
	{
		if (message.SeriesId is { } seriesId)
		{
			var series = await db.Series.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);
			if (series is null || message.Episodes.Count == 0)
			{
				return null;
			}

			var seasons = message.Episodes
				.GroupBy(e => e.Season)
				.Select(g => new { number = g.Key, episodes = g.Select(e => new { number = e.Number }).ToArray() })
				.ToArray();
			return new { shows = new[] { new { ids = Ids(series.TvdbId, series.TmdbId, series.ImdbId), seasons } } };
		}

		if (message.MovieId is { } movieId)
		{
			var movie = await db.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Id == movieId, cancellationToken);
			if (movie is null)
			{
				return null;
			}

			return new
			{
				movies = new[]
				{
					new
					{
						ids = Ids(null, movie.TmdbId, movie.ImdbId),
						title = movie.Title,
						year = movie.Year,
						resolution = MapResolution(message.Quality),
						media_type = MapMediaType(message.Quality)
					}
				}
			};
		}

		return null;
	}

	private static Dictionary<string, object> Ids(int? tvdb, int? tmdb, string? imdb)
	{
		var ids = new Dictionary<string, object>();
		if (tvdb is { } t)
		{
			ids["tvdb"] = t;
		}

		if (tmdb is { } m)
		{
			ids["tmdb"] = m;
		}

		if (!string.IsNullOrEmpty(imdb))
		{
			ids["imdb"] = imdb;
		}

		return ids;
	}

	/// <summary>
	///     Maps to Trakt's resolution enum. Submarine's <see cref="NotificationMessage" /> only carries the
	///     parsed quality, not raw media info (scan type, HDR), so interlaced/HDR variants are not distinguished.
	/// </summary>
	private static string? MapResolution(QualityModel? quality)
		=> quality?.Resolution.Resolution switch
		{
			QualityResolution.R2160_P => "uhd_4k",
			QualityResolution.R1080_P => "hd_1080p",
			QualityResolution.R720_P => "hd_720p",
			QualityResolution.R576_P => "sd_576p",
			QualityResolution.R480_P => "sd_480p",
			_ => null
		};

	private static string? MapMediaType(QualityModel? quality)
		=> quality?.Resolution.Source switch
		{
			QualitySource.BLURAY or QualitySource.BLURAY_REMUX or QualitySource.BLURAY_DISK => "bluray",
			QualitySource.DVD => "dvd",
			QualitySource.CAM => "cam",
			QualitySource.WEB_DL or QualitySource.WEB_RIP or QualitySource.TV or QualitySource.RAW_HD => "digital",
			_ => null
		};
}
