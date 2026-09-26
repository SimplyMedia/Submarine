using Submarine.Core.CustomFormats;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Shouldly;
using Xunit;

namespace Submarine.Core.Tests.DecisionEngine;

public class DownloadDecisionMakerTest
{
	private readonly DownloadDecisionMaker _instance = new(new ReleaseFilterEvaluator());

	[Fact]
	public void Decide_ShouldReject_WhenQualityNotInProfile()
	{
		var decision = _instance.Decide(Candidate(Release(QualityResolution.R480_P)), Context());

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("quality"));
	}

	[Fact]
	public void Decide_ShouldReject_WhenNoWantedLanguage()
	{
		var decision = _instance.Decide(
			Candidate(Release(languages: [Language.GERMAN])),
			Context(languageProfile: Languages(Language.ENGLISH)));

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("language"));
	}

	[Fact]
	public void Decide_ShouldApprove_WhenCandidateIsQualityUpgradeOverExisting()
	{
		var decision = _instance.Decide(
			Candidate(Release(QualityResolution.R1080_P)),
			Context(existing: Quality(QualityResolution.R720_P)));

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldReject_WhenExistingMeetsCutoff()
	{
		var decision = _instance.Decide(
			Candidate(Release(QualityResolution.R2160_P)),
			Context(profile: Profile(cutoff: 1), existing: Quality(QualityResolution.R1080_P)));

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("cutoff"));
	}

	[Fact]
	public void Decide_ShouldReject_WhenCandidateIsSidegradeOfExisting()
	{
		var decision = _instance.Decide(
			Candidate(Release(QualityResolution.R1080_P)),
			Context(existing: Quality(QualityResolution.R1080_P)));

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("upgrade"));
	}

	[Fact]
	public void Decide_ShouldApprove_WhenCandidateIsRevisionUpgradeOverExisting()
	{
		var decision = _instance.Decide(
			Candidate(Release(revision: new Revision(2, IsProper: true))),
			Context(existing: Quality(QualityResolution.R1080_P)));

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldApprove_WhenCandidateIsProperAtTheQualityCutoff()
	{
		var decision = _instance.Decide(
			Candidate(Release(QualityResolution.R1080_P, revision: new Revision(2, IsProper: true))),
			Context(profile: Profile(cutoff: 1), existing: Quality(QualityResolution.R1080_P)));

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldNotUpgradeToProper_WhenPropersAreNotUpgraded()
	{
		var decision = _instance.Decide(
			Candidate(Release(revision: new Revision(1, IsProper: true))),
			Context(existing: Quality(QualityResolution.R1080_P),
				downloadPropersAndRepacks: DownloadPropersAndRepacks.DO_NOT_UPGRADE));

		decision.Approved.ShouldBeFalse();
	}

	[Fact]
	public void Decide_ShouldReject_WhenExistingMeetsLanguageCutoffAndCandidateAddsNoImprovement()
	{
		var decision = _instance.Decide(
			Candidate(Release(languages: [Language.ENGLISH])),
			Context(languageProfile: LanguageProfile(Language.ENGLISH, upgradeAllowed: true, Language.ENGLISH, Language.GERMAN),
				existingLanguages: [Language.ENGLISH]));

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("language cutoff"));
	}

	[Fact]
	public void Decide_ShouldApprove_WhenCandidateAddsABetterRankedLanguage()
	{
		var decision = _instance.Decide(
			Candidate(Release(languages: [Language.ENGLISH])),
			Context(languageProfile: LanguageProfile(Language.ENGLISH, upgradeAllowed: true, Language.ENGLISH, Language.GERMAN),
				existingLanguages: [Language.GERMAN]));

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldReject_WhenBlockFilterMatches()
	{
		var decision = _instance.Decide(
			Candidate(Release(releaseGroup: "EVO")),
			Context(filters: [BlockFilter(ReleaseFilterField.RELEASE_GROUP, "EVO")]));

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("blocked"));
	}

	[Fact]
	public void DecideAll_ShouldOrderByTier_WhenPreferFiltersDiffer()
	{
		var result = _instance.DecideAll(
		[
			Candidate(Release(releaseGroup: "TIER2")),
			Candidate(Release(releaseGroup: "TIER1"))
		], Context(filters:
		[
			PreferFilter(ReleaseFilterField.RELEASE_GROUP, "TIER1", 1),
			PreferFilter(ReleaseFilterField.RELEASE_GROUP, "TIER2", 2)
		]));

		result[0].Candidate.Parsed.ReleaseGroup.ShouldBe("TIER1");
		result[1].Candidate.Parsed.ReleaseGroup.ShouldBe("TIER2");
	}

	[Fact]
	public void DecideAll_ShouldRankByCustomFormatScore_WhenFormatsMatch()
	{
		var format = new CustomFormat
		{
			Id = 1,
			Name = "Good Group",
			Specifications =
			[
				new CustomFormatSpecification("Good Group", CustomFormatSpecificationType.RELEASE_GROUP, false, true,
					Json("GOOD"))
			]
		};

		var result = _instance.DecideAll(
		[
			Candidate(Release(releaseGroup: "PLAIN")),
			Candidate(Release(releaseGroup: "GOOD"))
		], Context(customFormats: [format], formatItems: [new ProfileFormatItem(1, 500)]));

		result[0].Candidate.Parsed.ReleaseGroup.ShouldBe("GOOD");
		result[0].CustomFormats.ShouldContain(format);
	}

	[Fact]
	public void DecideAll_ShouldPreferConsistentReleaseGroup_WhenSeasonGroupMatches()
	{
		var consistent = Candidate(Release(releaseGroup: "SAME"), indexerPriority: 100);
		var other = Candidate(Release(releaseGroup: "OTHER"));

		var result = _instance.DecideAll([other, consistent], Context(seasonReleaseGroup: "SAME"));

		result[0].Candidate.Parsed.ReleaseGroup.ShouldBe("SAME");
	}

	[Fact]
	public void DecideAll_ShouldPreferFullSeason_WhenComparableToEpisode()
	{
		var season = Candidate(Release(seriesData: Series(SeriesReleaseType.FULL_SEASON)));
		var episode = Candidate(Release(seriesData: Series(SeriesReleaseType.EPISODE)));

		var result = _instance.DecideAll([episode, season], Context());

		result[0].Candidate.Parsed.SeriesReleaseData!.ReleaseType.ShouldBe(SeriesReleaseType.FULL_SEASON);
	}

	[Fact]
	public void Decide_ShouldRejectTemporary_WhenSeedersBelowMinimum()
	{
		var decision = _instance.Decide(Candidate(Release(), seeders: 1, minimumSeeders: 3), Context());

		decision.Approved.ShouldBeFalse();
		var rejection = decision.Rejections.ShouldHaveSingleItem();
		rejection.Reason.ShouldBe("1 seeders, minimum is 3");
		rejection.Type.ShouldBe(RejectionType.TEMPORARY);
	}

	[Fact]
	public void Decide_ShouldApprove_WhenSeedersMeetMinimum()
	{
		var decision = _instance.Decide(Candidate(Release(), seeders: 3, minimumSeeders: 3), Context());

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldApprove_WhenSeedersUnknown()
	{
		var decision = _instance.Decide(Candidate(Release(), minimumSeeders: 3), Context());

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldIgnoreMinimumSeeders_WhenReleaseIsUsenet()
	{
		var decision = _instance.Decide(
			Candidate(Release(protocol: Protocol.USENET), seeders: 1, minimumSeeders: 3), Context());

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldRejectTemporary_WhenWithinDelayWindow()
	{
		var now = DateTime.UtcNow;

		var decision = _instance.Decide(
			Candidate(Release(), publishDate: now),
			Context(delayProfile: Delay(torrentDelayMinutes: 60)), now);

		decision.Approved.ShouldBeFalse();
		var rejection = decision.Rejections.ShouldHaveSingleItem();
		rejection.Reason.ShouldBe("waiting for delay window");
		rejection.Type.ShouldBe(RejectionType.TEMPORARY);
	}

	[Fact]
	public void Decide_ShouldApprove_WhenDelayWindowElapsed()
	{
		var now = DateTime.UtcNow;

		var decision = _instance.Decide(
			Candidate(Release(), publishDate: now.AddMinutes(-120)),
			Context(delayProfile: Delay(torrentDelayMinutes: 60)), now);

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldBypassDelay_WhenBypassEnabledAndCandidateAtHighestAllowedQuality()
	{
		var now = DateTime.UtcNow;

		var decision = _instance.Decide(
			Candidate(Release(QualityResolution.R2160_P), publishDate: now),
			Context(delayProfile: Delay(torrentDelayMinutes: 60, bypassIfHighestQuality: true)), now);

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldNotBypassDelay_WhenCandidateIsBelowTheHighestAllowedQuality()
	{
		var now = DateTime.UtcNow;

		var decision = _instance.Decide(
			Candidate(Release(QualityResolution.R1080_P), publishDate: now),
			Context(delayProfile: Delay(torrentDelayMinutes: 60, bypassIfHighestQuality: true)), now);

		decision.Approved.ShouldBeFalse();
	}

	[Fact]
	public void Decide_ShouldBypassDelay_WhenCustomFormatScoreIsAboveTheThreshold()
	{
		var now = DateTime.UtcNow;
		var format = new CustomFormat
		{
			Id = 1,
			Name = "Good Group",
			Specifications =
			[
				new CustomFormatSpecification("Good Group", CustomFormatSpecificationType.RELEASE_GROUP, false, true, Json("FLUX"))
			]
		};

		var decision = _instance.Decide(
			Candidate(Release(), publishDate: now),
			Context(
				customFormats: [format],
				formatItems: [new ProfileFormatItem(1, 100)],
				delayProfile: Delay(torrentDelayMinutes: 60, bypassIfAboveCustomFormatScore: true, minimumCustomFormatScore: 50)),
			now);

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldNotBypassDelay_WhenCustomFormatScoreIsBelowTheThreshold()
	{
		var now = DateTime.UtcNow;

		var decision = _instance.Decide(
			Candidate(Release(), publishDate: now),
			Context(delayProfile: Delay(torrentDelayMinutes: 60, bypassIfAboveCustomFormatScore: true, minimumCustomFormatScore: 50)),
			now);

		decision.Approved.ShouldBeFalse();
	}

	[Fact]
	public void DecideAll_ShouldPreferPreferredProtocol_WhenScoresOtherwiseEqual()
	{
		var torrent = Candidate(Release(protocol: Protocol.BITTORRENT));
		var usenet = Candidate(Release(protocol: Protocol.USENET));

		var result = _instance.DecideAll([torrent, usenet],
			Context(delayProfile: Delay(preferredProtocol: Protocol.USENET)));

		result[0].Candidate.Parsed.Protocol.ShouldBe(Protocol.USENET);
	}

	[Fact]
	public void Decide_ShouldRejectPermanent_WhenIgnoredTermMatchesSubstring()
	{
		var decision = _instance.Decide(Candidate(Release()),
			Context(releaseProfiles: [ReleaseProfile(ignored: ["flux"])]));

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection =>
			rejection.Reason == "matches ignored term 'flux'" && rejection.Type == RejectionType.PERMANENT);
	}

	[Fact]
	public void Decide_ShouldRejectPermanent_WhenIgnoredTermMatchesRegex()
	{
		var decision = _instance.Decide(Candidate(Release()),
			Context(releaseProfiles: [ReleaseProfile(ignored: ["/DDP5.1/"])]));

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason == "matches ignored term '/DDP5.1/'");
	}

	[Fact]
	public void Decide_ShouldRejectPermanent_WhenRequiredTermMissing()
	{
		var decision = _instance.Decide(Candidate(Release()),
			Context(releaseProfiles: [ReleaseProfile(required: ["WEB-DL", "REMUX"])]));

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection =>
			rejection.Reason == "missing required term 'REMUX'" && rejection.Type == RejectionType.PERMANENT);
	}

	[Fact]
	public void Decide_ShouldApprove_WhenAllRequiredTermsPresent()
	{
		var decision = _instance.Decide(Candidate(Release()),
			Context(releaseProfiles: [ReleaseProfile(required: ["WEB-DL", "AMZN"])]));

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldSkipReleaseProfile_WhenScopedToAnotherIndexer()
	{
		var decision = _instance.Decide(Candidate(Release()),
			Context(releaseProfiles: [ReleaseProfile(ignored: ["flux"], indexerId: 99)]));

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldSkipReleaseProfile_WhenProfileIsDisabled()
	{
		var decision = _instance.Decide(Candidate(Release()),
			Context(releaseProfiles: [ReleaseProfile(ignored: ["flux"], enabled: false)]));

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldRejectTemporary_WhenMinimumAvailabilityNotMet()
	{
		var decision = _instance.Decide(Candidate(Release()), Context(minimumAvailabilityMet: false));

		decision.Approved.ShouldBeFalse();
		var rejection = decision.Rejections.ShouldHaveSingleItem();
		rejection.Reason.ShouldBe("minimum availability not met");
		rejection.Type.ShouldBe(RejectionType.TEMPORARY);
	}

	[Fact]
	public void DecideAll_ShouldOrderApprovedFirst_WhenSomeRejected()
	{
		var result = _instance.DecideAll(
		[
			Candidate(Release(languages: [Language.GERMAN])),
			Candidate(Release(languages: [Language.ENGLISH]))
		], Context(languageProfile: Languages(Language.ENGLISH)));

		result[0].Approved.ShouldBeTrue();
		result[1].Approved.ShouldBeFalse();
	}

	[Fact]
	public void Decide_ShouldReject_WhenCustomFormatScoreIsBelowMinimum()
	{
		var decision = _instance.Decide(Candidate(Release()), Context(minFormatScore: 100));

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("custom format score"));
	}

	[Fact]
	public void Decide_ShouldApprove_WhenCustomFormatScoreMeetsMinimum()
	{
		var format = new CustomFormat
		{
			Id = 1,
			Name = "Good Group",
			Specifications =
			[
				new CustomFormatSpecification("Good Group", CustomFormatSpecificationType.RELEASE_GROUP, false, true, Json("FLUX"))
			]
		};

		var decision = _instance.Decide(
			Candidate(Release()),
			Context(customFormats: [format], formatItems: [new ProfileFormatItem(1, 100)], minFormatScore: 100));

		decision.Approved.ShouldBeTrue();
		decision.CustomFormatScore.ShouldBe(100);
	}

	[Fact]
	public void Decide_ShouldReject_WhenSizeIsOutsideTheQualityDefinition()
	{
		var definition = new QualityDefinition
		{
			Source = QualitySource.WEB_DL,
			Resolution = QualityResolution.R1080_P,
			Title = "WebDL-1080p",
			MaxSizeMbPerMinute = 1
		};

		var decision = _instance.Decide(
			Candidate(Release(), size: 50L * 1024 * 1024 * 1024),
			Context(qualityDefinitions: [definition], runtimeMinutes: 60));

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("outside the limits"));
	}

	[Fact]
	public void Decide_ShouldApprove_WhenSizeIsInsideTheQualityDefinition()
	{
		var definition = new QualityDefinition
		{
			Source = QualitySource.WEB_DL,
			Resolution = QualityResolution.R1080_P,
			Title = "WebDL-1080p",
			MaxSizeMbPerMinute = 100
		};

		var decision = _instance.Decide(
			Candidate(Release(), size: 4L * 1024 * 1024 * 1024),
			Context(qualityDefinitions: [definition], runtimeMinutes: 60));

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldReject_WhenReleaseIsBlocklisted()
	{
		var decision = _instance.Decide(
			Candidate(Release()),
			Context(isBlocklisted: candidate => candidate.Info.Guid == "guid"));

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason == "release is blocklisted");
	}

	[Fact]
	public void Decide_ShouldRejectPermanent_WhenReleaseIsOlderThanRetention()
	{
		var now = DateTime.UtcNow;
		var config = new IndexerConfig { RetentionDays = 100 };

		var decision = _instance.Decide(
			Candidate(Release(protocol: Protocol.USENET), publishDate: now.AddDays(-200)),
			Context(indexerConfig: config), now);

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection =>
			rejection.Reason.Contains("retention") && rejection.Type == RejectionType.PERMANENT);
	}

	[Fact]
	public void Decide_ShouldApprove_WhenReleaseIsWithinRetention()
	{
		var now = DateTime.UtcNow;
		var config = new IndexerConfig { RetentionDays = 100 };

		var decision = _instance.Decide(
			Candidate(Release(protocol: Protocol.USENET), publishDate: now.AddDays(-10)),
			Context(indexerConfig: config), now);

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldRejectTemporary_WhenReleaseIsBelowMinimumAge()
	{
		var now = DateTime.UtcNow;
		var config = new IndexerConfig { MinimumAgeMinutes = 30 };

		var decision = _instance.Decide(
			Candidate(Release(protocol: Protocol.USENET), publishDate: now.AddMinutes(-5)),
			Context(indexerConfig: config), now);

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection =>
			rejection.Reason.Contains("minimum age") && rejection.Type == RejectionType.TEMPORARY);
	}

	[Fact]
	public void Decide_ShouldReject_WhenSizeExceedsTheMaximumSize()
	{
		var config = new IndexerConfig { MaximumSizeMb = 100 };

		var decision = _instance.Decide(
			Candidate(Release(), size: 200L * 1024 * 1024),
			Context(indexerConfig: config));

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("maximum"));
	}

	[Fact]
	public void Decide_ShouldReject_WhenQualityIsRawDiskAndTheProfileDoesNotAllowIt()
	{
		var release = Release();
		release = release with
		{
			Quality = new QualityModel(
				new QualityResolutionModel(QualitySource.BLURAY_DISK, QualityResolution.R1080_P),
				new Revision())
		};

		var decision = _instance.Decide(Candidate(release), Context());

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("quality"));
	}

	[Fact]
	public void Decide_ShouldApprove_WhenQualityIsRawDiskAndTheProfileAllowsIt()
	{
		var profile = Profile(cutoff: 2, upgradeAllowed: true);
		profile.Items.Add(new QualityProfileItem(
			new QualityResolutionModel(QualitySource.BLURAY_DISK, QualityResolution.R1080_P), true));

		var release = Release() with
		{
			Quality = new QualityModel(
				new QualityResolutionModel(QualitySource.BLURAY_DISK, QualityResolution.R1080_P),
				new Revision())
		};

		var decision = _instance.Decide(Candidate(release), Context(profile: profile));

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldApprove_WhenSameTierBeatsTheExistingCustomFormatScore()
	{
		var format = new CustomFormat
		{
			Id = 1,
			Name = "Good Group",
			Specifications =
			[
				new CustomFormatSpecification("Good Group", CustomFormatSpecificationType.RELEASE_GROUP, false, true, Json("FLUX"))
			]
		};

		var decision = _instance.Decide(
			Candidate(Release()),
			Context(
				customFormats: [format],
				formatItems: [new ProfileFormatItem(1, 150)],
				existing: Quality(QualityResolution.R1080_P),
				existingScore: 50,
				cutoffFormatScore: 100));

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldComputeTheDocumentedScore()
	{
		// quality index 1 * 1000 + revision 1 * 10 - indexer priority 25
		var decision = _instance.Decide(Candidate(Release()), Context());

		decision.Score.ShouldBe(985);
	}

	[Fact]
	public void Decide_ShouldAddTheFullSeasonAndProtocolBonuses()
	{
		var decision = _instance.Decide(
			Candidate(Release(seriesData: Series(SeriesReleaseType.FULL_SEASON))),
			Context(delayProfile: Delay(preferredProtocol: Protocol.BITTORRENT)));

		// 1000 + 10 + 50 (full season) + 25 (preferred protocol) - 25 (priority)
		decision.Score.ShouldBe(1060);
	}

	[Fact]
	public void DecideAll_ShouldPreferMoreSeeders_WhenScoresAreEqual()
	{
		var fewSeeders = Candidate(Release(), seeders: 1);
		var manySeeders = Candidate(Release(), seeders: 99);

		var result = _instance.DecideAll([fewSeeders, manySeeders], Context());

		result[0].Candidate.Info.Seeders.ShouldBe(99);
	}

	[Fact]
	public void Decide_ShouldRejectSampleReleases_WhenSizeIsSmall()
	{
		var release = Release() with { FullTitle = "Series.Title.S01E01.sample.1080p.AMZN.WEB-DL.DDP5.1.H.264-FLUX" };
		var decision = _instance.Decide(Candidate(release, size: 10L * 1024 * 1024), Context());

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("sample"));
	}

	[Fact]
	public void Decide_ShouldAcceptSampleTitledRelease_WhenSizeIsUnknown()
	{
		// unlike Sonarr/Radarr's non-nullable release size (which defaults to zero and so always "< 70MB"), an
		// unreported Submarine release size is not assumed to be a sample
		var release = Release() with { FullTitle = "Series.Title.S01E01.sample.1080p.AMZN.WEB-DL.DDP5.1.H.264-FLUX" };
		var decision = _instance.Decide(Candidate(release), Context());

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldRejectMultiSeasonReleases()
	{
		var decision = _instance.Decide(
			Candidate(Release(seriesData: Series(SeriesReleaseType.MULTI_SEASON))),
			Context());

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("multi-season"));
	}

	[Fact]
	public void Decide_ShouldRejectWhenExistingFileCoversMoreEpisodes()
	{
		var decision = _instance.Decide(Candidate(Release()), Context() with { ExistingFileCoversMoreEpisodes = true });

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("more episodes"));
	}

	[Fact]
	public void Decide_ShouldRejectRepack_WhenReleaseGroupDoesNotMatchTheExistingFile()
	{
		var release = Release(QualityResolution.R1080_P, revision: new Revision(2, IsRepack: true), releaseGroup: "OTHER");
		var decision = _instance.Decide(
			Candidate(release),
			Context(existing: Quality(QualityResolution.R1080_P), seasonReleaseGroup: "FLUX"));

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("repack/version release group"));
	}

	[Fact]
	public void Decide_ShouldAcceptRepack_WhenReleaseGroupMatchesTheExistingFile()
	{
		var release = Release(QualityResolution.R1080_P, revision: new Revision(2, IsRepack: true), releaseGroup: "FLUX");
		var decision = _instance.Decide(
			Candidate(release),
			Context(existing: Quality(QualityResolution.R1080_P), seasonReleaseGroup: "FLUX"));

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldRejectAnimeVersionUpgrade_WhenReleaseGroupDoesNotMatch()
	{
		// anime version bumps are not marked IsRepack, unlike a Sonarr/Radarr repack, so the group check must also
		// cover a plain revision bump when the series is anime
		var release = Release(QualityResolution.R1080_P, revision: new Revision(2), releaseGroup: "OTHER");
		var context = Context(existing: Quality(QualityResolution.R1080_P), seasonReleaseGroup: "FLUX")
			with { SeriesType = SeriesType.ANIME };

		var decision = _instance.Decide(Candidate(release), context);

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("repack/version release group"));
	}

	[Fact]
	public void Decide_ShouldRejectWhenNotEnoughFreeSpace()
	{
		var context = Context() with { AvailableFreeSpaceBytes = 50L * 1024 * 1024, MinimumFreeSpaceMb = 100 };
		var decision = _instance.Decide(Candidate(Release(), size: 10L * 1024 * 1024), context);

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("free space"));
	}

	[Fact]
	public void Decide_ShouldAcceptWhenEnoughFreeSpace()
	{
		var context = Context() with { AvailableFreeSpaceBytes = 10L * 1024 * 1024 * 1024, MinimumFreeSpaceMb = 100 };
		var decision = _instance.Decide(Candidate(Release(), size: 1L * 1024 * 1024 * 1024), context);

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldRejectAlreadyImportedTitle()
	{
		var release = Release();
		var context = Context() with { AlreadyImportedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { release.FullTitle } };

		var decision = _instance.Decide(Candidate(release), context);

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("already grabbed and imported"));
	}

	[Fact]
	public void Decide_ShouldRejectAlreadyImportedInfoHash()
	{
		var candidate = Candidate(Release());
		candidate = candidate with { Info = candidate.Info with { InfoHash = "ABC123" } };
		var context = Context() with { AlreadyImportedInfoHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ABC123" } };

		var decision = _instance.Decide(candidate, context);

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("same info hash"));
	}

	[Fact]
	public void Decide_ShouldRejectWhenIndexerRequiredFlagsAreMissing()
	{
		var candidate = Candidate(Release());
		candidate = candidate with { Info = candidate.Info with { IndexerId = 5, IndexerFlags = [] } };
		var context = Context() with
		{
			IndexerRequiredFlags = new Dictionary<int, IReadOnlyList<IndexerFlag>> { [5] = [IndexerFlag.FREELEECH] }
		};

		var decision = _instance.Decide(candidate, context);

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("required indexer flags"));
	}

	[Fact]
	public void Decide_ShouldAcceptWhenIndexerRequiredFlagIsPresent()
	{
		var candidate = Candidate(Release());
		candidate = candidate with { Info = candidate.Info with { IndexerId = 5, IndexerFlags = [IndexerFlag.FREELEECH] } };
		var context = Context() with
		{
			IndexerRequiredFlags = new Dictionary<int, IReadOnlyList<IndexerFlag>> { [5] = [IndexerFlag.FREELEECH] }
		};

		var decision = _instance.Decide(candidate, context);

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldRejectWhenProtocolIsDisabledByTheDelayProfile()
	{
		var delay = Delay();
		delay.EnableTorrent = false;

		var decision = _instance.Decide(
			Candidate(Release(protocol: Protocol.BITTORRENT)),
			Context(delayProfile: delay));

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("not enabled"));
	}

	[Fact]
	public void Decide_ShouldRejectHardcodedSubs_WhenNotAllowedAndReleaseGroupIsNotWhitelisted()
	{
		var release = Release(releaseGroup: "FLUX") with { HardcodedSubs = true };
		var config = new IndexerConfig { AllowHardcodedSubs = false, WhitelistedHardcodedSubs = "OTHERGROUP" };

		var decision = _instance.Decide(Candidate(release), Context(indexerConfig: config));

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("hardcoded subtitles"));
	}

	[Fact]
	public void Decide_ShouldAcceptHardcodedSubs_WhenReleaseGroupIsWhitelisted()
	{
		var release = Release(releaseGroup: "FLUX") with { HardcodedSubs = true };
		var config = new IndexerConfig { AllowHardcodedSubs = false, WhitelistedHardcodedSubs = "FLUX,OTHER" };

		var decision = _instance.Decide(Candidate(release), Context(indexerConfig: config));

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldPreferReleaseClosestToThePreferredSize_WhenOtherwiseTied()
	{
		var definition = new QualityDefinition
		{
			Source = QualitySource.WEB_DL, Resolution = QualityResolution.R1080_P, PreferredSizeMbPerMinute = 10
		};

		// both releases tie on score, seeders and age; the one closest to 10 MB/min * 60 min = 600 MB wins
		var close = Candidate(Release(), size: 600L * 1024 * 1024);
		var far = Candidate(Release(), size: 2000L * 1024 * 1024);
		var context = Context(qualityDefinitions: [definition], runtimeMinutes: 60);

		var result = _instance.DecideAll([far, close], context);

		result[0].Candidate.Info.Size.ShouldBe(600L * 1024 * 1024);
	}

	[Fact]
	public void Decide_ShouldRejectUnmonitoredMedia()
	{
		var decision = _instance.Decide(Candidate(Release()), Context() with { MediaMonitored = false });

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("not monitored"));
	}

	[Fact]
	public void Decide_ShouldRejectWhenNoRequestedEpisodesAreMonitored()
	{
		var context = Context() with { EpisodeCount = 2, MonitoredEpisodeCount = 0 };
		var decision = _instance.Decide(Candidate(Release()), context);

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("no episodes"));
	}

	[Fact]
	public void Decide_ShouldBypassMonitoredAndAvailabilityChecks_ForInteractiveSearch()
	{
		var context = Context(minimumAvailabilityMet: false) with { MediaMonitored = false, IsInteractive = true };
		var decision = _instance.Decide(Candidate(Release()), context);

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldRejectWhenIndexerTagsDoNotMatchTheMediaTags()
	{
		var candidate = Candidate(Release());
		candidate = candidate with { Info = candidate.Info with { IndexerId = 7 } };
		var context = Context() with
		{
			MediaTagIds = [1],
			IndexerTagIds = new Dictionary<int, IReadOnlyList<int>> { [7] = [2] }
		};

		var decision = _instance.Decide(candidate, context);

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("indexer tags"));
	}

	[Fact]
	public void Decide_ShouldAcceptWhenIndexerTagsIntersectTheMediaTags()
	{
		var candidate = Candidate(Release());
		candidate = candidate with { Info = candidate.Info with { IndexerId = 7 } };
		var context = Context() with
		{
			MediaTagIds = [1, 2],
			IndexerTagIds = new Dictionary<int, IReadOnlyList<int>> { [7] = [2, 3] }
		};

		var decision = _instance.Decide(candidate, context);

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldRejectWhenARecentGrabAlreadyMeetsOrExceedsTheRelease()
	{
		var context = Context() with { RecentGrabQuality = Quality(QualityResolution.R2160_P) };
		var decision = _instance.Decide(Candidate(Release(QualityResolution.R1080_P)), context);

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("recent grab"));
	}

	[Fact]
	public void Decide_ShouldAcceptWhenTheReleaseUpgradesTheRecentGrab()
	{
		var context = Context() with { RecentGrabQuality = Quality(QualityResolution.R720_P) };
		var decision = _instance.Decide(Candidate(Release(QualityResolution.R2160_P)), context);

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldBypassRecentGrabCheck_ForInteractiveSearch()
	{
		var context = Context() with { RecentGrabQuality = Quality(QualityResolution.R2160_P), IsInteractive = true };
		var decision = _instance.Decide(Candidate(Release(QualityResolution.R1080_P)), context);

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldHoldRelease_WhenTheQueueAlreadyHasOneThatItDoesNotUpgrade()
	{
		var context = Context() with { QueuedReleases = [new QueuedRelease([1], null, Quality(QualityResolution.R2160_P), 0)] };
		var decision = _instance.Decide(Candidate(Release(QualityResolution.R1080_P)) with { EpisodeIds = [1] }, context);

		decision.Approved.ShouldBeFalse();
	}

	[Fact]
	public void Decide_ShouldIgnoreTheQueue_ForInteractiveSearch()
	{
		var context = Context() with { QueuedReleases = [new QueuedRelease([1], null, Quality(QualityResolution.R2160_P), 0)], IsInteractive = true };
		var decision = _instance.Decide(Candidate(Release(QualityResolution.R1080_P)) with { EpisodeIds = [1] }, context);

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldRejectSingleEpisodeInSeasonSearch_WhenSeasonIsOlderThanTheIndexerThreshold()
	{
		var candidate = Candidate(Release());
		candidate = candidate with { Info = candidate.Info with { IndexerId = 3 } };
		var context = Context() with
		{
			IsSeasonSearch = true,
			SeriesType = SeriesType.STANDARD,
			EpisodeCount = 1,
			DaysSinceSeasonLastAired = 30,
			IndexerSeasonSearchMaxAge = new Dictionary<int, int> { [3] = 14 }
		};

		var decision = _instance.Decide(candidate, context);

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("season pack required"));
	}

	[Fact]
	public void Decide_ShouldAcceptSingleEpisodeInSeasonSearch_WhenSeasonIsRecent()
	{
		var candidate = Candidate(Release());
		candidate = candidate with { Info = candidate.Info with { IndexerId = 3 } };
		var context = Context() with
		{
			IsSeasonSearch = true,
			SeriesType = SeriesType.STANDARD,
			EpisodeCount = 1,
			DaysSinceSeasonLastAired = 5,
			IndexerSeasonSearchMaxAge = new Dictionary<int, int> { [3] = 14 }
		};

		var decision = _instance.Decide(candidate, context);

		decision.Approved.ShouldBeTrue();
	}

	[Fact]
	public void Decide_ShouldRejectProperForAFileOlderThanSevenDays()
	{
		var release = Release(QualityResolution.R1080_P, revision: new Revision(1, IsProper: true), releaseGroup: "FLUX");
		var context = Context(existing: Quality(QualityResolution.R1080_P), seasonReleaseGroup: "FLUX")
			with { ExistingFileAddedDate = DateTime.UtcNow.AddDays(-30) };

		var decision = _instance.Decide(Candidate(release), context);

		decision.Approved.ShouldBeFalse();
		decision.Rejections.ShouldContain(rejection => rejection.Reason.Contains("older than 7 days"));
	}

	[Fact]
	public void Decide_ShouldAcceptProperForARecentFile()
	{
		var release = Release(QualityResolution.R1080_P, revision: new Revision(1, IsProper: true), releaseGroup: "FLUX");
		var context = Context(existing: Quality(QualityResolution.R1080_P), seasonReleaseGroup: "FLUX")
			with { ExistingFileAddedDate = DateTime.UtcNow.AddDays(-1) };

		var decision = _instance.Decide(Candidate(release), context);

		decision.Approved.ShouldBeTrue();
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
			Languages = languages ?? [Language.ENGLISH],
			StreamingProvider = source,
			Type = ReleaseType.SERIES,
			SeriesReleaseData = seriesData,
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, resolution),
				revision ?? new Revision()),
			Protocol = protocol,
			ReleaseGroup = releaseGroup
		};

	private static ReleaseCandidate Candidate(
		BaseRelease release,
		int indexerPriority = 25,
		int? seeders = null,
		int? minimumSeeders = null,
		DateTime? publishDate = null,
		long? size = null)
	{
		var info = new ReleaseInfo
		{
			Title = "Series.Title.S01",
			Guid = "guid",
			Protocol = release.Protocol,
			Indexer = "MyIndexer",
			IndexerPriority = indexerPriority,
			Size = size
		};

		if (seeders is not null)
		{
			info = info with { Seeders = seeders };
		}

		if (publishDate is not null)
		{
			info = info with { PublishDate = publishDate };
		}

		return new ReleaseCandidate(release, info, minimumSeeders);
	}

	private static DecisionContext Context(
		QualityProfile? profile = null,
		LanguageProfile? languageProfile = null,
		IReadOnlyCollection<ReleaseFilter>? filters = null,
		IReadOnlyCollection<CustomFormat>? customFormats = null,
		IReadOnlyList<ProfileFormatItem>? formatItems = null,
		QualityModel? existing = null,
		IReadOnlyList<Language>? existingLanguages = null,
		int existingScore = 0,
		string? seasonReleaseGroup = null,
		DelayProfile? delayProfile = null,
		IReadOnlyCollection<ReleaseProfile>? releaseProfiles = null,
		bool? minimumAvailabilityMet = null,
		int minFormatScore = 0,
		int cutoffFormatScore = 0,
		IReadOnlyCollection<QualityDefinition>? qualityDefinitions = null,
		int? runtimeMinutes = null,
		IndexerConfig? indexerConfig = null,
		DownloadPropersAndRepacks downloadPropersAndRepacks = DownloadPropersAndRepacks.PREFER_AND_UPGRADE,
		Func<ReleaseCandidate, bool>? isBlocklisted = null)
	{
		var qualityProfile = profile ?? Profile();
		qualityProfile.FormatItems = [.. (formatItems ?? [])];
		qualityProfile.MinFormatScore = minFormatScore;
		qualityProfile.CutoffFormatScore = cutoffFormatScore;

		return new DecisionContext
		{
			QualityProfile = qualityProfile,
			LanguageProfile = languageProfile ?? Languages(Language.ENGLISH),
			ReleaseFilters = filters ?? [],
			CustomFormats = customFormats ?? [],
			ExistingFileQuality = existing,
			ExistingFileLanguages = existingLanguages,
			ExistingCustomFormatScore = existingScore,
			SeasonReleaseGroup = seasonReleaseGroup,
			DelayProfile = delayProfile,
			ReleaseProfiles = releaseProfiles ?? [],
			MinimumAvailabilityMet = minimumAvailabilityMet,
			QualityDefinitions = qualityDefinitions ?? [],
			RuntimeMinutes = runtimeMinutes,
			IndexerConfig = indexerConfig,
			DownloadPropersAndRepacks = downloadPropersAndRepacks,
			IsBlocklisted = isBlocklisted
		};
	}

	private static ReleaseFilter BlockFilter(ReleaseFilterField field, string value)
		=> new()
		{
			Id = 1,
			Field = field,
			Values = [value],
			Mode = ReleaseFilterMode.BLOCK,
			Tier = 0
		};

	private static ReleaseFilter PreferFilter(ReleaseFilterField field, string value, int tier)
		=> new()
		{
			Id = 1,
			Field = field,
			Values = [value],
			Mode = ReleaseFilterMode.PREFER,
			Tier = tier
		};

	private static DelayProfile Delay(int torrentDelayMinutes = 0, int usenetDelayMinutes = 0,
		bool bypassIfHighestQuality = false, bool bypassIfAboveCustomFormatScore = false,
		int minimumCustomFormatScore = 0, Protocol preferredProtocol = Protocol.BITTORRENT)
		=> new()
		{
			Name = "Test",
			TorrentDelayMinutes = torrentDelayMinutes,
			UsenetDelayMinutes = usenetDelayMinutes,
			BypassIfHighestQuality = bypassIfHighestQuality,
			BypassIfAboveCustomFormatScore = bypassIfAboveCustomFormatScore,
			MinimumCustomFormatScore = minimumCustomFormatScore,
			PreferredProtocol = preferredProtocol
		};

	private static ReleaseProfile ReleaseProfile(
		IReadOnlyList<string>? required = null,
		IReadOnlyList<string>? ignored = null,
		int? indexerId = null,
		bool enabled = true)
		=> new()
		{
			Name = "Test",
			Enabled = enabled,
			Required = required?.ToList() ?? [],
			Ignored = ignored?.ToList() ?? [],
			IndexerId = indexerId
		};

	private static QualityProfile Profile(int cutoff = 2, bool upgradeAllowed = true)
		=> new()
		{
			Name = "Test",
			UpgradeAllowed = upgradeAllowed,
			Cutoff = cutoff,
			Items =
			[
				Item(QualityResolution.R720_P),
				Item(QualityResolution.R1080_P),
				Item(QualityResolution.R2160_P)
			]
		};

	private static QualityProfileItem Item(QualityResolution resolution)
		=> new(new QualityResolutionModel(QualitySource.WEB_DL, resolution), true);

	private static LanguageProfile Languages(params Language[] languages)
		=> new() { Name = "Test", Languages = [.. languages] };

	private static LanguageProfile LanguageProfile(Language cutoff, bool upgradeAllowed, params Language[] languages)
		=> new() { Name = "Test", Languages = [.. languages], Cutoff = cutoff, UpgradeAllowed = upgradeAllowed };

	private static QualityModel Quality(QualityResolution resolution)
		=> new(new QualityResolutionModel(QualitySource.WEB_DL, resolution), new Revision());

	private static SeriesReleaseData Series(SeriesReleaseType type)
		=> new()
		{
			ReleaseType = type,
			Seasons = [1],
			Episodes = [],
			AbsoluteEpisodes = []
		};

	private static System.Text.Json.JsonElement Json(string value)
		=> System.Text.Json.JsonSerializer.SerializeToElement(value);
}
