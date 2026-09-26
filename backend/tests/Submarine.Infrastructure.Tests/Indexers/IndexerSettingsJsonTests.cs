using System.Collections.Generic;
using System.Linq;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.Indexers;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Indexers;

public class IndexerSettingsJsonTests
{
	[Fact]
	public void Validate_ShouldAcceptFullTorznabSettings()
	{
		var errors = IndexerSettingsJson.Validate(IndexerImplementation.TORZNAB, """
			{"baseUrl": "https://indexer.example", "apiPath": "/api", "apiKey": "key", "categories": [2000, 2040], "animeCategories": [5070], "additionalParameters": "&extended=1", "minimumSeeders": 5}
			""");

		errors.ShouldBeEmpty();
	}

	[Fact]
	public void Validate_ShouldRejectTorznab_WhenBaseUrlMissing()
	{
		var errors = IndexerSettingsJson.Validate(IndexerImplementation.TORZNAB, """{"apiKey": "key"}""");
		errors.ShouldContain(error => error.Contains("BaseUrl"));
	}

	[Fact]
	public void Validate_ShouldRejectTorznab_WhenBaseUrlNotAbsolute()
	{
		var errors = IndexerSettingsJson.Validate(IndexerImplementation.TORZNAB, """{"baseUrl": "not-a-url"}""");
		errors.ShouldContain(error => error.Contains("absolute"));
	}

	[Fact]
	public void Validate_ShouldRejectTorznab_WhenMinimumSeedersNegative()
	{
		var errors = IndexerSettingsJson.Validate(IndexerImplementation.TORZNAB, """{"baseUrl": "https://x.example", "minimumSeeders": -1}""");
		errors.ShouldContain(error => error.Contains("MinimumSeeders"));
	}

	[Fact]
	public void Validate_ShouldRejectNewznab_WhenBaseUrlMissing()
	{
		var errors = IndexerSettingsJson.Validate(IndexerImplementation.NEWZNAB, "{}");
		errors.ShouldNotBeEmpty();
	}

	[Fact]
	public void Validate_ShouldRejectCardigann_WhenDefinitionIdMissing()
	{
		var errors = IndexerSettingsJson.Validate(IndexerImplementation.CARDIGANN, "{}");
		errors.ShouldContain(error => error.Contains("DefinitionId"));
	}

	[Fact]
	public void Validate_ShouldAcceptCardigann_WhenOnlyDefinitionIdSet()
	{
		var errors = IndexerSettingsJson.Validate(IndexerImplementation.CARDIGANN, """{"definitionId": "1337x"}""");
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void Validate_ShouldRejectCardigann_WhenBaseUrlOverrideInvalid()
	{
		var errors = IndexerSettingsJson.Validate(IndexerImplementation.CARDIGANN, """{"definitionId": "1337x", "baseUrl": "ftp://x"}""");
		errors.ShouldNotBeEmpty();
	}

	[Fact]
	public void Validate_ShouldReportBrokenJson()
	{
		var errors = IndexerSettingsJson.Validate(IndexerImplementation.TORZNAB, """{"baseUrl": """);
		errors.ShouldHaveSingleItem().ShouldContain("json");
	}

	[Fact]
	public void Deserialize_ShouldApplyDefaults_WhenFieldsMissing()
	{
		var settings = IndexerSettingsJson.Deserialize<TorznabSettings>("""{"baseUrl": "https://x.example"}""");

		settings.ApiPath.ShouldBe("/api");
		settings.MinimumSeeders.ShouldBe(1);
		settings.Categories.ShouldBeNull();
	}

	[Fact]
	public void Deserialize_ShouldParseCollections_WhenPresent()
	{
		var settings = IndexerSettingsJson.Deserialize<CardigannSettings>("""{"definitionId": "yts", "fields": {"apiurl": "movies.example"}}""");

		settings.DefinitionId.ShouldBe("yts");
		settings.Fields.ShouldNotBeNull();
		settings.Fields!["apiurl"].ShouldBe("movies.example");
	}
}
