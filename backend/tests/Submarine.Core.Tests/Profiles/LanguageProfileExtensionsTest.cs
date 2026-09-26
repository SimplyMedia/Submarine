using Submarine.Core.Entities;
using Submarine.Core.Languages;
using Submarine.Core.Profiles;
using Shouldly;
using Xunit;

namespace Submarine.Core.Tests.Profiles;

public sealed class LanguageProfileExtensionsTest
{
	private static LanguageProfile CreateProfile(params Language[] languages)
		=> new() { Languages = [.. languages], Cutoff = languages[^1], UpgradeAllowed = true };

	[Fact]
	public void IsWanted_ShouldMatchAnyConfiguredLanguage()
	{
		var profile = CreateProfile(Language.ENGLISH, Language.FRENCH);

		profile.IsWanted([Language.JAPANESE, Language.FRENCH]).ShouldBeTrue();
		profile.IsWanted([Language.JAPANESE]).ShouldBeFalse();
	}

	[Fact]
	public void MeetsCutoff_ShouldRequireTheConfiguredCutoffLanguage()
	{
		var profile = CreateProfile(Language.ENGLISH, Language.FRENCH);

		profile.MeetsCutoff([Language.ENGLISH, Language.FRENCH]).ShouldBeTrue();
		profile.MeetsCutoff([Language.ENGLISH]).ShouldBeFalse();
	}

	[Fact]
	public void IsUpgrade_ShouldCompareBestWantedLanguageAndHonorUpgradeSetting()
	{
		var profile = CreateProfile(Language.ENGLISH, Language.FRENCH, Language.JAPANESE);

		profile.IsUpgrade([Language.ENGLISH], [Language.FRENCH, Language.JAPANESE]).ShouldBeFalse();
		profile.IsUpgrade([Language.JAPANESE], [Language.FRENCH]).ShouldBeTrue();
		profile.IsUpgrade([Language.FRENCH], [Language.ENGLISH]).ShouldBeTrue();
		profile.IsUpgrade([Language.ENGLISH], [Language.GERMAN]).ShouldBeFalse();
		profile.IsUpgrade([Language.GERMAN], [Language.FRENCH]).ShouldBeTrue();

		profile.UpgradeAllowed = false;
		profile.IsUpgrade([Language.ENGLISH], [Language.FRENCH]).ShouldBeFalse();
	}
}
