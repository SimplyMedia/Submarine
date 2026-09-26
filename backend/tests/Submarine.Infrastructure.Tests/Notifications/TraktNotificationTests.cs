using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Core.Notifications;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Notifications;
using Submarine.Infrastructure.Tests.DownloadClients;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Notifications;

/// <summary>
///     Asserts the Trakt OAuth device code flow, token refresh persistence and collection sync payloads.
/// </summary>
public sealed class TraktNotificationTests
{
	private static (IHttpClientFactory Factory, StubHttpHandler Stub) CreateFactory(
		Func<HttpRequestMessage, string?, HttpResponseMessage>? responder = null)
	{
		var stub = new StubHttpHandler(responder ?? ((_, _) => StubHttpHandler.Json("{}")));
		var services = new ServiceCollection();
		services.AddHttpClient(NotificationSenderFactory.HttpClientName)
			.ConfigurePrimaryHttpMessageHandler(() => stub);
		var provider = services.BuildServiceProvider();
		return (provider.GetRequiredService<IHttpClientFactory>(), stub);
	}

	private static IConfiguration EmptyConfiguration()
		=> new ConfigurationBuilder().Build();

	[Fact]
	public async Task StartDeviceFlow_ShouldPostClientId_AndParseResponse()
	{
		var (factory, stub) = CreateFactory((_, _) => StubHttpHandler.Json(
			"""{"device_code":"dc","user_code":"UC-1","verification_url":"https://trakt.tv/activate","expires_in":600,"interval":5}"""));
		var service = new TraktAuthService(factory);

		var result = await service.StartDeviceFlowAsync("client-1", TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Url.ShouldBe("https://api.trakt.tv/oauth/device/code");
		request.Body!.ShouldContain("\"client_id\":\"client-1\"");
		result.DeviceCode.ShouldBe("dc");
		result.UserCode.ShouldBe("UC-1");
		result.VerificationUrl.ShouldBe("https://trakt.tv/activate");
		result.ExpiresIn.ShouldBe(600);
		result.Interval.ShouldBe(5);
	}

	[Theory]
	[InlineData(HttpStatusCode.BadRequest, TraktPollStatus.PENDING)]
	[InlineData(HttpStatusCode.Gone, TraktPollStatus.EXPIRED)]
	[InlineData((HttpStatusCode)418, TraktPollStatus.DENIED)]
	[InlineData(HttpStatusCode.Conflict, TraktPollStatus.DENIED)]
	public async Task PollDeviceFlow_ShouldMapStatusCode(HttpStatusCode statusCode, TraktPollStatus expected)
	{
		var (factory, _) = CreateFactory((_, _) => new HttpResponseMessage(statusCode));
		var service = new TraktAuthService(factory);

		var result = await service.PollDeviceFlowAsync("id", "secret", "dc", TestContext.Current.CancellationToken);

		result.Status.ShouldBe(expected);
		result.Tokens.ShouldBeNull();
	}

	[Fact]
	public async Task PollDeviceFlow_ShouldReturnTokens_WhenAuthorized()
	{
		var (factory, stub) = CreateFactory((_, _) => StubHttpHandler.Json(
			"""{"access_token":"at","refresh_token":"rt","expires_in":7200}"""));
		var service = new TraktAuthService(factory);

		var result = await service.PollDeviceFlowAsync("id", "secret", "dc", TestContext.Current.CancellationToken);

		stub.Requests.Single().Body!.ShouldContain("\"code\":\"dc\"");
		result.Status.ShouldBe(TraktPollStatus.AUTHORIZED);
		result.Tokens!.AccessToken.ShouldBe("at");
		result.Tokens.RefreshToken.ShouldBe("rt");
		result.Tokens.ExpiresIn.ShouldBe(7200);
	}

	[Fact]
	public async Task Refresh_ShouldPostRefreshTokenGrant_AndParseNewTokens()
	{
		var (factory, stub) = CreateFactory((_, _) => StubHttpHandler.Json(
			"""{"access_token":"new-at","refresh_token":"new-rt","expires_in":7200}"""));
		var service = new TraktAuthService(factory);

		var result = await service.RefreshAsync("id", "secret", "old-rt", TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Url.ShouldBe("https://api.trakt.tv/oauth/token");
		request.Body!.ShouldContain("\"refresh_token\":\"old-rt\"");
		request.Body!.ShouldContain("\"grant_type\":\"refresh_token\"");
		result.AccessToken.ShouldBe("new-at");
		result.RefreshToken.ShouldBe("new-rt");
	}

	[Fact]
	public async Task TokenRefresher_ShouldReturnUnchanged_ForNonTraktNotification()
	{
		using var db = TestDbFactory.Create(TimeProvider.System);
		var notification = new Notification { Type = NotificationType.WEBHOOK, SettingsJson = "{}" };
		db.Notifications.Add(notification);
		db.SaveChanges();
		var refresher = new TraktTokenRefresher(db, Substitute.For<ITraktAuthService>(), EmptyConfiguration(), TimeProvider.System);

		var json = await refresher.EnsureFreshTokensAsync(notification, TestContext.Current.CancellationToken);

		json.ShouldBe("{}");
	}

	[Fact]
	public async Task TokenRefresher_ShouldReturnUnchanged_WhenTokenStillValidForMoreThan5Minutes()
	{
		var clock = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
		using var db = TestDbFactory.Create(clock);
		var settings = new TraktSettings { AccessToken = "at", RefreshToken = "rt", ExpiresAt = clock.GetUtcNow().UtcDateTime.AddHours(1) };
		var notification = new Notification { Type = NotificationType.TRAKT, SettingsJson = NotificationSettingsJson.Serialize(settings) };
		db.Notifications.Add(notification);
		db.SaveChanges();
		var authService = Substitute.For<ITraktAuthService>();
		var refresher = new TraktTokenRefresher(db, authService, EmptyConfiguration(), clock);

		await refresher.EnsureFreshTokensAsync(notification, TestContext.Current.CancellationToken);

		await authService.DidNotReceive().RefreshAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task TokenRefresher_ShouldRefreshAndPersist_WhenNearExpiry()
	{
		var clock = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
		using var db = TestDbFactory.Create(clock);
		var settings = new TraktSettings { ClientId = "cid", ClientSecret = "csecret", AccessToken = "old-at", RefreshToken = "old-rt", ExpiresAt = clock.GetUtcNow().UtcDateTime.AddMinutes(2) };
		var notification = new Notification { Type = NotificationType.TRAKT, SettingsJson = NotificationSettingsJson.Serialize(settings) };
		db.Notifications.Add(notification);
		db.SaveChanges();
		var authService = Substitute.For<ITraktAuthService>();
		authService.RefreshAsync("cid", "csecret", "old-rt", Arg.Any<CancellationToken>())
			.Returns(new TraktTokens("new-at", "new-rt", 7200));
		var refresher = new TraktTokenRefresher(db, authService, EmptyConfiguration(), clock);

		var json = await refresher.EnsureFreshTokensAsync(notification, TestContext.Current.CancellationToken);

		json.ShouldContain("\"accessToken\":\"new-at\"");
		json.ShouldContain("\"refreshToken\":\"new-rt\"");
		var persisted = db.Notifications.Single(n => n.Id == notification.Id).SettingsJson;
		persisted.ShouldContain("new-at");
	}

	private static NotificationMessage EpisodeMessage(NotificationEventType eventType, int seriesId)
		=> new(eventType, "Imported", "body", seriesId, null, "Some Show", 2024,
			new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()),
			[Language.ENGLISH], "GRP", "idx", "qb", "dl-1", 1000, "/media/x.mkv", null,
			[new NotificationEpisode(1, 1, "Pilot", "2024-01-01"), new NotificationEpisode(1, 2, "Two", "2024-01-08")], []);

	private static NotificationMessage MovieMessage(NotificationEventType eventType, int movieId)
		=> new(eventType, "Imported", "body", null, movieId, "Some Movie", 2024,
			new QualityModel(new QualityResolutionModel(QualitySource.BLURAY, QualityResolution.R1080_P), new Revision()),
			[Language.ENGLISH], "GRP", "idx", "qb", "dl-1", 1000, "/media/x.mkv", null, [], []);

	[Fact]
	public async Task Sender_ShouldAddEpisodesToCollection_GroupedBySeason()
	{
		using var db = TestDbFactory.Create(TimeProvider.System);
		var series = new Series { TvdbId = 42, TmdbId = 99, Title = "Some Show", Year = 2024 };
		db.Series.Add(series);
		db.SaveChanges();
		var (factory, stub) = CreateFactory();
		var sender = new TraktSender(db, factory, EmptyConfiguration());
		var settings = """{"clientId":"cid","accessToken":"at","refreshToken":"rt","expiresAt":"2030-01-01T00:00:00"}""";

		await sender.SendAsync(EpisodeMessage(NotificationEventType.IMPORT, series.Id), settings, TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Url.ShouldBe("https://api.trakt.tv/sync/collection");
		request.HasHeader("Authorization", "Bearer at").ShouldBeTrue();
		request.HasHeader("trakt-api-key", "cid").ShouldBeTrue();
		request.Body!.ShouldContain("\"tvdb\":42");
		request.Body!.ShouldContain("\"number\":1");
		request.Body!.ShouldContain("\"number\":2");
	}

	[Fact]
	public async Task Sender_ShouldRemoveFromCollection_OnDelete()
	{
		using var db = TestDbFactory.Create(TimeProvider.System);
		var series = new Series { TvdbId = 42, Title = "Some Show", Year = 2024 };
		db.Series.Add(series);
		db.SaveChanges();
		var (factory, stub) = CreateFactory();
		var sender = new TraktSender(db, factory, EmptyConfiguration());
		var settings = """{"clientId":"cid","accessToken":"at","refreshToken":"rt","expiresAt":"2030-01-01T00:00:00"}""";

		await sender.SendAsync(
			EpisodeMessage(NotificationEventType.DELETE, series.Id) with { Episodes = [new NotificationEpisode(1, 1, "Pilot", null)] },
			settings, TestContext.Current.CancellationToken);

		stub.Requests.Single().Url.ShouldBe("https://api.trakt.tv/sync/collection/remove");
	}

	[Fact]
	public async Task Sender_ShouldAddMovie_WithResolutionAndMediaType()
	{
		using var db = TestDbFactory.Create(TimeProvider.System);
		var movie = new Movie { TmdbId = 55, Title = "Some Movie", Year = 2024 };
		db.Movies.Add(movie);
		db.SaveChanges();
		var (factory, stub) = CreateFactory();
		var sender = new TraktSender(db, factory, EmptyConfiguration());
		var settings = """{"clientId":"cid","accessToken":"at","refreshToken":"rt","expiresAt":"2030-01-01T00:00:00"}""";

		await sender.SendAsync(MovieMessage(NotificationEventType.IMPORT, movie.Id), settings, TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Body!.ShouldContain("\"tmdb\":55");
		request.Body!.ShouldContain("\"resolution\":\"hd_1080p\"");
		request.Body!.ShouldContain("\"media_type\":\"bluray\"");
	}

	[Fact]
	public async Task Sender_Test_ShouldCallUsersSettings()
	{
		var (factory, stub) = CreateFactory();
		using var db = TestDbFactory.Create(TimeProvider.System);
		var sender = new TraktSender(db, factory, EmptyConfiguration());

		await sender.TestAsync("""{"clientId":"cid","accessToken":"at","refreshToken":"rt","expiresAt":"2030-01-01T00:00:00"}""", TestContext.Current.CancellationToken);

		stub.Requests.Single().Url.ShouldBe("https://api.trakt.tv/users/settings");
	}
}
