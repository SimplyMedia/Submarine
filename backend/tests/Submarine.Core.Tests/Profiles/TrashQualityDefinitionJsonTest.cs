using Submarine.Core.Profiles;
using Submarine.Core.Quality;
using Shouldly;
using Xunit;

namespace Submarine.Core.Tests.Profiles;

public class TrashQualityDefinitionJsonTest
{
	[Theory]
	[InlineData("SDTV", QualitySource.TV, QualityResolution.R480_P)]
	[InlineData("DVD", QualitySource.DVD, null)]
	[InlineData("HDTV-1080p", QualitySource.TV, QualityResolution.R1080_P)]
	[InlineData("WEBDL-1080p", QualitySource.WEB_DL, QualityResolution.R1080_P)]
	[InlineData("WEBRip-720p", QualitySource.WEB_RIP, QualityResolution.R720_P)]
	[InlineData("Bluray-1080p", QualitySource.BLURAY, QualityResolution.R1080_P)]
	[InlineData("Bluray-1080p Remux", QualitySource.BLURAY_REMUX, QualityResolution.R1080_P)]
	[InlineData("Remux-2160p", QualitySource.BLURAY_REMUX, QualityResolution.R2160_P)]
	public void Resolve_ShouldMapTrashQualityNames_ToSourceAndResolution(
		string quality, QualitySource expectedSource, QualityResolution? expectedResolution)
	{
		var resolved = TrashQualityDefinitionJson.Resolve(new TrashQualityDefinition(quality, 10, 100, 200));

		resolved.ShouldNotBeNull();
		resolved!.Value.Source.ShouldBe(expectedSource);
		resolved.Value.Resolution.ShouldBe(expectedResolution);
	}

	[Fact]
	public void Resolve_ShouldReturnNull_WhenQualityNameIsUnknown()
		=> TrashQualityDefinitionJson.Resolve(new TrashQualityDefinition("Unknown-4k", 10, 100, 200)).ShouldBeNull();

	[Fact]
	public void Resolve_ShouldConvertTheSeriesUnlimitedSentinel_ToNull()
	{
		// series quality-size files use preferred 995 / max 1000 as the "Unlimited" sentinel
		var resolved = TrashQualityDefinitionJson.Resolve(new TrashQualityDefinition("Bluray-1080p", 50.4, 995, 1000));

		resolved.ShouldNotBeNull();
		resolved!.Value.Max.ShouldBeNull();
		resolved.Value.Preferred.ShouldBeNull();
		resolved.Value.Min.ShouldBe(50.4);
	}

	[Fact]
	public void Resolve_ShouldConvertTheMovieUnlimitedSentinel_ToNull()
	{
		// movie quality-size files use preferred 1999 / max 2000 as the "Unlimited" sentinel
		var resolved = TrashQualityDefinitionJson.Resolve(new TrashQualityDefinition("Remux-1080p", 102, 1999, 2000));

		resolved.ShouldNotBeNull();
		resolved!.Value.Max.ShouldBeNull();
		resolved.Value.Preferred.ShouldBeNull();
	}

	[Fact]
	public void Resolve_ShouldKeepARealLimit_WhenBelowTheUnlimitedSentinel()
	{
		var resolved = TrashQualityDefinitionJson.Resolve(new TrashQualityDefinition("WEBDL-1080p", 12.5, 90, 199.9));

		resolved.ShouldNotBeNull();
		resolved!.Value.Max.ShouldBe(199.9);
		resolved.Value.Preferred.ShouldBe(90);
	}
}
