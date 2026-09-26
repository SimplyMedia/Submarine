using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Parser;
using Submarine.Core.Parser.Release;
using Submarine.Core.Provider;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Release.Usenet;
using Submarine.Core.Validator;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.IndexerManagement;
using Submarine.Infrastructure.Mappings;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Search;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Search;

public sealed class RssSyncCommandHandlerTests : IDisposable
{
	private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
	private readonly SqliteSubmarineDbContext _db;
	private readonly IIndexerProvider _provider = Substitute.For<IIndexerProvider>();
	private readonly IIndexerStatusService _status = Substitute.For<IIndexerStatusService>();
	private readonly ICommandQueue _queue = Substitute.For<ICommandQueue>();
	private readonly ICommandContext _context = Substitute.For<ICommandContext>();

	public RssSyncCommandHandlerTests() => _db = TestDbFactory.Create(_clock);

	public void Dispose() => _db.Dispose();

	[Fact]
	public async Task ExecuteAsync_ShouldNotFetchOrQueue_WhenRssIntervalIsZero()
	{
		_db.IndexerConfig.Add(new IndexerConfig { RssSyncIntervalMinutes = 0 });
		await _db.SaveChangesAsync();

		var indexer = Substitute.For<IIndexer>();
		var entity = new Indexer { Name = "disabled" };
		_provider.GetEnabledAsync(IndexerSearchMode.RSS, Arg.Any<CancellationToken>())
			.Returns([new ConfiguredIndexer(entity, indexer)]);
		var handler = CreateHandler();

		await handler.ExecuteAsync(new RssSyncCommand(), _context, TestContext.Current.CancellationToken);

		await indexer.DidNotReceive().FetchRssAsync(Arg.Any<CancellationToken>());
		await _queue.DidNotReceive().EnqueueAsync(Arg.Any<ICommand>(), Arg.Any<CommandTrigger>(), Arg.Any<CommandPriority>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ExecuteAsync_ShouldContinueAfterAnIndexerFails()
	{
		var brokenEntity = new Indexer { Name = "broken" };
		var healthyEntity = new Indexer { Name = "healthy" };
		_db.Indexers.AddRange(brokenEntity, healthyEntity);
		await _db.SaveChangesAsync();
		var first = Substitute.For<IIndexer>();
		first.FetchRssAsync(Arg.Any<CancellationToken>()).Returns<Task<IReadOnlyList<ReleaseInfo>>>(_ => throw new HttpRequestException("unavailable"));
		var second = Substitute.For<IIndexer>();
		second.FetchRssAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<ReleaseInfo>());
		_provider.GetEnabledAsync(IndexerSearchMode.RSS, Arg.Any<CancellationToken>())
			.Returns([
				new ConfiguredIndexer(brokenEntity, first),
				new ConfiguredIndexer(healthyEntity, second)
			]);
		var handler = CreateHandler();

		await Should.NotThrowAsync(() => handler.ExecuteAsync(new RssSyncCommand(), _context, TestContext.Current.CancellationToken));

		await second.Received(1).FetchRssAsync(Arg.Any<CancellationToken>());
		await _status.Received(1).RecordFailureAsync(brokenEntity.Id, Arg.Any<CancellationToken>());
		await _status.Received(1).RecordSuccessAsync(healthyEntity.Id, Arg.Any<CancellationToken>());
		await _queue.Received(1).EnqueueAsync(Arg.Is<ProcessPendingReleasesCommand>(_ => true), CommandTrigger.SYSTEM,
			Arg.Any<CommandPriority>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ExecuteAsync_ShouldStopProcessingAtLastSeenGuidForThatIndexer()
	{
		var entity = new Indexer { Name = "feed" };
		_db.Indexers.Add(entity);
		await _db.SaveChangesAsync();
		_db.IndexerStatuses.Add(new IndexerStatus { IndexerId = entity.Id, LastRssSyncReleaseInfo = "seen-guid" });
		await _db.SaveChangesAsync();
		var releases = new[]
		{
			new ReleaseInfo { Guid = "new-guid", Title = "new-title", Protocol = Protocol.BITTORRENT },
			new ReleaseInfo { Guid = "seen-guid", Title = "seen-title", Protocol = Protocol.BITTORRENT },
			new ReleaseInfo { Guid = "older-guid", Title = "older-title", Protocol = Protocol.BITTORRENT }
		};
		var client = Substitute.For<IIndexer>();
		client.FetchRssAsync(Arg.Any<CancellationToken>()).Returns(releases);
		_provider.GetEnabledAsync(IndexerSearchMode.RSS, Arg.Any<CancellationToken>())
			.Returns([new ConfiguredIndexer(entity, client)]);
		var parser = Substitute.For<IParser<TorrentRelease>>();
		parser.Parse(Arg.Any<string>()).Returns(_ => throw new FormatException());

		await CreateHandler(parser).ExecuteAsync(new RssSyncCommand(), _context, TestContext.Current.CancellationToken);

		parser.Received(1).Parse("new-title");
		parser.DidNotReceive().Parse("seen-title");
		parser.DidNotReceive().Parse("older-title");
		(await _db.IndexerStatuses.FindAsync(entity.Id))!.LastRssSyncReleaseInfo.ShouldBe("new-guid");
	}

	private RssSyncCommandHandler CreateHandler(IParser<TorrentRelease>? torrentParser = null)
	{
		var matcher = new ReleaseMatcher(_db, Substitute.For<IMappingsClient>());
		return new RssSyncCommandHandler(
			_db, _provider, _status, new IndexerHistoryRecorder(_db, _clock), matcher, null!, _queue,
			torrentParser ?? CreateTorrentParser(), CreateUsenetParser(), NullLogger<RssSyncCommandHandler>.Instance);
	}


	private static IParser<TorrentRelease> CreateTorrentParser() => new TorrentReleaseParserService(
		NullLogger<TorrentReleaseParserService>.Instance,
		new TorrentReleaseValidatorService(NullLogger<TorrentReleaseValidatorService>.Instance), TestReleaseParserFactory.Create());

	private static IParser<UsenetRelease> CreateUsenetParser() => new UsenetReleaseParserService(
		NullLogger<UsenetReleaseParserService>.Instance,
		new UsenetReleaseValidatorService(NullLogger<UsenetReleaseValidatorService>.Instance), TestReleaseParserFactory.Create());
}
