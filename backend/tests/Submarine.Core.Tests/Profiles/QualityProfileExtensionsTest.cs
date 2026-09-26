using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Profiles;
using Submarine.Core.Quality;
using Shouldly;
using Xunit;

namespace Submarine.Core.Tests.Profiles;

public class QualityProfileExtensionsTest
{
	private static readonly QualityResolutionModel WebDl480P = new(QualitySource.WEB_DL, QualityResolution.R480_P);
	private static readonly QualityResolutionModel WebDl720P = new(QualitySource.WEB_DL, QualityResolution.R720_P);
	private static readonly QualityResolutionModel WebDl1080P = new(QualitySource.WEB_DL, QualityResolution.R1080_P);
	private static readonly QualityResolutionModel Bluray1080P = new(QualitySource.BLURAY, QualityResolution.R1080_P);

	[Fact]
	public void IsUpgrade_ShouldReturnTrue_WhenCandidateIsHigherTierAndAllowed()
	{
		var profile = CreateProfile(cutoff: 1, upgradeAllowed: true);

		profile.IsUpgrade(Quality(WebDl720P), Quality(WebDl1080P), 0, 0, true).ShouldBeTrue();
	}

	[Fact]
	public void IsUpgrade_ShouldReturnFalse_WhenCandidateIsLowerTier()
	{
		var profile = CreateProfile(cutoff: 1, upgradeAllowed: true);

		profile.IsUpgrade(Quality(WebDl1080P), Quality(WebDl720P), 0, 0, true).ShouldBeFalse();
	}

	[Theory]
	[InlineData(1, 2, true)]
	[InlineData(2, 1, false)]
	[InlineData(1, 1, false)]
	public void IsUpgrade_ShouldHonorRevision_WhenSameTier(int currentVersion, int candidateVersion, bool expected)
	{
		var profile = CreateProfile(cutoff: 2, upgradeAllowed: true);

		profile.IsUpgrade(
			Quality(WebDl1080P, new Revision(currentVersion)),
			Quality(WebDl1080P, new Revision(candidateVersion)),
			0, 0, true).ShouldBe(expected);
	}

	[Fact]
	public void IsUpgrade_ShouldReturnFalse_WhenUpgradeNotAllowed()
	{
		var profile = CreateProfile(cutoff: 1, upgradeAllowed: false);

		profile.IsUpgrade(Quality(WebDl720P), Quality(WebDl1080P), 0, 0, false).ShouldBeFalse();
	}

	[Fact]
	public void IsUpgrade_ShouldReturnFalse_WhenCurrentAlreadyMeetsCutoff()
	{
		var profile = CreateProfile(cutoff: 1, upgradeAllowed: true);

		profile.IsUpgrade(Quality(WebDl1080P), Quality(WebDl2160P()), 0, 0, true).ShouldBeFalse();
	}

	[Fact]
	public void IsUpgrade_ShouldReturnFalse_WhenCandidateIsNotAllowed()
	{
		var profile = CreateProfile(cutoff: 0, upgradeAllowed: true);
		profile.Items[2] = profile.Items[2] with { Allowed = false };

		profile.IsUpgrade(Quality(WebDl720P), Quality(WebDl2160P()), 0, 0, true).ShouldBeFalse();
	}

	[Theory]
	[InlineData(0, true)]
	[InlineData(1, true)]
	[InlineData(2, false)]
	public void MeetsCutoff_ShouldReturnExpected_BasedOnItemOrder(int cutoff, bool expected)
	{
		var profile = CreateProfile(cutoff: cutoff, upgradeAllowed: true);

		profile.MeetsCutoff(Quality(WebDl1080P)).ShouldBe(expected);
	}

	[Fact]
	public void MeetsCutoff_ShouldReturnFalse_WhenQualityIsNotInItems()
	{
		var profile = CreateProfile(cutoff: 2, upgradeAllowed: true);

		profile.MeetsCutoff(Quality(Bluray1080P)).ShouldBeFalse();
	}

	[Fact]
	public void GetIndex_ShouldMatchSourceAndResolution()
	{
		var profile = CreateProfile(cutoff: 1, upgradeAllowed: true);

		profile.GetIndex(Quality(WebDl1080P)).ShouldBe(1);
		profile.GetIndex(Quality(Bluray1080P)).ShouldBe(-1);
	}

	[Fact]
	public void IsAllowed_ShouldReturnFalse_WhenQualityIsUnknown()
	{
		var profile = CreateProfile(cutoff: 1, upgradeAllowed: true);

		profile.IsAllowed(Quality(Bluray1080P)).ShouldBeFalse();
	}

	[Fact]
	public void IsUpgrade_ShouldRequireMinUpgradeFormatScore_WhenCandidateIsHigherTier()
	{
		var profile = CreateProfile(cutoff: 1, upgradeAllowed: true);
		profile.MinUpgradeFormatScore = 100;

		profile.IsUpgrade(Quality(WebDl720P), Quality(WebDl1080P), 0, 50, true).ShouldBeFalse();
		profile.IsUpgrade(Quality(WebDl720P), Quality(WebDl1080P), 0, 100, true).ShouldBeTrue();
	}

	[Theory]
	[InlineData(DownloadPropersAndRepacks.PREFER_AND_UPGRADE, true)]
	[InlineData(DownloadPropersAndRepacks.DO_NOT_UPGRADE, false)]
	[InlineData(DownloadPropersAndRepacks.DO_NOT_PREFER, false)]
	public void IsUpgrade_ShouldHonorPropersPreference_WhenSameTier(DownloadPropersAndRepacks preference, bool expected)
	{
		var profile = CreateProfile(cutoff: 2, upgradeAllowed: true);

		profile.IsUpgrade(
			Quality(WebDl1080P, new Revision(1)),
			Quality(WebDl1080P, new Revision(1, IsProper: true)),
			0, 0, true, preference).ShouldBe(expected);
	}

	[Fact]
	public void IsUpgrade_ShouldUpgradeOnCustomFormatScore_WhenSameTierAndScoreBeatsCutoff()
	{
		var profile = CreateProfile(cutoff: 2, upgradeAllowed: true);
		profile.CutoffFormatScore = 100;

		profile.IsUpgrade(Quality(WebDl1080P), Quality(WebDl1080P), 50, 150, true).ShouldBeTrue();
		profile.IsUpgrade(Quality(WebDl1080P), Quality(WebDl1080P), 150, 150, true).ShouldBeFalse();
		profile.IsUpgrade(Quality(WebDl1080P), Quality(WebDl1080P), 50, 150, true, DownloadPropersAndRepacks.DO_NOT_PREFER)
			.ShouldBeTrue();
	}

	[Fact]
	public void IsUpgrade_ShouldNotUpgradeOnScore_WhenCutoffFormatScoreIsZero()
	{
		var profile = CreateProfile(cutoff: 2, upgradeAllowed: true);

		profile.IsUpgrade(Quality(WebDl1080P), Quality(WebDl1080P), 50, 150, true).ShouldBeFalse();
	}

	[Fact]
	public void IsRevisionUpgrade_ShouldPreferHigherVersion_WhateverThePreference()
	{
		QualityProfileExtensions.IsRevisionUpgrade(
			new Revision(1), new Revision(2), DownloadPropersAndRepacks.DO_NOT_UPGRADE).ShouldBeTrue();
		QualityProfileExtensions.IsRevisionUpgrade(
			new Revision(2), new Revision(1), DownloadPropersAndRepacks.PREFER_AND_UPGRADE).ShouldBeFalse();
	}

	[Fact]
	public void Compare_ShouldOrderByTierThenRevision()
	{
		var profile = CreateProfile(cutoff: 1, upgradeAllowed: true);

		profile.Compare(Quality(WebDl1080P), Quality(WebDl720P)).ShouldBePositive();
		profile.Compare(Quality(WebDl720P), Quality(WebDl1080P)).ShouldBeNegative();
		profile.Compare(
			Quality(WebDl1080P, new Revision(1)),
			Quality(WebDl1080P, new Revision(2))).ShouldBeNegative();
		profile.Compare(
			Quality(WebDl1080P, new Revision(2)),
			Quality(WebDl1080P, new Revision(2))).ShouldBe(0);
	}

	private static QualityResolutionModel WebDl2160P() => new(QualitySource.WEB_DL, QualityResolution.R2160_P);

	private static QualityModel Quality(QualityResolutionModel resolution, Revision? revision = null)
		=> new(resolution, revision ?? new Revision());

	private static QualityProfile CreateProfile(int cutoff, bool upgradeAllowed)
		=> new()
		{
			Name = "Test",
			UpgradeAllowed = upgradeAllowed,
			Cutoff = cutoff,
			Items =
			[
				new QualityProfileItem(WebDl720P, true),
				new QualityProfileItem(WebDl1080P, true),
				new QualityProfileItem(WebDl2160P(), true)
			]
		};
}
