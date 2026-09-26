using System.Net.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Notifications;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Notifications;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Infrastructure.Tests.Notifications;

/// <summary>
///     Asserts event filtering, message building and dispatch of the notification dispatcher.
/// </summary>
public sealed class NotificationDispatcherTests : IAsyncLifetime
{
	private SqliteConnection _connection = null!;
	private ServiceProvider _provider = null!;
	private readonly FakeSenderFactory _senders = new();

	public async ValueTask InitializeAsync()
	{
		_connection = new SqliteConnection("DataSource=:memory:");
		_connection.Open();

		var services = new ServiceCollection();
		services.TryAddSingleton(TimeProvider.System);
		services.AddDbContext<SqliteSubmarineDbContext>(options => options.UseSqlite(_connection));
		services.AddScoped<SubmarineDbContext>(sp => sp.GetRequiredService<SqliteSubmarineDbContext>());

		_provider = services.BuildServiceProvider();
		var db = _provider.GetRequiredService<SubmarineDbContext>();
		await db.Database.EnsureCreatedAsync();
		db.GeneralConfig.Add(new GeneralConfig());
		await db.SaveChangesAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _provider.DisposeAsync();
		await _connection.DisposeAsync();
	}

	private SubmarineDbContext Db => _provider.GetRequiredService<SubmarineDbContext>();

	private NotificationDispatcher Dispatcher => new(Db, _senders, new NotificationStatusService(Db, TimeProvider.System, Substitute.For<IEventBus>()), new PassthroughTraktTokenRefresher(), NullLogger<NotificationDispatcher>.Instance);

	private async Task<Notification> SeedNotificationAsync(Action<Notification> configure)
	{
		var notification = new Notification { Name = "Test", Type = NotificationType.WEBHOOK, SettingsJson = "{}" };
		configure(notification);
		Db.Notifications.Add(notification);
		await Db.SaveChangesAsync();
		return notification;
	}

	private async Task<Series> SeedSeriesAsync(params string[] tagLabels)
	{
		var tags = tagLabels.Select(label => new Tag { Label = label }).ToList();
		var series = new Series
		{
			TvdbId = Random.Shared.Next(),
			Title = "Some Show",
			Year = 2024,
			PosterUrl = "https://image/poster.jpg",
			Tags = tags
		};
		series.Episodes.Add(new Episode
		{
			SeasonNumber = 1,
			EpisodeNumber = 1,
			Title = "Pilot",
			AirDate = "2024-01-01",
			Monitored = true
		});
		Db.Series.Add(series);
		await Db.SaveChangesAsync();
		return series;
	}

	private static ReleaseGrabbedEvent GrabEvent(int seriesId, IReadOnlyList<int> episodeIds)
		=> new(new GrabbedRelease(
			"Some.Show.S01E01", "idx", 1, 1, "qBittorrent", "hash",
			Protocol.BITTORRENT, 1000, new QualityModel(new QualityResolutionModel(), new Revision()),
			[Language.ENGLISH], "GRP", seriesId, episodeIds, null, 1, "guid", null, null));

	private static EpisodeFileImportedEvent ImportEvent(int seriesId, int episodeId, bool isUpgrade)
		=> new(seriesId, 1, 1, [episodeId], "/media/x.mkv", "src", null, isUpgrade,
			new QualityModel(new QualityResolutionModel(), new Revision()), [Language.ENGLISH], "GRP");

	[Fact]
	public async Task Grab_ShouldBuildMessageFromMedia()
	{
		await SeedSeriesAsync();
		var config = await Db.GeneralConfig.SingleAsync(TestContext.Current.CancellationToken);
		config.InstanceName = "My Submarine";
		config.ApplicationUrl = "https://submarine.example/base";
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
		await SeedNotificationAsync(n =>
		{
			n.OnGrab = true;
			n.OnImport = false;
		});
		var sender = new RecordingSender(NotificationType.WEBHOOK);
		_senders.Register(sender);

		await Dispatcher.HandleAsync(GrabEvent(1, [1]), TestContext.Current.CancellationToken);

		var message = sender.Messages.Single();
		message.EventType.ShouldBe(NotificationEventType.GRAB);
		message.Title.ShouldBe("My Submarine - Grabbed");
		message.Links.ShouldContain(link =>
			link.Label == "Open My Submarine"
			&& link.Url == "https://submarine.example/base/series/1");
		message.MediaTitle.ShouldBe("Some Show");
		message.Year.ShouldBe(2024);
		message.ImageUrl.ShouldBe("https://image/poster.jpg");
		message.Episodes.Single().Season.ShouldBe(1);
		message.Episodes.Single().Number.ShouldBe(1);
		message.Episodes.Single().Title.ShouldBe("Pilot");
		message.Indexer.ShouldBe("idx");
		message.DownloadClient.ShouldBe("qBittorrent");
		message.Quality.ShouldNotBeNull();
	}

	[Fact]
	public async Task Import_ShouldHonorUpgradeFlag()
	{
		await SeedSeriesAsync();
		await SeedNotificationAsync(n =>
		{
			n.OnImport = true;
			n.OnUpgrade = false;
		});
		var sender = new RecordingSender(NotificationType.WEBHOOK);
		_senders.Register(sender);

		await Dispatcher.HandleAsync(ImportEvent(1, 1, isUpgrade: false), TestContext.Current.CancellationToken);
		sender.Messages.Single().EventType.ShouldBe(NotificationEventType.IMPORT);

		sender.Messages.Clear();
		await Dispatcher.HandleAsync(ImportEvent(1, 1, isUpgrade: true), TestContext.Current.CancellationToken);
		sender.Messages.ShouldBeEmpty();
	}

	[Fact]
	public async Task Tags_ShouldRestrictNotification()
	{
		var tagged = await SeedSeriesAsync("anime");
		var untagged = await SeedSeriesAsync();
		await SeedNotificationAsync(async n =>
		{
			n.OnImport = true;
			n.Tags = [Db.Tags.First(t => t.Label == "anime")];
		});
		var sender = new RecordingSender(NotificationType.WEBHOOK);
		_senders.Register(sender);

		await Dispatcher.HandleAsync(ImportEvent(tagged.Id, tagged.Episodes.Single().Id, false), TestContext.Current.CancellationToken);
		sender.Messages.Count.ShouldBe(1);

		await Dispatcher.HandleAsync(ImportEvent(untagged.Id, untagged.Episodes.Single().Id, false), TestContext.Current.CancellationToken);
		sender.Messages.Count.ShouldBe(1);
	}

	[Fact]
	public async Task MediaServers_ShouldSkipGrab_AndReceiveImports()
	{
		await SeedSeriesAsync();
		await SeedNotificationAsync(n =>
		{
			n.OnGrab = true;
			n.OnImport = true;
			n.Type = NotificationType.PLEX;
		});
		var sender = new RecordingSender(NotificationType.PLEX);
		_senders.Register(sender);

		await Dispatcher.HandleAsync(GrabEvent(1, [1]), TestContext.Current.CancellationToken);
		sender.Messages.ShouldBeEmpty();

		await Dispatcher.HandleAsync(ImportEvent(1, 1, false), TestContext.Current.CancellationToken);
		var message = sender.Messages.Single();
		message.EventType.ShouldBe(NotificationEventType.IMPORT);
		message.Path.ShouldBe("/media/x.mkv");
	}

	[Fact]
	public async Task Emby_ShouldReceiveGrab_ButNotManualInteraction()
	{
		await SeedSeriesAsync();
		await SeedNotificationAsync(n =>
		{
			n.OnGrab = true;
			n.OnManualInteractionRequired = true;
			n.Type = NotificationType.EMBY;
		});
		var sender = new RecordingSender(NotificationType.EMBY);
		_senders.Register(sender);

		await Dispatcher.HandleAsync(GrabEvent(1, [1]), TestContext.Current.CancellationToken);
		sender.Messages.Single().EventType.ShouldBe(NotificationEventType.GRAB);

		sender.Messages.Clear();
		await Dispatcher.HandleAsync(
			new ManualInteractionRequiredEvent("dl-1", "Some.Show.S01E01", 1, null, "stalled"),
			TestContext.Current.CancellationToken);
		sender.Messages.ShouldBeEmpty();
	}

	[Fact]
	public async Task RepeatedFailures_ShouldDisableNotification_AndSkipFurtherSends()
	{
		await SeedSeriesAsync();
		var notification = await SeedNotificationAsync(n => n.OnGrab = true);
		var sender = new ThrowingSender(NotificationType.WEBHOOK);
		_senders.Register(sender);
		var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
		var statusService = new NotificationStatusService(Db, clock, Substitute.For<IEventBus>());
		var dispatcher = new NotificationDispatcher(Db, _senders, statusService, new PassthroughTraktTokenRefresher(), NullLogger<NotificationDispatcher>.Instance);

		await dispatcher.HandleAsync(GrabEvent(1, [1]), TestContext.Current.CancellationToken);
		(await statusService.IsAvailableAsync(notification.Id, TestContext.Current.CancellationToken)).ShouldBeTrue();

		clock.Advance(TimeSpan.FromMinutes(5));
		await dispatcher.HandleAsync(GrabEvent(1, [1]), TestContext.Current.CancellationToken);
		(await statusService.IsAvailableAsync(notification.Id, TestContext.Current.CancellationToken)).ShouldBeFalse();

		sender.CallCount = 0;
		await dispatcher.HandleAsync(GrabEvent(1, [1]), TestContext.Current.CancellationToken);
		sender.CallCount.ShouldBe(0);
	}

	[Fact]
	public async Task Health_ShouldRespectSeverityAndFlags()
	{
		await SeedNotificationAsync(n =>
		{
			n.OnHealthIssue = true;
			n.IncludeHealthWarnings = false;
		});
		await SeedNotificationAsync(n =>
		{
			n.OnHealthRestored = true;
			n.Type = NotificationType.TELEGRAM;
		});
		var web = new RecordingSender(NotificationType.WEBHOOK);
		var telegram = new RecordingSender(NotificationType.TELEGRAM);
		_senders.Register(web);
		_senders.Register(telegram);

		var @event = new HealthIssuesChangedEvent(
			[],
			[new(HealthIssueType.WARNING, "Indexers", "No indexers", null),
				new(HealthIssueType.ERROR, "Root folders", "Missing", null)],
			[new(HealthIssueType.ERROR, "Old", "Fixed", null)]);

		await Dispatcher.HandleAsync(@event, TestContext.Current.CancellationToken);

		web.Messages.Count.ShouldBe(1);
		web.Messages.Single().EventType.ShouldBe(NotificationEventType.HEALTH);
		web.Messages.Single().Body.ShouldContain("Root folders: Missing");

		telegram.Messages.Single().EventType.ShouldBe(NotificationEventType.HEALTH_RESTORED);
	}

	[Fact]
	public async Task DisabledNotification_ShouldBeSkipped()
	{
		await SeedSeriesAsync();
		await SeedNotificationAsync(n =>
		{
			n.OnGrab = true;
			n.Enable = false;
		});
		var sender = new RecordingSender(NotificationType.WEBHOOK);
		_senders.Register(sender);

		await Dispatcher.HandleAsync(GrabEvent(1, [1]), TestContext.Current.CancellationToken);

		sender.Messages.ShouldBeEmpty();
	}

	[Fact]
	public async Task SenderFailure_ShouldNotThrow()
	{
		await SeedSeriesAsync();
		await SeedNotificationAsync(n => n.OnGrab = true);
		_senders.Register(new ThrowingSender(NotificationType.WEBHOOK));

		await Dispatcher.HandleAsync(GrabEvent(1, [1]), TestContext.Current.CancellationToken);
	}

	[Fact]
	public async Task DeleteEvents_ShouldNotifyOnDelete()
	{
		var series = await SeedSeriesAsync();
		await SeedNotificationAsync(n => n.OnDelete = true);
		var sender = new RecordingSender(NotificationType.WEBHOOK);
		_senders.Register(sender);

		await Dispatcher.HandleAsync(new SeriesDeletedEvent(series.Id, "Some Show", false), TestContext.Current.CancellationToken);
		await Dispatcher.HandleAsync(
			new EpisodeFileDeletedEvent(series.Id, 1, [series.Episodes.Single().Id], "/media/x.mkv", FileDeleteReason.MISSING_FROM_DISK),
			TestContext.Current.CancellationToken);

		sender.Messages.Count.ShouldBe(2);
		sender.Messages.ShouldAllBe(m => m.EventType == NotificationEventType.DELETE);
		sender.Messages[1].Path.ShouldBe("/media/x.mkv");
	}

	[Fact]
	public async Task UpdateEvent_ShouldNotifyOnApplicationUpdate()
	{
		await SeedNotificationAsync(n =>
		{
			n.OnApplicationUpdate = true;
			n.Type = NotificationType.TELEGRAM;
		});
		var telegram = new RecordingSender(NotificationType.TELEGRAM);
		_senders.Register(telegram);

		await Dispatcher.HandleAsync(
			new ApplicationUpdateAvailableEvent("1.0.0", "2.0.0", "https://example/release"),
			TestContext.Current.CancellationToken);

		var message = telegram.Messages.Single();
		message.EventType.ShouldBe(NotificationEventType.APPLICATION_UPDATE);
		message.Body.ShouldContain("2.0.0");
		message.Links.Single().Url.ShouldBe("https://example/release");
	}

	private sealed class FakeSenderFactory : INotificationSenderFactory
	{
		private readonly Dictionary<NotificationType, INotificationSender> _senders = [];

		public void Register(INotificationSender sender)
			=> _senders[sender.Type] = sender;

		public INotificationSender Resolve(NotificationType type)
			=> _senders.TryGetValue(type, out var sender)
				? sender
				: throw new InvalidOperationException($"no sender for {type}");
	}

	private sealed class RecordingSender(NotificationType type) : INotificationSender
	{
		public List<NotificationMessage> Messages { get; } = [];

		public NotificationType Type => type;

		public Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
		{
			Messages.Add(message);
			return Task.CompletedTask;
		}

		public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;
	}

	private sealed class ThrowingSender(NotificationType type) : INotificationSender
	{
		public int CallCount { get; set; }

		public NotificationType Type => type;

		public Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
		{
			CallCount++;
			throw new HttpRequestException("boom");
		}

		public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;
	}

	private sealed class PassthroughTraktTokenRefresher : ITraktTokenRefresher
	{
		public Task<string> EnsureFreshTokensAsync(Notification notification, CancellationToken cancellationToken = default)
			=> Task.FromResult(notification.SettingsJson);
	}
}
