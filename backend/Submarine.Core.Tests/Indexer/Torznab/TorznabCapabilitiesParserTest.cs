using Submarine.Core.Indexer.Torznab;
using Xunit;

namespace Submarine.Core.Tests.Indexer.Torznab;

public class TorznabCapabilitiesParserTest
{
	private const string CapsXml = """
		<?xml version="1.0" encoding="UTF-8"?>
		<caps>
			<server version="1.1" title="Test Indexer"/>
			<limits max="100" default="50"/>
			<searching>
				<search available="yes" supportedParams="q"/>
				<tv-search available="yes" supportedParams="q,season,ep,tvdbid"/>
				<movie-search available="no" supportedParams="q,imdbid"/>
			</searching>
			<categories>
				<category id="2000" name="Movies">
					<subcat id="2040" name="Movies/HD"/>
					<subcat id="2045" name="Movies/UHD"/>
				</category>
				<category id="5000" name="TV">
					<subcat id="5040" name="TV/HD"/>
				</category>
			</categories>
		</caps>
		""";

	private readonly TorznabCapabilitiesParser _instance = new();

	[Fact]
	public void Parse_ShouldParseServerAndLimits_WhenPresent()
	{
		var capabilities = _instance.Parse(CapsXml);

		Assert.Equal("Test Indexer", capabilities.ServerTitle);
		Assert.Equal("1.1", capabilities.ServerVersion);
		Assert.Equal(100, capabilities.LimitsMax);
		Assert.Equal(50, capabilities.LimitsDefault);
	}

	[Fact]
	public void Parse_ShouldParseSearchModes_WhenPresent()
	{
		var capabilities = _instance.Parse(CapsXml);

		Assert.NotNull(capabilities.Search);
		Assert.True(capabilities.Search.Available);
		Assert.Equal(new[] { "q" }, capabilities.Search.SupportedParams);

		Assert.NotNull(capabilities.TvSearch);
		Assert.True(capabilities.TvSearch.Available);
		Assert.Equal(new[] { "q", "season", "ep", "tvdbid" }, capabilities.TvSearch.SupportedParams);

		Assert.NotNull(capabilities.MovieSearch);
		Assert.False(capabilities.MovieSearch.Available);
		Assert.Equal(new[] { "q", "imdbid" }, capabilities.MovieSearch.SupportedParams);
	}

	[Fact]
	public void Parse_ShouldParseCategoryTree_WhenPresent()
	{
		var capabilities = _instance.Parse(CapsXml);

		Assert.Equal(2, capabilities.Categories.Count);

		var movies = capabilities.Categories[0];
		Assert.Equal(2000, movies.Id);
		Assert.Equal("Movies", movies.Name);
		Assert.Equal(2, movies.Subcategories.Count);
		Assert.Equal(2040, movies.Subcategories[0].Id);
		Assert.Equal("Movies/HD", movies.Subcategories[0].Name);
		Assert.Equal(2045, movies.Subcategories[1].Id);

		var tv = capabilities.Categories[1];
		Assert.Equal(5000, tv.Id);
		Assert.Single(tv.Subcategories);
	}

	[Fact]
	public void Parse_ShouldReturnEmptyCapabilities_WhenCapsAreEmpty()
	{
		var capabilities = _instance.Parse("<caps></caps>");

		Assert.Null(capabilities.ServerTitle);
		Assert.Null(capabilities.LimitsMax);
		Assert.Null(capabilities.Search);
		Assert.Null(capabilities.TvSearch);
		Assert.Null(capabilities.MovieSearch);
		Assert.Empty(capabilities.Categories);
	}
}
