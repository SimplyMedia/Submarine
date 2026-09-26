using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Entities;
using Submarine.Core.Indexers;
using Submarine.Core.Parser;
using Submarine.Core.Parser.Release;
using Submarine.Core.Provider;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Release.Usenet;
using Submarine.Core.Search;
using Submarine.Core.Validator;
using Submarine.Infrastructure.Downloads;
using Submarine.Infrastructure.IndexerManagement;
using Submarine.Infrastructure.Mappings;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Search;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Search;

public sealed class InteractiveSearchServiceTests : IDisposable
{
	private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
	private readonly SqliteSubmarineDbContext _db;
	private readonly IIndexerProvider _provider = Substitute.For<IIndexerProvider>();
	private readonly IIndexerStatusService _status = Substitute.For<IIndexerStatusService>();
	private readonly List<SearchRequest> _requests = [];
	private readonly InteractiveSearchService _service;

	public InteractiveSearchServiceTests()
	{
		_db = TestDbFactory.Create(_clock);
		var indexer = new Indexer { Name = "search test" };
		_db.Indexers.Add(indexer);
		_db.SaveChanges();
		var client = Substitute.For<IIndexer>();
		client.FetchAsync(Arg.Any<SearchRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
		{
			_requests.Add(call.Arg<SearchRequest>());
			return Task.FromResult<IReadOnlyList<ReleaseInfo>>([]);
		});
		_provider.GetEnabledAsync(IndexerSearchMode.INTERACTIVE, Arg.Any<CancellationToken>())
			.Returns([new ConfiguredIndexer(indexer, client)]);
		_status.RecordSuccessAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		var history = new IndexerHistoryRecorder(_db, _clock);
		var search = new ReleaseSearchService(_provider, _status, history, NullLogger<ReleaseSearchService>.Instance);
		var mappings = Substitute.For<IMappingsClient>();
		_service = new InteractiveSearchService(
			_db, search, new ReleaseMatcher(_db, mappings),
			new DecisionContextFactory(_db, new BlocklistService(_db)),
			Substitute.For<IDownloadDecisionMaker>(), new ReleaseResultCache(new MemoryCache(new MemoryCacheOptions())),
			new EpisodeSearchPlanner(mappings), CreateTorrentParser(), CreateUsenetParser());
	}

	public void Dispose() => _db.Dispose();

	[Fact]
	public async Task SearchAsync_ShouldBuildRequestsForEverySearchScope()
	{
		var series = new Series { TvdbId = 101, Title = "Scope Show" };
		var movie = new Movie { TmdbId = 202, Title = "Scope Film", Year = 2020 };
		_db.AddRange(series, movie);
		await _db.SaveChangesAsync();
		var episode = new Episode
		{
			SeriesId = series.Id,
			SeasonNumber = 2,
			EpisodeNumber = 3,
			SceneSeasonNumber = 4,
			SceneEpisodeNumber = 5
		};
		_db.Episodes.Add(episode);
		await _db.SaveChangesAsync();

		await _service.SearchAsync(series.Id, null, null, null, null, null, indexerIds: [1], cancellationToken: TestContext.Current.CancellationToken);
		await _service.SearchAsync(series.Id, 2, null, null, null, null, cancellationToken: TestContext.Current.CancellationToken);
		await _service.SearchAsync(null, null, episode.Id, null, null, null, cancellationToken: TestContext.Current.CancellationToken);
		await _service.SearchAsync(null, null, null, movie.Id, null, null, cancellationToken: TestContext.Current.CancellationToken);
		await _service.SearchAsync(null, null, null, null, "free words", null, [5000], type: "movie", cancellationToken: TestContext.Current.CancellationToken);

		_requests.Count.ShouldBe(5);
		_requests[0].ShouldBeOfType<TvSearchRequest>().Query.ShouldBe("Scope Show");
		var season = _requests[1].ShouldBeOfType<TvSearchRequest>();
		season.Query.ShouldBe("Scope Show S02");
		season.Season.ShouldBe(2);
		var episodeQuery = _requests[2].ShouldBeOfType<TvSearchRequest>();
		episodeQuery.Query.ShouldBe("Scope Show S04E05");
		episodeQuery.Season.ShouldBe(4);
		episodeQuery.Episode.ShouldBe(5);
		var movieQuery = _requests[3].ShouldBeOfType<MovieSearchRequest>();
		movieQuery.Query.ShouldBe("Scope Film");
		movieQuery.Year.ShouldBe(2020);
		var textQuery = _requests[4].ShouldBeOfType<MovieSearchRequest>();
		textQuery.Query.ShouldBe("free words");
		textQuery.Categories.ShouldBe([5000]);
	}

	private static IParser<TorrentRelease> CreateTorrentParser() => new TorrentReleaseParserService(
		NullLogger<TorrentReleaseParserService>.Instance,
		new TorrentReleaseValidatorService(NullLogger<TorrentReleaseValidatorService>.Instance), TestReleaseParserFactory.Create());

	private static IParser<UsenetRelease> CreateUsenetParser() => new UsenetReleaseParserService(
		NullLogger<UsenetReleaseParserService>.Instance,
		new UsenetReleaseValidatorService(NullLogger<UsenetReleaseValidatorService>.Instance), TestReleaseParserFactory.Create());
}
