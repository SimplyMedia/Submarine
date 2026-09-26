using Shouldly;
using Submarine.Core.Languages;
using Submarine.Core.MediaFiles;
using Xunit;

namespace Submarine.Infrastructure.Tests.MediaFiles;

public sealed class ExtraFileLanguageDetectorTests
{
	[Theory]
	[InlineData("Movie.Title.2020.en", Language.ENGLISH)]
	[InlineData("Movie.Title.2020.eng", Language.ENGLISH)]
	[InlineData("Movie.Title.2020.fre", Language.FRENCH)]
	[InlineData("Movie.Title.2020_de", Language.GERMAN)]
	[InlineData("Movie.Title.2020-pt", Language.PORTUGUESE)]
	public void Detect_ShouldRecognizeLanguageSuffix(string nameWithoutExtension, Language expected)
		=> ExtraFileLanguageDetector.Detect(nameWithoutExtension).ShouldBe(expected);

	[Fact]
	public void Detect_ShouldReturnNull_WhenNoSuffixPresent()
		=> ExtraFileLanguageDetector.Detect("Movie.Title.2020").ShouldBeNull();

	[Fact]
	public void Detect_ShouldReturnNull_WhenSuffixIsNotARecognizedCode()
		=> ExtraFileLanguageDetector.Detect("Movie.Title.2020.forced").ShouldBeNull();
}
