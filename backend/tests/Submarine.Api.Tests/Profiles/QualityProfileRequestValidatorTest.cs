using Submarine.Api.Features.QualityProfiles;
using Submarine.Core.Quality;
using Shouldly;
using Xunit;

namespace Submarine.Api.Tests.Profiles;

public class QualityProfileRequestValidatorTest
{
	private readonly QualityProfileRequestValidator _validator = new();

	[Fact]
	public async Task Validate_ShouldPass_WhenProfileIsValid()
	{
		var result = await _validator.ValidateAsync(ValidRequest());

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task Validate_ShouldFail_WhenNameIsEmpty()
	{
		var request = ValidRequest() with { Name = "" };

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(error => error.PropertyName == "Name");
	}

	[Fact]
	public async Task Validate_ShouldFail_WhenNoQualityIsAllowed()
	{
		var request = ValidRequest() with
		{
			Items =
			[
				new QualityProfileItemRequest(new QualityModelResource("WEB_DL", "R720_P", "WebDL-720p"), false),
				new QualityProfileItemRequest(new QualityModelResource("WEB_DL", "R1080_P", "WebDL-1080p"), false)
			]
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task Validate_ShouldFail_WhenCutoffPointsOutsideTheItems()
	{
		var request = ValidRequest() with { Cutoff = 5 };

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task Validate_ShouldFail_WhenCutoffQualityIsNotAllowed()
	{
		var request = ValidRequest() with
		{
			Cutoff = 1,
			Items =
			[
				new QualityProfileItemRequest(new QualityModelResource("WEB_DL", "R720_P", "WebDL-720p"), true),
				new QualityProfileItemRequest(new QualityModelResource("WEB_DL", "R1080_P", "WebDL-1080p"), false)
			]
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(error => error.ErrorMessage.Contains("cutoff"));
	}

	[Fact]
	public async Task Validate_ShouldFail_WhenQualityIsUnknown()
	{
		var request = ValidRequest() with
		{
			Items = [new QualityProfileItemRequest(new QualityModelResource("MYSTERY", "R1080_P", "Mystery-1080p"), true)]
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
	}

	private static QualityProfileRequest ValidRequest()
		=> new(
			"HD",
			UpgradeAllowed: true,
			Cutoff: 1,
			Items:
			[
				new QualityProfileItemRequest(new QualityModelResource("WEB_DL", "R720_P", "WebDL-720p"), true),
				new QualityProfileItemRequest(new QualityModelResource("WEB_DL", "R1080_P", "WebDL-1080p"), true)
			],
			FormatItems: [],
			MinFormatScore: 0,
			CutoffFormatScore: 0,
			MinUpgradeFormatScore: 0);
}
