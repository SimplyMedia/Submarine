using System.Collections.Generic;
using System.Linq;
using Submarine.Core.Indexers;
using Submarine.Core.Provider;
using Submarine.Infrastructure.Indexers.Torznab;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Indexers;

public class TorznabCapabilitiesParserTests
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
				<music-search available="yes" supportedParams="q"/>
				<book-search available="yes" supportedParams="q,author"/>
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

	[Fact]
	public void Parse_ShouldParseLimits_WhenPresent()
	{
		var capabilities = TorznabCapabilitiesParser.Parse(CapsXml);
		capabilities.LimitsMax.ShouldBe(100);
		capabilities.LimitsDefault.ShouldBe(50);
	}

	[Fact]
	public void Parse_ShouldParseSearchModes_WhenPresent()
	{
		var capabilities = TorznabCapabilitiesParser.Parse(CapsXml);

		capabilities.SearchAvailable.ShouldBeTrue();
		capabilities.SearchParams.ShouldBe(SearchParams.Q);

		capabilities.TvSearchAvailable.ShouldBeTrue();
		capabilities.TvSearchParams.ShouldBe(SearchParams.Q | SearchParams.Season | SearchParams.Ep | SearchParams.TvdbId);

		capabilities.MovieSearchAvailable.ShouldBeFalse();
		capabilities.MovieSearchParams.ShouldBe(SearchParams.Q | SearchParams.ImdbId);

		capabilities.MusicSearchAvailable.ShouldBeTrue();
		capabilities.BookSearchAvailable.ShouldBeTrue();
	}

	[Fact]
	public void Parse_ShouldParseCategoryTree_WhenPresent()
	{
		var capabilities = TorznabCapabilitiesParser.Parse(CapsXml);
		capabilities.Categories.Count.ShouldBe(2);

		var movies = capabilities.Categories[0];
		movies.Id.ShouldBe(2000);
		movies.Name.ShouldBe("Movies");
		movies.SubCategories.Select(category => category.Id).ShouldBe([2040, 2045]);

		capabilities.Categories[1].SubCategories.ShouldHaveSingleItem().Id.ShouldBe(5040);
	}

	[Fact]
	public void Parse_ShouldUseStandardName_WhenNameAttributeMissing()
	{
		const string xml = """
			<caps>
				<categories>
					<category id="5070">
						<subcat id="5040"/>
					</category>
				</categories>
			</caps>
			""";

		var capabilities = TorznabCapabilitiesParser.Parse(xml);
		capabilities.Categories.ShouldHaveSingleItem().Name.ShouldBe("TV/Anime");
	}

	[Fact]
	public void Parse_ShouldReturnEmptyCapabilities_WhenCapsAreEmpty()
	{
		var capabilities = TorznabCapabilitiesParser.Parse("<caps></caps>");

		capabilities.LimitsMax.ShouldBeNull();
		capabilities.SearchAvailable.ShouldBeFalse();
		capabilities.TvSearchAvailable.ShouldBeFalse();
		capabilities.MovieSearchAvailable.ShouldBeFalse();
		capabilities.Categories.ShouldBeEmpty();
	}

	[Fact]
	public void Parse_ShouldThrow_WhenCapsRootMissing()
	{
		Should.Throw<IndexerException>(() => TorznabCapabilitiesParser.Parse("<rss></rss>"))
			.Message.ShouldContain("caps");
	}
}
