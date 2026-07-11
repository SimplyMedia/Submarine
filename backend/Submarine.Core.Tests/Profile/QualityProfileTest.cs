using System.Collections.Generic;
using Submarine.Core.Profile;
using Submarine.Core.Quality;
using Xunit;

namespace Submarine.Core.Tests.Profile;

public class QualityProfileTest
{
	private static readonly QualityResolutionModel WebDl480P = new(QualitySource.WEB_DL, QualityResolution.R480_P);
	private static readonly QualityResolutionModel WebDl720P = new(QualitySource.WEB_DL, QualityResolution.R720_P);
	private static readonly QualityResolutionModel WebDl1080P = new(QualitySource.WEB_DL, QualityResolution.R1080_P);
	private static readonly QualityResolutionModel Bluray1080P = new(QualitySource.BLURAY, QualityResolution.R1080_P);

	[Fact]
	public void IsUpgrade_ShouldReturnTrue_WhenCandidateIsHigherTierAndAllowed()
	{
		var profile = CreateProfile(cutoff: 3, upgradeAllowed: true);

		var current = new QualityModel(WebDl480P, new Revision());
		var candidate = new QualityModel(WebDl720P, new Revision());

		Assert.True(profile.IsUpgrade(current, candidate));
	}

	[Fact]
	public void IsUpgrade_ShouldReturnFalse_WhenCandidateIsLowerTier()
	{
		var profile = CreateProfile(cutoff: 3, upgradeAllowed: true);

		var current = new QualityModel(WebDl720P, new Revision());
		var candidate = new QualityModel(WebDl480P, new Revision());

		Assert.False(profile.IsUpgrade(current, candidate));
	}

	[Theory]
	[InlineData(1, 2, true)]
	[InlineData(2, 1, false)]
	[InlineData(1, 1, false)]
	public void IsUpgrade_ShouldHonorRevision_WhenSameTier(int currentVersion, int candidateVersion, bool expected)
	{
		var profile = CreateProfile(cutoff: 3, upgradeAllowed: true);

		var current = new QualityModel(WebDl720P, new Revision(currentVersion));
		var candidate = new QualityModel(WebDl720P, new Revision(candidateVersion));

		Assert.Equal(expected, profile.IsUpgrade(current, candidate));
	}

	[Fact]
	public void IsUpgrade_ShouldReturnFalse_WhenUpgradeNotAllowed()
	{
		var profile = CreateProfile(cutoff: 3, upgradeAllowed: false);

		var current = new QualityModel(WebDl480P, new Revision());
		var candidate = new QualityModel(WebDl720P, new Revision());

		Assert.False(profile.IsUpgrade(current, candidate));
	}

	[Fact]
	public void IsUpgrade_ShouldReturnFalse_WhenCurrentAlreadyMeetsCutoff()
	{
		var profile = CreateProfile(cutoff: 1, upgradeAllowed: true);

		var current = new QualityModel(WebDl720P, new Revision());
		var candidate = new QualityModel(WebDl1080P, new Revision());

		Assert.False(profile.IsUpgrade(current, candidate));
	}

	[Fact]
	public void IsUpgrade_ShouldReturnFalse_WhenCandidateIsNotAllowed()
	{
		var profile = CreateProfile(cutoff: 3, upgradeAllowed: true);

		var current = new QualityModel(WebDl1080P, new Revision());
		var candidate = new QualityModel(Bluray1080P, new Revision());

		Assert.False(profile.IsUpgrade(current, candidate));
	}

	[Theory]
	[InlineData(0, true)]
	[InlineData(1, true)]
	[InlineData(2, false)]
	public void MeetsCutoff_ShouldReturnExpected_BasedOnItemOrder(int cutoff, bool expected)
	{
		var profile = CreateProfile(cutoff, upgradeAllowed: true);

		var quality = new QualityModel(WebDl720P, new Revision());

		Assert.Equal(expected, profile.MeetsCutoff(quality));
	}

	[Fact]
	public void MeetsCutoff_ShouldReturnFalse_WhenQualityIsNotInItems()
	{
		var profile = CreateProfile(cutoff: 0, upgradeAllowed: true);

		var quality = new QualityModel(new QualityResolutionModel(QualitySource.CAM), new Revision());

		Assert.False(profile.MeetsCutoff(quality));
	}

	private static QualityProfile CreateProfile(int cutoff, bool upgradeAllowed)
		=> new()
		{
			Name = "Test",
			UpgradeAllowed = upgradeAllowed,
			Cutoff = cutoff,
			Items = new List<QualityProfileItem>
			{
				new() { Quality = WebDl480P, Allowed = true },
				new() { Quality = WebDl720P, Allowed = true },
				new() { Quality = WebDl1080P, Allowed = true },
				new() { Quality = Bluray1080P, Allowed = false }
			}
		};
}
