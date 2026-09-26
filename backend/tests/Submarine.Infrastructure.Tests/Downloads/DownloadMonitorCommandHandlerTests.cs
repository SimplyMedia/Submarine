using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Submarine.Core.Commands;
using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Parser;
using Submarine.Core.Provider;
using Submarine.Core.Release;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Downloads;
using Submarine.Infrastructure.Import;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Downloads;

public sealed class DownloadMonitorCommandHandlerTests : IDisposable
{
	private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
	private readonly SqliteSubmarineDbContext _db;
	private readonly IDownloadClientProvider _clientProvider = Substitute.For<IDownloadClientProvider>();
	private readonly IDownloadClientStatusTracker _statusTracker = Substitute.For<IDownloadClientStatusTracker>();
	private readonly IImportService _importService = Substitute.For<IImportService>();
	private readonly IEventBus _eventBus = Substitute.For<IEventBus>();
	private readonly ICommandQueue _commandQueue = Substitute.For<ICommandQueue>();
	private readonly IParser<BaseRelease> _releaseParser = TestReleaseParserFactory.Create();
	private readonly IDownloadClient _client = Substitute.For<IDownloadClient>();
	private readonly DownloadClient _clientEntity;
	private readonly ICommandContext _context = Substitute.For<ICommandContext>();

	public DownloadMonitorCommandHandlerTests()
	{
		_db = TestDbFactory.Create(_time);
		_db.DownloadConfig.Add(new DownloadConfig());
		_clientEntity = new DownloadClient { Name = "Client", Type = DownloadClientType.QBITTORRENT, SettingsJson = "{}" };
		_db.DownloadClients.Add(_clientEntity);
		_db.SaveChanges();

		_statusTracker.IsAvailable(Arg.Any<int>()).Returns(true);
		_clientProvider.GetEnabledAsync(Arg.Any<Protocol?>(), Arg.Any<CancellationToken>())
			.Returns([new EnabledDownloadClient(_clientEntity, _client)]);
	}

	public void Dispose() => _db.Dispose();

	private DownloadMonitorCommandHandler Handler
		=> new(_db, _clientProvider, _statusTracker, _importService, _releaseParser, _eventBus, _commandQueue, _time, NullLogger<DownloadMonitorCommandHandler>.Instance);

	private DownloadConfig Config() => _db.DownloadConfig.Single();

	private TrackedDownload SeedDownload(TrackedDownloadState state, string downloadId, int? seriesId = null, IReadOnlyList<int>? episodeIds = null, int? movieId = null)
	{
		List<int>? realEpisodeIds = null;

		if (seriesId is { } sid && !_db.Series.Any(x => x.Id == sid))
		{
			var series = new Series { Title = "Test Show", CleanTitle = "testshow" };
			_db.Series.Add(series);
			_db.SaveChanges();
			seriesId = series.Id;

			if (episodeIds is { Count: > 0 } wanted)
			{
				realEpisodeIds = [];
				for (var i = 0; i < wanted.Count; i++)
				{
					var episode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = i + 1 };
					_db.Episodes.Add(episode);
					_db.SaveChanges();
					realEpisodeIds.Add(episode.Id);
				}
			}
		}

		if (movieId is { } mid && !_db.Movies.Any(x => x.Id == mid))
		{
			_db.Movies.Add(new Movie { Title = "Test Movie", CleanTitle = "testmovie" });
			_db.SaveChanges();
			movieId = _db.Movies.Local.First().Id;
		}

		var download = new TrackedDownload
		{
			DownloadClientId = _clientEntity.Id,
			DownloadId = downloadId,
			Title = "Test.Show.S01E01",
			Protocol = Protocol.BITTORRENT,
			Status = TrackedDownloadStatus.DOWNLOADING,
			State = state,
			SeriesId = seriesId,
			MovieId = movieId,
			EpisodeIds = realEpisodeIds ?? (episodeIds is null ? [] : [.. episodeIds]),
			Added = _time.GetUtcNow().UtcDateTime
		};
		_db.TrackedDownloads.Add(download);
		_db.SaveChanges();
		return download;
	}

	private static DownloadClientItem Item(string downloadId, DownloadItemStatus status, bool canBeRemoved = true, bool canMoveFiles = true, string? message = null, Protocol protocol = Protocol.BITTORRENT)
		=> new()
		{
			DownloadId = downloadId,
			Title = "Test.Show.S01E01",
			Status = status,
			CanBeRemoved = canBeRemoved,
			CanMoveFiles = canMoveFiles,
			Protocol = protocol,
			Message = message
		};

	[Fact]
	public async Task ExecuteAsync_ShouldRunImport_WhenDownloadCompletes()
	{
		var download = SeedDownload(TrackedDownloadState.IMPORT_PENDING, "abc", seriesId: 1, episodeIds: [1]);
		Config().EnableCompletedDownloadHandling = true;
		_db.SaveChanges();
		_client.GetItemsAsync(Arg.Any<CancellationToken>()).Returns([Item("abc", DownloadItemStatus.COMPLETED)]);
		_importService.ImportTrackedDownloadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new ImportRunSummary([]));

		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);

		await _importService.Received(1).ImportTrackedDownloadAsync(download.Id, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ExecuteAsync_ShouldRemoveFromClient_WhenImportedAndSeedingSatisfied()
	{
		var download = SeedDownload(TrackedDownloadState.IMPORT_PENDING, "abc", seriesId: 1, episodeIds: [1]);
		Config().EnableCompletedDownloadHandling = true;
		Config().RemoveCompletedDownloads = true;
		_db.SaveChanges();
		_client.GetItemsAsync(Arg.Any<CancellationToken>()).Returns([Item("abc", DownloadItemStatus.COMPLETED, canBeRemoved: true, canMoveFiles: true)]);
		SimulateSuccessfulImport();

		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);

		await _client.Received(1).RemoveAsync("abc", false, Arg.Any<CancellationToken>());
		await _client.Received(1).MarkImportedAsync("abc", Arg.Any<CancellationToken>());
		(await _db.TrackedDownloads.AnyAsync(x => x.Id == download.Id, TestContext.Current.CancellationToken)).ShouldBeFalse();
	}

	[Fact]
	public async Task ExecuteAsync_ShouldKeepDownload_WhenImportedButStillSeeding()
	{
		var download = SeedDownload(TrackedDownloadState.IMPORT_PENDING, "abc", seriesId: 1, episodeIds: [1]);
		Config().EnableCompletedDownloadHandling = true;
		Config().RemoveCompletedDownloads = true;
		_db.SaveChanges();
		_client.GetItemsAsync(Arg.Any<CancellationToken>()).Returns([Item("abc", DownloadItemStatus.COMPLETED, canBeRemoved: true, canMoveFiles: false)]);
		SimulateSuccessfulImport();

		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);

		await _client.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
		(await _db.TrackedDownloads.AnyAsync(x => x.Id == download.Id, TestContext.Current.CancellationToken)).ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_ShouldNotRemove_WhenNotBittorrentButCanMoveFilesFalse()
	{
		// non-torrent protocols (usenet) are removable regardless of CanMoveFiles, since there is no seeding concept
		var download = SeedDownload(TrackedDownloadState.IMPORT_PENDING, "abc", seriesId: 1, episodeIds: [1]);
		Config().EnableCompletedDownloadHandling = true;
		Config().RemoveCompletedDownloads = true;
		_db.SaveChanges();
		_client.GetItemsAsync(Arg.Any<CancellationToken>()).Returns([Item("abc", DownloadItemStatus.COMPLETED, canBeRemoved: true, canMoveFiles: false, protocol: Protocol.USENET)]);
		SimulateSuccessfulImport();

		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);

		await _client.Received(1).RemoveAsync("abc", false, Arg.Any<CancellationToken>());
		(await _db.TrackedDownloads.AnyAsync(x => x.Id == download.Id, TestContext.Current.CancellationToken)).ShouldBeFalse();
	}

	[Fact]
	public async Task ExecuteAsync_ShouldBlocklistAndRedownload_WhenDownloadFails()
	{
		var download = SeedDownload(TrackedDownloadState.DOWNLOADING, "fail1", seriesId: 1, episodeIds: [42]);
		Config().EnableFailedDownloadHandling = true;
		Config().RedownloadFailedReleases = true;
		Config().RemoveFailedDownloads = false;
		_db.SaveChanges();
		_client.GetItemsAsync(Arg.Any<CancellationToken>()).Returns([Item("fail1", DownloadItemStatus.FAILED, message: "disk full")]);

		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);

		var reloaded = await _db.TrackedDownloads.FirstAsync(x => x.Id == download.Id, TestContext.Current.CancellationToken);
		reloaded.State.ShouldBe(TrackedDownloadState.FAILED);

		(await _db.BlocklistItems.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
		(await _db.HistoryEvents.CountAsync(x => x.Type == HistoryEventType.FAILED, TestContext.Current.CancellationToken)).ShouldBe(1);
		await _eventBus.Received(1).PublishAsync(Arg.Any<DownloadFailedEvent>(), Arg.Any<CancellationToken>());
		await _commandQueue.Received(1).EnqueueAsync(
			Arg.Is<EpisodeSearchCommand>(x => x.EpisodeIds.SequenceEqual(download.EpisodeIds)),
			CommandTrigger.SYSTEM,
			Arg.Any<CommandPriority>(),
			Arg.Any<CancellationToken>());
		await _client.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ExecuteAsync_ShouldRemoveFromClient_WhenFailedAndRemoveFailedDownloadsEnabled()
	{
		var download = SeedDownload(TrackedDownloadState.DOWNLOADING, "fail2");
		Config().EnableFailedDownloadHandling = true;
		Config().RemoveFailedDownloads = true;
		_db.SaveChanges();
		_client.GetItemsAsync(Arg.Any<CancellationToken>()).Returns([Item("fail2", DownloadItemStatus.FAILED)]);

		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);

		await _client.Received(1).RemoveAsync("fail2", true, Arg.Any<CancellationToken>());
		(await _db.TrackedDownloads.AnyAsync(x => x.Id == download.Id, TestContext.Current.CancellationToken)).ShouldBeFalse();
	}

	[Fact]
	public async Task ExecuteAsync_ShouldNotRedownloadOrBlocklist_WhenFailedHandlingDisabled()
	{
		var download = SeedDownload(TrackedDownloadState.DOWNLOADING, "fail3", seriesId: 1, episodeIds: [1]);
		Config().EnableFailedDownloadHandling = false;
		Config().RemoveFailedDownloads = false;
		_db.SaveChanges();
		_client.GetItemsAsync(Arg.Any<CancellationToken>()).Returns([Item("fail3", DownloadItemStatus.FAILED)]);

		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);

		(await _db.BlocklistItems.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
		await _commandQueue.DidNotReceive().EnqueueAsync(Arg.Any<ICommand>(), Arg.Any<CommandTrigger>(), Arg.Any<CommandPriority>(), Arg.Any<CancellationToken>());
		(await _db.TrackedDownloads.AnyAsync(x => x.Id == download.Id, TestContext.Current.CancellationToken)).ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_ShouldDiscoverUnknownItem_AndMatchLibraryByCleanTitle()
	{
		_db.Series.Add(new Series { Title = "Test Show", CleanTitle = "testshow" });
		_db.SaveChanges();
		_client.GetItemsAsync(Arg.Any<CancellationToken>()).Returns([Item("newitem", DownloadItemStatus.DOWNLOADING)]);

		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);

		var created = await _db.TrackedDownloads.SingleAsync(x => x.DownloadId == "newitem", TestContext.Current.CancellationToken);
		created.SeriesId.ShouldNotBeNull();
		created.State.ShouldBe(TrackedDownloadState.DOWNLOADING);
	}

	[Fact]
	public async Task ExecuteAsync_ShouldMarkReadOnlyUnknownItem_AsIgnored()
	{
		_client.GetItemsAsync(Arg.Any<CancellationToken>()).Returns([new DownloadClientItem { DownloadId = "ro", Title = "Other App Download", Status = DownloadItemStatus.DOWNLOADING, IsReadOnly = true }]);

		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);

		var created = await _db.TrackedDownloads.SingleAsync(x => x.DownloadId == "ro", TestContext.Current.CancellationToken);
		created.State.ShouldBe(TrackedDownloadState.IGNORED);
	}

	[Fact]
	public async Task ExecuteAsync_ShouldKeepTrackedRow_UntilThreeConsecutiveMisses()
	{
		var download = SeedDownload(TrackedDownloadState.DOWNLOADING, "vanished");
		_client.GetItemsAsync(Arg.Any<CancellationToken>()).Returns([]);

		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);
		(await _db.TrackedDownloads.SingleAsync(x => x.Id == download.Id, TestContext.Current.CancellationToken)).MissedPolls.ShouldBe(1);

		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);
		(await _db.TrackedDownloads.AnyAsync(x => x.Id == download.Id, TestContext.Current.CancellationToken)).ShouldBeTrue();

		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);
		(await _db.TrackedDownloads.AnyAsync(x => x.Id == download.Id, TestContext.Current.CancellationToken)).ShouldBeFalse();
		(await _db.HistoryEvents.CountAsync(x => x.Type == HistoryEventType.FAILED && x.DownloadId == "vanished", TestContext.Current.CancellationToken)).ShouldBe(1);
	}

	[Fact]
	public async Task ExecuteAsync_ShouldResetMissedPolls_WhenItemReappears()
	{
		var download = SeedDownload(TrackedDownloadState.DOWNLOADING, "flaky");
		_client.GetItemsAsync(Arg.Any<CancellationToken>()).Returns([]);
		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);
		(await _db.TrackedDownloads.SingleAsync(x => x.Id == download.Id, TestContext.Current.CancellationToken)).MissedPolls.ShouldBe(1);

		_client.GetItemsAsync(Arg.Any<CancellationToken>()).Returns([Item("flaky", DownloadItemStatus.DOWNLOADING)]);
		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);
		(await _db.TrackedDownloads.SingleAsync(x => x.Id == download.Id, TestContext.Current.CancellationToken)).MissedPolls.ShouldBe(0);
	}

	[Fact]
	public async Task ExecuteAsync_ShouldRetryStalledImporting_AfterFifteenMinutes()
	{
		var download = SeedDownload(TrackedDownloadState.IMPORTING, "stuck", seriesId: 1, episodeIds: [1]);
		Config().EnableCompletedDownloadHandling = true;
		_db.SaveChanges();
		_time.Advance(TimeSpan.FromMinutes(20));
		_client.GetItemsAsync(Arg.Any<CancellationToken>()).Returns([Item("stuck", DownloadItemStatus.COMPLETED)]);
		SimulateSuccessfulImport();

		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);

		await _importService.Received(1).ImportTrackedDownloadAsync(download.Id, Arg.Any<CancellationToken>());
	}


	[Fact]
	public async Task ExecuteAsync_ShouldSkipClient_WhenStatusTrackerReportsUnavailable()
	{
		_statusTracker.IsAvailable(_clientEntity.Id).Returns(false);
		_client.GetItemsAsync(Arg.Any<CancellationToken>()).Returns([Item("abc", DownloadItemStatus.DOWNLOADING)]);

		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);

		await _client.DidNotReceive().GetItemsAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ExecuteAsync_ShouldRecordFailure_WhenClientThrows()
	{
		_client.GetItemsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new DownloadClientException("unreachable"));

		await Handler.ExecuteAsync(new DownloadMonitorCommand(), _context, TestContext.Current.CancellationToken);

		await _statusTracker.Received(1).RecordFailureAsync(_clientEntity.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	private void SimulateSuccessfulImport()
		=> _importService.ImportTrackedDownloadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				var id = callInfo.ArgAt<int>(0);
				var row = _db.TrackedDownloads.First(x => x.Id == id);
				row.State = TrackedDownloadState.IMPORTED;
				row.Imported = true;
				_db.SaveChanges();
				return Task.FromResult(new ImportRunSummary([]));
			});
}
