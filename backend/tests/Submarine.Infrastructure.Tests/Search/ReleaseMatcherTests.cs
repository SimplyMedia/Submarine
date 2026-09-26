using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Release;
using Submarine.Infrastructure.Mappings;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Search;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Search;

// Ported from v1 Submarine.Api.Tests/ReleaseMediaMatcherTest.cs. Matching moved from a single in-memory
// ReleaseMediaMatcher (Normalize + MatchSeries/MatchMovie over pre-loaded lists) to a DB-backed ReleaseMatcher: pure
// title comparison now lives in TitleMatcher (see TitleMatcherTests) while this class covers the EF-integrated
// lookups (FindSeriesAsync/FindMovieAsync/MatchesMovieAsync) that TitleMatcher alone cannot exercise.
public sealed class ReleaseMatcherTests : IDisposable
{
	private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
	private readonly SqliteSubmarineDbContext _db;
	private readonly IMappingsClient _mappingsClient = Substitute.For<IMappingsClient>();
	private readonly ReleaseMatcher _instance;

	public ReleaseMatcherTests()
	{
		_db = TestDbFactory.Create(_clock);
		_instance = new ReleaseMatcher(_db, _mappingsClient);
	}

	public void Dispose() => _db.Dispose();

	private static BaseRelease Release(string title, int? year = null, SeriesReleaseData? seriesData = null)
		=> new() { FullTitle = title, Title = title, Year = year, SeriesReleaseData = seriesData };

	[Fact]
	public async Task MatchLibraryAsync_ShouldMatchSeries_WhenTitleMatchesIgnoringPunctuationAndCase()
	{
		var series = new Series { Title = "The Office", TvdbId = 7 };
		series.Episodes.Add(new Episode { SeasonNumber = 1, EpisodeNumber = 1 });
		_db.Series.Add(series);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var release = Release("the office!",
			seriesData: new SeriesReleaseData { ReleaseType = SeriesReleaseType.EPISODE, Seasons = [1], Episodes = [1] });

		var match = await _instance.MatchLibraryAsync(release, TestContext.Current.CancellationToken);

		match.ShouldNotBeNull();
		match.SeriesId.ShouldBe(series.Id);
		match.EpisodeIds.ShouldHaveSingleItem();
	}

	[Fact]
	public async Task MatchSeriesAsync_ShouldMatchByEpisodeNumbers_RegardlessOfReleaseTitle()
	{
		// unlike v1's ReleaseMediaMatcher, which took a tvdbId override to bypass a title mismatch, v2 never
		// re-checks the title once the caller already resolved the series (e.g. a per-series automatic search):
		// MatchSeriesAsync only resolves numbering against that series' episodes.
		var series = new Series { Title = "Original Title", TvdbId = 42 };
		series.Episodes.Add(new Episode { SeasonNumber = 1, EpisodeNumber = 1 });
		_db.Series.Add(series);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var release = Release("completely different",
			seriesData: new SeriesReleaseData { ReleaseType = SeriesReleaseType.EPISODE, Seasons = [1], Episodes = [1] });

		var match = await _instance.MatchSeriesAsync(series, release, TestContext.Current.CancellationToken);

		match.ShouldNotBeNull();
		match.SeriesId.ShouldBe(series.Id);
	}

	[Fact]
	public async Task MatchesMovieAsync_ShouldMatchByTmdbId_WhenInfoReportsIt()
	{
		var movie = new Movie { Title = "Dune", TmdbId = 438631, Year = 2021 };

		var matches = await _instance.MatchesMovieAsync(movie, new ReleaseInfo { TmdbId = 438631 },
			Release("completely unrelated title"), TestContext.Current.CancellationToken);

		matches.ShouldBeTrue();
	}

	[Fact]
	public async Task MatchesMovieAsync_ShouldMatchByImdbId_WhenInfoReportsItAndTmdbIdMissing()
	{
		var movie = new Movie { Title = "Dune", TmdbId = 438631, ImdbId = "tt1160419", Year = 2021 };

		var matches = await _instance.MatchesMovieAsync(movie, new ReleaseInfo { ImdbId = "tt1160419" },
			Release("completely unrelated title"), TestContext.Current.CancellationToken);

		matches.ShouldBeTrue();
	}

	[Theory]
	[InlineData(2021, true)]
	[InlineData(2022, true)]
	[InlineData(2015, false)]
	public async Task MatchesMovieAsync_ShouldHonorYearTolerance_WhenNoIdsReported(int releaseYear, bool expectMatch)
	{
		var movie = new Movie { Title = "Dune", TmdbId = 438631, Year = 2021 };

		var matches = await _instance.MatchesMovieAsync(movie, new ReleaseInfo(),
			Release("dune", releaseYear), TestContext.Current.CancellationToken);

		matches.ShouldBe(expectMatch);
	}

	[Fact]
	public async Task MatchLibraryAsync_ShouldMatchMovie_WhenTitleMatchesAndNoSeriesReleaseData()
	{
		var movie = new Movie { Title = "Dune", TmdbId = 438631, Year = 2021 };
		_db.Movies.Add(movie);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var match = await _instance.MatchLibraryAsync(Release("dune", 2021), TestContext.Current.CancellationToken);

		match.ShouldNotBeNull();
		match.MovieId.ShouldBe(movie.Id);
	}
}
