using Submarine.Core.Profiles;
using Submarine.Core.Quality;
using Shouldly;
using Xunit;

namespace Submarine.Core.Tests.Profiles;

public class QualityProfileTemplatesTest
{
	[Fact]
	public void Create_ShouldBuildEveryKnownTemplate()
	{
		foreach (var name in QualityProfileTemplates.Names)
		{
			var profile = QualityProfileTemplates.Create(name);

			profile.Name.ShouldBe(name);
			profile.UpgradeAllowed.ShouldBeTrue();
			profile.Items.Count.ShouldBe(QualityResolutionModel.All.Length);
			profile.Items[profile.Cutoff].Allowed.ShouldBeTrue();
			profile.Items.Count(item => item.Allowed).ShouldBeGreaterThan(0);
		}
	}

	[Fact]
	public void Create_ShouldThrow_WhenTemplateIsUnknown()
	{
		Should.Throw<KeyNotFoundException>(() => QualityProfileTemplates.Create("Ultra-HD-8K"));
	}

	[Fact]
	public void Create_Any_ShouldAllowEverything_AndCutoffAtTheHighest()
	{
		var profile = QualityProfileTemplates.Create("Any");

		profile.Items.ShouldAllBe(item => item.Allowed);
		profile.Cutoff.ShouldBe(profile.Items.Count - 1);
	}

	[Fact]
	public void Create_Hd1080p_ShouldOnlyAllow1080p()
	{
		var profile = QualityProfileTemplates.Create("HD-1080p");

		profile.Items.Where(item => item.Allowed)
			.ShouldAllBe(item => item.Quality.Resolution == QualityResolution.R1080_P);
		profile.Items.Count(item => item.Allowed).ShouldBeGreaterThan(1);
	}

	[Fact]
	public void Create_Remux1080p_ShouldCutoffAtRemux()
	{
		var profile = QualityProfileTemplates.Create("Remux-1080p");

		var cutoffItem = profile.Items[profile.Cutoff];
		cutoffItem.Quality.Source.ShouldBe(QualitySource.BLURAY_REMUX);
		cutoffItem.Quality.Resolution.ShouldBe(QualityResolution.R1080_P);
	}

	[Fact]
	public void Create_ShouldOrderItemsLowToHigh()
	{
		var profile = QualityProfileTemplates.Create("Any");

		var resolutions = profile.Items
			.Select(item => item.Quality.Resolution)
			.Select(resolution => resolution switch
			{
				QualityResolution.R360_P => 0,
				QualityResolution.R480_P => 1,
				QualityResolution.R540_P => 2,
				QualityResolution.R576_P => 3,
				QualityResolution.R720_P => 4,
				QualityResolution.R1080_P => 5,
				QualityResolution.R2160_P => 6,
				_ => -1
			})
			.ToList();

		resolutions.ShouldBe(resolutions.OrderBy(tier => tier).ToList());
	}

	[Fact]
	public void Create_ShouldMatchTemplateNameCaseInsensitively()
	{
		QualityProfileTemplates.Create("any").Name.ShouldBe("any");
	}
}
