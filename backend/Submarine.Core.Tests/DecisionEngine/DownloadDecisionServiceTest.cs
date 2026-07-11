using Submarine.Core.DecisionEngine;
using Submarine.Core.DecisionEngine.CustomFormats;
using Submarine.Core.DecisionEngine.Filter;
using Submarine.Core.Indexer;
using Submarine.Core.Languages;
using Submarine.Core.Profile;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Xunit;

namespace Submarine.Core.Tests.DecisionEngine;

public class DownloadDecisionServiceTest
{
	private static readonly ReleaseInfo Info = new()
	{
		Title = "Series.Title.S01",
		Guid = "guid",
		Protocol = Protocol.BITTORRENT
	};

	private readonly DownloadDecisionService _instance;

	public DownloadDecisionServiceTest(ITestOutputHelper output)
		=> _instance = new DownloadDecisionService(
			new XunitLogger<DownloadDecisionService>(output),
			new FilterEvaluator(),
			new CustomFormatEvaluator(new XunitLogger<CustomFormatEvaluator>(output)));

	[Fact]
	public void Decide_ShouldReject_WhenQualityNotInProfile()
	{
		var decision = _instance.Decide(Candidate(Release(QualityResolution.R480_P)), Context());

		Assert.False(decision.Approved);
		Assert.Contains(decision.Rejections, r => r.Reason.Contains("quality"));
	}

	[Fact]
	public void Decide_ShouldReject_WhenNoWantedLanguage()
	{
		var decision = _instance.Decide(Candidate(Release(languages: new[] { Language.GERMAN })),
			Context(languageProfile: Languages(Language.ENGLISH)));

		Assert.False(decision.Approved);
		Assert.Contains(decision.Rejections, r => r.Reason.Contains("language"));
	}

	[Fact]
	public void Decide_ShouldApprove_WhenCandidateIsQualityUpgradeOverExisting()
	{
		var decision = _instance.Decide(Candidate(Release(QualityResolution.R1080_P)),
			Context(existing: Quality(QualityResolution.R720_P)));

		Assert.True(decision.Approved);
	}

	[Fact]
	public void Decide_ShouldReject_WhenExistingMeetsCutoff()
	{
		var decision = _instance.Decide(Candidate(Release(QualityResolution.R2160_P)),
			Context(profile: Profile(cutoff: 1), existing: Quality(QualityResolution.R1080_P)));

		Assert.False(decision.Approved);
		Assert.Contains(decision.Rejections, r => r.Reason.Contains("cutoff"));
	}

	[Fact]
	public void Decide_ShouldReject_WhenCandidateIsSidegradeOfExisting()
	{
		var decision = _instance.Decide(Candidate(Release(QualityResolution.R1080_P)),
			Context(existing: Quality(QualityResolution.R1080_P)));

		Assert.False(decision.Approved);
		Assert.Contains(decision.Rejections, r => r.Reason.Contains("upgrade"));
	}

	[Fact]
	public void Decide_ShouldApprove_WhenCandidateIsRevisionUpgradeOverExisting()
	{
		var decision = _instance.Decide(
			Candidate(Release(revision: new Revision(2, IsProper: true))),
			Context(existing: Quality(QualityResolution.R1080_P)));

		Assert.True(decision.Approved);
	}

	[Fact]
	public void Decide_ShouldReject_WhenBlockFilterMatches()
	{
		var filters = new[] { new ReleaseFilter(1, FilterField.RELEASE_GROUP, new[] { "EVO" }, FilterMode.BLOCK) };

		var decision = _instance.Decide(Candidate(Release(releaseGroup: "EVO")), Context(filters: filters));

		Assert.False(decision.Approved);
		Assert.Contains(decision.Rejections, r => r.Reason.Contains("blocked"));
	}

	[Fact]
	public void DecideAll_ShouldOrderByTier_WhenPreferFiltersDiffer()
	{
		var filters = new[]
		{
			new ReleaseFilter(1, FilterField.RELEASE_GROUP, new[] { "TIER1" }, FilterMode.PREFER, 1),
			new ReleaseFilter(2, FilterField.RELEASE_GROUP, new[] { "TIER2" }, FilterMode.PREFER, 2)
		};

		var result = _instance.DecideAll(
			new[] { Candidate(Release(releaseGroup: "TIER2")), Candidate(Release(releaseGroup: "TIER1")) },
			Context(filters: filters));

		Assert.Equal("TIER1", result[0].Candidate.Release.ReleaseGroup);
		Assert.Equal("TIER2", result[1].Candidate.Release.ReleaseGroup);
	}

	[Fact]
	public void DecideAll_ShouldRankByCustomFormatScore_WhenFormatsMatch()
	{
		var format = new CustomFormat(1, "Good Group",
			new[] { new CustomFormatCondition("Good Group", CustomFormatConditionType.RELEASE_GROUP, "^GOOD$", true, false) });

		var result = _instance.DecideAll(
			new[] { Candidate(Release(releaseGroup: "PLAIN")), Candidate(Release(releaseGroup: "GOOD")) },
			Context(customFormats: new[] { format }, customFormatScores: new Dictionary<int, int> { [1] = 500 }));

		Assert.Equal("GOOD", result[0].Candidate.Release.ReleaseGroup);
		Assert.Contains("Good Group", result[0].MatchedCustomFormats);
	}

	[Fact]
	public void DecideAll_ShouldPreferConsistentReleaseGroup_WhenSeasonGroupMatches()
	{
		var consistent = Candidate(Release(releaseGroup: "SAME"), indexerPriority: 100);
		var other = Candidate(Release(releaseGroup: "OTHER"));

		var result = _instance.DecideAll(new[] { other, consistent }, Context(seasonReleaseGroup: "SAME"));

		Assert.Equal("SAME", result[0].Candidate.Release.ReleaseGroup);
	}

	[Fact]
	public void DecideAll_ShouldPreferFullSeason_WhenComparableToEpisode()
	{
		var season = Candidate(Release(seriesData: Series(SeriesReleaseType.FULL_SEASON)));
		var episode = Candidate(Release(seriesData: Series(SeriesReleaseType.EPISODE)));

		var result = _instance.DecideAll(new[] { episode, season }, Context());

		Assert.Equal(SeriesReleaseType.FULL_SEASON, result[0].Candidate.Release.SeriesReleaseData!.ReleaseType);
	}

	[Fact]
	public void DecideAll_ShouldOrderApprovedFirst_WhenSomeRejected()
	{
		var result = _instance.DecideAll(
			new[]
			{
				Candidate(Release(languages: new[] { Language.GERMAN })),
				Candidate(Release(languages: new[] { Language.ENGLISH }))
			},
			Context(languageProfile: Languages(Language.ENGLISH)));

		Assert.True(result[0].Approved);
		Assert.False(result[1].Approved);
	}

	private static BaseRelease Release(
		QualityResolution resolution = QualityResolution.R1080_P,
		Revision? revision = null,
		string? releaseGroup = "FLUX",
		IReadOnlyList<Language>? languages = null,
		StreamingProvider? source = StreamingProvider.AMAZON,
		SeriesReleaseData? seriesData = null)
		=> new()
		{
			FullTitle = "Series.Title.S01.1080p.AMZN.WEB-DL.DDP5.1.H.264-FLUX",
			Title = "Series Title",
			Aliases = Array.Empty<string>(),
			Languages = languages ?? new[] { Language.ENGLISH },
			StreamingProvider = source,
			Type = ReleaseType.SERIES,
			SeriesReleaseData = seriesData,
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, resolution),
				revision ?? new Revision()),
			Protocol = Protocol.BITTORRENT,
			ReleaseGroup = releaseGroup
		};

	private static ReleaseCandidate Candidate(BaseRelease release, string? indexerName = "MyIndexer",
		int indexerPriority = 25)
		=> new(release, Info, indexerName, indexerPriority);

	private static MediaContext Context(
		QualityProfile? profile = null,
		LanguageProfile? languageProfile = null,
		IReadOnlyCollection<ReleaseFilter>? filters = null,
		IReadOnlyCollection<CustomFormat>? customFormats = null,
		IReadOnlyDictionary<int, int>? customFormatScores = null,
		QualityModel? existing = null,
		string? seasonReleaseGroup = null)
		=> new()
		{
			QualityProfile = profile ?? Profile(),
			LanguageProfile = languageProfile ?? Languages(Language.ENGLISH),
			Filters = filters ?? Array.Empty<ReleaseFilter>(),
			CustomFormats = customFormats ?? Array.Empty<CustomFormat>(),
			CustomFormatScores = customFormatScores ?? new Dictionary<int, int>(),
			ExistingFileQuality = existing,
			SeasonReleaseGroup = seasonReleaseGroup
		};

	private static QualityProfile Profile(int cutoff = 2, bool upgradeAllowed = true)
		=> new()
		{
			Name = "Test",
			UpgradeAllowed = upgradeAllowed,
			Cutoff = cutoff,
			Items = new List<QualityProfileItem>
			{
				Item(QualityResolution.R720_P),
				Item(QualityResolution.R1080_P),
				Item(QualityResolution.R2160_P)
			}
		};

	private static QualityProfileItem Item(QualityResolution resolution)
		=> new() { Quality = new QualityResolutionModel(QualitySource.WEB_DL, resolution), Allowed = true };

	private static LanguageProfile Languages(params Language[] languages)
		=> new() { Name = "Test", Languages = languages.ToList() };

	private static QualityModel Quality(QualityResolution resolution)
		=> new(new QualityResolutionModel(QualitySource.WEB_DL, resolution), new Revision());

	private static SeriesReleaseData Series(SeriesReleaseType type)
		=> new()
		{
			ReleaseType = type,
			Seasons = new[] { 1 },
			Episodes = Array.Empty<int>(),
			AbsoluteEpisodes = Array.Empty<int>()
		};
}
