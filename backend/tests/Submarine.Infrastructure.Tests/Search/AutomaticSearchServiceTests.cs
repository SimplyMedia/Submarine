using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Languages;
using Submarine.Core.Parser;
using Submarine.Core.Parser.Release;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Release.Usenet;
using Submarine.Core.Validator;
using Submarine.Infrastructure.Downloads;
using Submarine.Infrastructure.Grab;
using Submarine.Infrastructure.Mappings;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Search;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Search;

public sealed class AutomaticSearchServiceTests : IDisposable
{
	private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
	private readonly SqliteSubmarineDbContext _db;
	private readonly IGrabService _grabService = Substitute.For<IGrabService>();
	private readonly IDownloadDecisionMaker _decisionMaker = Substitute.For<IDownloadDecisionMaker>();
	private readonly AutomaticSearchService _service;
	private readonly Series _series;

	public AutomaticSearchServiceTests()
	{
		_db = TestDbFactory.Create(_clock);
		var qualityProfile = new QualityProfile { Name = "P", Cutoff = 0, Items = [new QualityProfileItem(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), true)] };
		var languageProfile = new LanguageProfile { Name = "L", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
		_series = new Series { TvdbId = 940001, Title = "Automatic search series" };
		_db.AddRange(qualityProfile, languageProfile, _series);
		_db.SaveChanges();

		var version = new MediaVersion { Name = "Main", SeriesId = _series.Id, QualityProfileId = qualityProfile.Id, LanguageProfileId = languageProfile.Id, RootFolderId = 0, Path = "Automatic" };
		_db.MediaVersions.Add(version);
		_db.SaveChanges();

		var mappingsClient = Substitute.For<IMappingsClient>();
		var matcher = new ReleaseMatcher(_db, mappingsClient);
		var contextFactory = new DecisionContextFactory(_db, new BlocklistService(_db));
		var episodePlanner = new EpisodeSearchPlanner(mappingsClient);
		IParser<TorrentRelease> torrentParser = new TorrentReleaseParserService(
			NullLogger<TorrentReleaseParserService>.Instance,
			new TorrentReleaseValidatorService(NullLogger<TorrentReleaseValidatorService>.Instance),
			TestReleaseParserFactory.Create());
		IParser<UsenetRelease> usenetParser = new UsenetReleaseParserService(
			NullLogger<UsenetReleaseParserService>.Instance,
			new UsenetReleaseValidatorService(NullLogger<UsenetReleaseValidatorService>.Instance),
			TestReleaseParserFactory.Create());

		// Every candidate is approved; the orchestration logic under test is season-pack-first grouping and
		// the grabbed-episode tracking, not the decision engine itself (covered separately).
		_decisionMaker.DecideAll(Arg.Any<IReadOnlyCollection<ReleaseCandidate>>(), Arg.Any<DecisionContext>(), Arg.Any<DateTime?>())
			.Returns(callInfo => callInfo.Arg<IReadOnlyCollection<ReleaseCandidate>>()
				.Select(candidate => new DownloadDecision(candidate, true, 100, [], [], 0))
				.ToList());

		_grabService.GrabAsync(Arg.Any<DownloadDecision>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<int>?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<GrabOutcome>(new GrabbedOutcome(new TrackedDownload())));

		_service = new AutomaticSearchService(
			_db, null!, matcher, contextFactory, _decisionMaker, _grabService, episodePlanner,
			torrentParser, usenetParser, _clock, NullLogger<AutomaticSearchService>.Instance);
	}

	public void Dispose() => _db.Dispose();

	private static ReleaseCandidate Candidate(string fullTitle, IReadOnlyList<int> episodeIds)
	{
		var release = new TorrentRelease(new BaseRelease
		{
			FullTitle = fullTitle,
			Title = "Automatic search series",
			ReleaseGroup = "GROUP",
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()),
			Languages = [Language.ENGLISH]
		});
		var info = new ReleaseInfo { Guid = fullTitle, Title = fullTitle, Protocol = Protocol.BITTORRENT, Size = 1000 };
		return new ReleaseCandidate(release, info, null, null, null, episodeIds);
	}

	[Fact]
	public async Task ProcessSeriesCandidatesAsync_ShouldPreferSeasonPack_AndSkipOverlappingSingleEpisode_WithoutEvenDeciding()
	{
		var seasonPack = Candidate("Automatic.search.series.S01.1080p-GROUP", [1, 2]);
		var single = Candidate("Automatic.search.series.S01E01.1080p-GROUP", [1]);

		await _service.ProcessSeriesCandidatesAsync(_series.Id, [single, seasonPack], TestContext.Current.CancellationToken);

		await _grabService.Received(1).GrabAsync(
			Arg.Is<DownloadDecision>(decision => decision.Candidate.EpisodeIds!.SequenceEqual(new[] { 1, 2 })),
			Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<int>?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
		_decisionMaker.ReceivedCalls().Count().ShouldBe(1);
	}

	[Fact]
	public async Task ProcessSeriesCandidatesAsync_ShouldGrabEachGroup_WhenEpisodeSetsDoNotOverlap()
	{
		var seasonPackEpisode1 = Candidate("Automatic.search.series.S01.1080p-GROUP", [1]);
		var singleEpisode2 = Candidate("Automatic.search.series.S01E02.1080p-GROUP", [2]);

		await _service.ProcessSeriesCandidatesAsync(_series.Id, [seasonPackEpisode1, singleEpisode2], TestContext.Current.CancellationToken);

		await _grabService.Received(2).GrabAsync(
			Arg.Any<DownloadDecision>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<int>?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
	}
}
