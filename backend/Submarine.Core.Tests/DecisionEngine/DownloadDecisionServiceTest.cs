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
	public void Decide_ShouldApprove_WhenCandidateIsProperAtTheQualityCutoff()
	{
		var decision = _instance.Decide(
			Candidate(Release(QualityResolution.R1080_P, revision: new Revision(2, IsProper: true))),
			Context(profile: Profile(cutoff: 1), existing: Quality(QualityResolution.R1080_P)));

		Assert.True(decision.Approved);
	}

	[Fact]
	public void Decide_ShouldReject_WhenExistingMeetsLanguageCutoffAndCandidateAddsNoImprovement()
	{
		var decision = _instance.Decide(
			Candidate(Release(languages: new[] { Language.ENGLISH })),
			Context(languageProfile: LanguageProfile(Language.ENGLISH, upgradeAllowed: true, Language.ENGLISH, Language.GERMAN),
				existingLanguages: new[] { Language.ENGLISH }));

		Assert.False(decision.Approved);
		Assert.Contains(decision.Rejections, r => r.Reason.Contains("language cutoff"));
	}

	[Fact]
	public void Decide_ShouldApprove_WhenCandidateAddsABetterRankedLanguage()
	{
		var decision = _instance.Decide(
			Candidate(Release(languages: new[] { Language.ENGLISH })),
			Context(languageProfile: LanguageProfile(Language.ENGLISH, upgradeAllowed: true, Language.ENGLISH, Language.GERMAN),
				existingLanguages: new[] { Language.GERMAN }));

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
	public void Decide_ShouldRejectTemporary_WhenSeedersBelowMinimum()
	{
		var decision = _instance.Decide(Candidate(Release(), seeders: 1, minimumSeeders: 3), Context());

		Assert.False(decision.Approved);
		var rejection = Assert.Single(decision.Rejections);
		Assert.Equal("1 seeders, minimum is 3", rejection.Reason);
		Assert.Equal(RejectionType.TEMPORARY, rejection.Type);
	}

	[Fact]
	public void Decide_ShouldApprove_WhenSeedersMeetMinimum()
	{
		var decision = _instance.Decide(Candidate(Release(), seeders: 3, minimumSeeders: 3), Context());

		Assert.True(decision.Approved);
	}

	[Fact]
	public void Decide_ShouldApprove_WhenSeedersUnknown()
	{
		var decision = _instance.Decide(Candidate(Release(), minimumSeeders: 3), Context());

		Assert.True(decision.Approved);
	}

	[Fact]
	public void Decide_ShouldIgnoreMinimumSeeders_WhenReleaseIsUsenet()
	{
		var decision = _instance.Decide(
			Candidate(Release(protocol: Protocol.USENET), seeders: 1, minimumSeeders: 3), Context());

		Assert.True(decision.Approved);
	}

	[Fact]
	public void Decide_ShouldRejectTemporary_WhenWithinDelayWindow()
	{
		var now = DateTimeOffset.UtcNow;

		var decision = _instance.Decide(Candidate(Release(), publishDate: now),
			Context(delayProfile: Delay(torrentDelayMinutes: 60)), now);

		Assert.False(decision.Approved);
		var rejection = Assert.Single(decision.Rejections);
		Assert.Equal("waiting for delay window", rejection.Reason);
		Assert.Equal(RejectionType.TEMPORARY, rejection.Type);
	}

	[Fact]
	public void Decide_ShouldApprove_WhenDelayWindowElapsed()
	{
		var now = DateTimeOffset.UtcNow;

		var decision = _instance.Decide(Candidate(Release(), publishDate: now.AddMinutes(-120)),
			Context(delayProfile: Delay(torrentDelayMinutes: 60)), now);

		Assert.True(decision.Approved);
	}

	[Fact]
	public void Decide_ShouldBypassDelay_WhenBypassEnabledAndCandidateAtHighestAllowedQuality()
	{
		var now = DateTimeOffset.UtcNow;

		var decision = _instance.Decide(Candidate(Release(QualityResolution.R2160_P), publishDate: now),
			Context(delayProfile: Delay(torrentDelayMinutes: 60, bypassIfHighestQuality: true)), now);

		Assert.True(decision.Approved);
	}

	[Fact]
	public void DecideAll_ShouldPreferPreferredProtocol_WhenScoresOtherwiseEqual()
	{
		var torrent = Candidate(Release(protocol: Protocol.BITTORRENT));
		var usenet = Candidate(Release(protocol: Protocol.USENET));

		var result = _instance.DecideAll(new[] { torrent, usenet },
			Context(delayProfile: Delay(preferredProtocol: Protocol.USENET)));

		Assert.Equal(Protocol.USENET, result[0].Candidate.Release.Protocol);
	}

	[Fact]
	public void Decide_ShouldRejectPermanent_WhenIgnoredTermMatchesSubstring()
	{
		var decision = _instance.Decide(Candidate(Release()),
			Context(releaseProfiles: new[] { ReleaseProfile(ignored: new[] { "flux" }) }));

		Assert.False(decision.Approved);
		Assert.Contains(decision.Rejections,
			r => r.Reason == "matches ignored term 'flux'" && r.Type == RejectionType.PERMANENT);
	}

	[Fact]
	public void Decide_ShouldRejectPermanent_WhenIgnoredTermMatchesRegex()
	{
		var decision = _instance.Decide(Candidate(Release()),
			Context(releaseProfiles: new[] { ReleaseProfile(ignored: new[] { "/DDP5.1/" }) }));

		Assert.False(decision.Approved);
		Assert.Contains(decision.Rejections, r => r.Reason == "matches ignored term '/DDP5.1/'");
	}

	[Fact]
	public void Decide_ShouldRejectPermanent_WhenRequiredTermMissing()
	{
		var decision = _instance.Decide(Candidate(Release()),
			Context(releaseProfiles: new[] { ReleaseProfile(required: new[] { "WEB-DL", "REMUX" }) }));

		Assert.False(decision.Approved);
		Assert.Contains(decision.Rejections,
			r => r.Reason == "missing required term 'REMUX'" && r.Type == RejectionType.PERMANENT);
	}

	[Fact]
	public void Decide_ShouldApprove_WhenAllRequiredTermsPresent()
	{
		var decision = _instance.Decide(Candidate(Release()),
			Context(releaseProfiles: new[] { ReleaseProfile(required: new[] { "WEB-DL", "AMZN" }) }));

		Assert.True(decision.Approved);
	}

	[Fact]
	public void Decide_ShouldSkipReleaseProfile_WhenScopedToAnotherIndexer()
	{
		var decision = _instance.Decide(Candidate(Release(), indexerName: "MyIndexer"),
			Context(releaseProfiles: new[] { ReleaseProfile(ignored: new[] { "flux" }, indexer: "OtherIndexer") }));

		Assert.True(decision.Approved);
	}

	[Fact]
	public void Decide_ShouldRejectTemporary_WhenMinimumAvailabilityNotMet()
	{
		var decision = _instance.Decide(Candidate(Release()), Context(minimumAvailabilityMet: false));

		Assert.False(decision.Approved);
		var rejection = Assert.Single(decision.Rejections);
		Assert.Equal("minimum availability not met", rejection.Reason);
		Assert.Equal(RejectionType.TEMPORARY, rejection.Type);
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
		SeriesReleaseData? seriesData = null,
		Protocol protocol = Protocol.BITTORRENT)
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
			Protocol = protocol,
			ReleaseGroup = releaseGroup
		};

	private static ReleaseCandidate Candidate(BaseRelease release, string? indexerName = "MyIndexer",
		int indexerPriority = 25, int? seeders = null, int? minimumSeeders = null, DateTimeOffset? publishDate = null)
	{
		var info = Info;

		if (seeders is not null)
			info = info with { Seeders = seeders };

		if (publishDate is not null)
			info = info with { PublishDate = publishDate };

		return new ReleaseCandidate(release, info, indexerName, indexerPriority, minimumSeeders);
	}

	private static MediaContext Context(
		QualityProfile? profile = null,
		LanguageProfile? languageProfile = null,
		IReadOnlyCollection<ReleaseFilter>? filters = null,
		IReadOnlyCollection<CustomFormat>? customFormats = null,
		IReadOnlyDictionary<int, int>? customFormatScores = null,
		QualityModel? existing = null,
		IReadOnlyList<Language>? existingLanguages = null,
		string? seasonReleaseGroup = null,
		DelayProfile? delayProfile = null,
		IReadOnlyCollection<ReleaseProfile>? releaseProfiles = null,
		bool? minimumAvailabilityMet = null)
		=> new()
		{
			QualityProfile = profile ?? Profile(),
			LanguageProfile = languageProfile ?? Languages(Language.ENGLISH),
			Filters = filters ?? Array.Empty<ReleaseFilter>(),
			CustomFormats = customFormats ?? Array.Empty<CustomFormat>(),
			CustomFormatScores = customFormatScores ?? new Dictionary<int, int>(),
			ExistingFileQuality = existing,
			ExistingFileLanguages = existingLanguages,
			SeasonReleaseGroup = seasonReleaseGroup,
			DelayProfile = delayProfile,
			ReleaseProfiles = releaseProfiles ?? Array.Empty<ReleaseProfile>(),
			MinimumAvailabilityMet = minimumAvailabilityMet
		};

	private static DelayProfile Delay(int torrentDelayMinutes = 0, int usenetDelayMinutes = 0,
		bool bypassIfHighestQuality = false, Protocol preferredProtocol = Protocol.BITTORRENT)
		=> new()
		{
			Name = "Test",
			TorrentDelayMinutes = torrentDelayMinutes,
			UsenetDelayMinutes = usenetDelayMinutes,
			BypassIfHighestQuality = bypassIfHighestQuality,
			PreferredProtocol = preferredProtocol
		};

	private static ReleaseProfile ReleaseProfile(IReadOnlyList<string>? required = null,
		IReadOnlyList<string>? ignored = null, string? indexer = null)
		=> new()
		{
			Name = "Test",
			Required = required?.ToList() ?? new List<string>(),
			Ignored = ignored?.ToList() ?? new List<string>(),
			Indexer = indexer
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

	private static LanguageProfile LanguageProfile(Language cutoff, bool upgradeAllowed, params Language[] languages)
		=> new() { Name = "Test", Languages = languages.ToList(), Cutoff = cutoff, UpgradeAllowed = upgradeAllowed };

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
