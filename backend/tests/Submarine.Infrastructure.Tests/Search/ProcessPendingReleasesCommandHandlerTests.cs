using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Commands;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Entities;
using Submarine.Core.Download;
using Submarine.Core.Indexers;
using Submarine.Core.Quality;
using Submarine.Core.Profiles;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Downloads;
using Submarine.Core.Parser;
using Submarine.Core.Parser.Release;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Release.Usenet;
using Submarine.Core.Provider;
using Submarine.Core.Validator;
using Submarine.Infrastructure.Grab;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Search;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Search;

public sealed class ProcessPendingReleasesCommandHandlerTests : IDisposable
{
	private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero));
	private readonly SqliteSubmarineDbContext _db;
	private readonly IDownloadDecisionMaker _decisionMaker = Substitute.For<IDownloadDecisionMaker>();
	private readonly IGrabService _grabService = Substitute.For<IGrabService>();

	public ProcessPendingReleasesCommandHandlerTests() => _db = TestDbFactory.Create(_clock);

	public void Dispose() => _db.Dispose();

	[Fact]
	public async Task ExecuteAsync_ShouldPrunePendingReleaseOlderThanFourteenDays()
	{
		var pending = new PendingRelease
		{
			Title = "expired",
			Release = "{}",
			Added = _clock.GetUtcNow().UtcDateTime.AddDays(-14).AddTicks(-1)
		};
		_db.PendingReleases.Add(pending);
		await _db.SaveChangesAsync();

		await CreateHandler().ExecuteAsync(new ProcessPendingReleasesCommand(), Substitute.For<ICommandContext>(), TestContext.Current.CancellationToken);

		(await _db.PendingReleases.FindAsync(pending.Id)).ShouldBeNull();
		_decisionMaker.DidNotReceive().Decide(Arg.Any<ReleaseCandidate>(), Arg.Any<DecisionContext>(), Arg.Any<DateTime?>());
		await _grabService.DidNotReceive().GrabAsync(Arg.Any<DownloadDecision>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<int>?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
	}
	[Fact]
	public async Task ExecuteAsync_ShouldRemovePendingMovieReleaseWhenCutoffIsSatisfied()
	{
		var profile = new QualityProfile
		{
			Name = "cutoff",
			Cutoff = 0,
			Items = [new QualityProfileItem(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), true)]
		};
		var languageProfile = new LanguageProfile { Name = "language" };
		var movie = new Movie { TmdbId = 702, Title = "Satisfied movie" };
		_db.AddRange(profile, languageProfile, movie);
		await _db.SaveChangesAsync();
		var version = new MediaVersion
		{
			Name = "main", MovieId = movie.Id, Path = "movie", QualityProfileId = profile.Id,
			LanguageProfileId = languageProfile.Id, RootFolderId = 0
		};
		_db.MediaVersions.Add(version);
		_db.MovieFiles.Add(new MovieFile
		{
			MovieId = movie.Id,
			MediaVersion = version,
			RelativePath = "movie.mkv",
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision())
		});
		var pending = new PendingRelease
		{
			Title = "obsolete", Release = "{}", MovieId = movie.Id, Added = _clock.GetUtcNow().UtcDateTime
		};
		_db.PendingReleases.Add(pending);
		await _db.SaveChangesAsync();

		await CreateHandler().ExecuteAsync(new ProcessPendingReleasesCommand(), Substitute.For<ICommandContext>(), TestContext.Current.CancellationToken);

		(await _db.PendingReleases.FindAsync(pending.Id)).ShouldBeNull();
		_decisionMaker.DidNotReceive().Decide(Arg.Any<ReleaseCandidate>(), Arg.Any<DecisionContext>(), Arg.Any<DateTime?>());
	}
	[Fact]
	public async Task ExecuteAsync_ShouldReDecideAndGrabOnceAfterTemporaryRejectionClears()
	{
		var series = new Series { TvdbId = 701, Title = "Automatic search series" };
		_db.Series.Add(series);
		await _db.SaveChangesAsync();
		var version = new MediaVersion
		{
			Name = "main", SeriesId = series.Id, Path = "series", QualityProfileId = 1, LanguageProfileId = 1, RootFolderId = 0
		};
		_db.MediaVersions.Add(version);
		var release = new ReleaseInfo
		{
			Guid = "release-1",
			Title = "Automatic.search.series.S01E01.1080p-GROUP",
			Protocol = Protocol.BITTORRENT,
			Size = 1000
		};
		var pending = new PendingRelease
		{
			Title = release.Title,
			Release = JsonSerializer.Serialize(release),
			SeriesId = series.Id,
			EpisodeIds = [101],
			Added = _clock.GetUtcNow().UtcDateTime
		};
		_db.PendingReleases.Add(pending);
		await _db.SaveChangesAsync();
		_decisionMaker.Decide(Arg.Any<ReleaseCandidate>(), Arg.Any<DecisionContext>(), Arg.Any<DateTime?>())
			.Returns(call => new DownloadDecision(call.Arg<ReleaseCandidate>(), true, 100, [], [], 0));
		_grabService.GrabAsync(Arg.Any<DownloadDecision>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<int>?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<GrabOutcome>(new GrabbedOutcome(new TrackedDownload())));
		var handler = CreateHandler();

		await handler.ExecuteAsync(new ProcessPendingReleasesCommand(), Substitute.For<ICommandContext>(), TestContext.Current.CancellationToken);
		await handler.ExecuteAsync(new ProcessPendingReleasesCommand(), Substitute.For<ICommandContext>(), TestContext.Current.CancellationToken);

		await _grabService.Received(1).GrabAsync(Arg.Any<DownloadDecision>(), version.Id, series.Id, pending.EpisodeIds, null, Arg.Any<CancellationToken>());
		(await _db.PendingReleases.FindAsync(pending.Id)).ShouldBeNull();
	}

	private ProcessPendingReleasesCommandHandler CreateHandler()
	{
		var contextFactory = new DecisionContextFactory(_db, new BlocklistService(_db));
		return new ProcessPendingReleasesCommandHandler(
			_db, contextFactory, _decisionMaker, _grabService, CreateTorrentParser(), CreateUsenetParser(), _clock,
			NullLogger<ProcessPendingReleasesCommandHandler>.Instance);
	}

	private static IParser<TorrentRelease> CreateTorrentParser() => new TorrentReleaseParserService(
		NullLogger<TorrentReleaseParserService>.Instance,
		new TorrentReleaseValidatorService(NullLogger<TorrentReleaseValidatorService>.Instance), TestReleaseParserFactory.Create());
	private static IParser<UsenetRelease> CreateUsenetParser() => new UsenetReleaseParserService(
		NullLogger<UsenetReleaseParserService>.Instance,
		new UsenetReleaseValidatorService(NullLogger<UsenetReleaseValidatorService>.Instance), TestReleaseParserFactory.Create());
}
