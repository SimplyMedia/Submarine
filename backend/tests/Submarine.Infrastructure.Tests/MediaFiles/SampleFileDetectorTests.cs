using Shouldly;
using Submarine.Core.MediaFiles;
using Xunit;

namespace Submarine.Infrastructure.Tests.MediaFiles;

public sealed class SampleFileDetectorTests
{
	[Fact]
	public void IsSample_ShouldReturnTrue_WhenSmallAndNamedSampleAndOthersExist()
		=> SampleFileDetector.IsSample("Movie.Title.2020.sample.mkv", 50 * 1024 * 1024, otherMediaFilesExist: true).ShouldBeTrue();

	[Fact]
	public void IsSample_ShouldReturnFalse_WhenNoOtherMediaFilesExist()
		=> SampleFileDetector.IsSample("Movie.Title.2020.sample.mkv", 50 * 1024 * 1024, otherMediaFilesExist: false).ShouldBeFalse();

	[Fact]
	public void IsSample_ShouldReturnFalse_WhenFileIsLarge()
		=> SampleFileDetector.IsSample("Movie.Title.2020.sample.mkv", 500L * 1024 * 1024, otherMediaFilesExist: true).ShouldBeFalse();

	[Fact]
	public void IsSample_ShouldReturnFalse_WhenNameDoesNotMentionSample()
		=> SampleFileDetector.IsSample("Movie.Title.2020.mkv", 50 * 1024 * 1024, otherMediaFilesExist: true).ShouldBeFalse();

	[Theory]
	[InlineData("Movie.Title.2020.mkv.!qB", true)]
	[InlineData("Movie.Title.2020.mkv.part", true)]
	[InlineData("Movie.Title.2020.mkv", false)]
	public void IsIncompleteDownloadArtifact_ShouldDetectInProgressExtensions(string fileName, bool expected)
		=> SampleFileDetector.IsIncompleteDownloadArtifact(fileName).ShouldBe(expected);
}
