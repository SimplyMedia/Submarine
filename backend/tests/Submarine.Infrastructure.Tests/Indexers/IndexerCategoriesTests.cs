using Submarine.Core.Indexers;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Indexers;

public class IndexerCategoriesTests
{
	[Fact]
	public void ById_ShouldResolveParents_WhenRootIdsRequested()
	{
		IndexerCategories.ById(1000).ShouldNotBeNull().Name.ShouldBe("Console");
		IndexerCategories.ById(2000).ShouldNotBeNull().Name.ShouldBe("Movies");
		IndexerCategories.ById(3000).ShouldNotBeNull().Name.ShouldBe("Audio");
		IndexerCategories.ById(4000).ShouldNotBeNull().Name.ShouldBe("PC");
		IndexerCategories.ById(5000).ShouldNotBeNull().Name.ShouldBe("TV");
		IndexerCategories.ById(6000).ShouldNotBeNull().Name.ShouldBe("XXX");
		IndexerCategories.ById(7000).ShouldNotBeNull().Name.ShouldBe("Books");
		IndexerCategories.ById(8000).ShouldNotBeNull().Name.ShouldBe("Other");
	}

	[Fact]
	public void ById_ShouldResolveSubcategories_WhenStandardIdsRequested()
	{
		IndexerCategories.ById(2045).ShouldNotBeNull().Name.ShouldBe("Movies/UHD");
		IndexerCategories.ById(5045).ShouldNotBeNull().Name.ShouldBe("TV/UHD");
		IndexerCategories.ById(5070).ShouldNotBeNull().Name.ShouldBe("TV/Anime");
		IndexerCategories.ById(2090).ShouldNotBeNull().Name.ShouldBe("Movies/x265");
		IndexerCategories.ById(5090).ShouldNotBeNull().Name.ShouldBe("TV/x265");
		IndexerCategories.ById(2080).ShouldNotBeNull().Name.ShouldBe("Movies/WEB-DL");
		IndexerCategories.ById(5010).ShouldNotBeNull().Name.ShouldBe("TV/WEB-DL");
	}

	[Fact]
	public void ById_ShouldReturnNull_WhenIdUnknown()
		=> IndexerCategories.ById(9999).ShouldBeNull();

	[Fact]
	public void ParentId_ShouldReturnParent_WhenSubcategoryRequested()
		=> IndexerCategories.ParentId(5040).ShouldBe(5000);

	[Fact]
	public void ParentId_ShouldReturnNull_WhenRootRequested()
		=> IndexerCategories.ParentId(5000).ShouldBeNull();

	[Fact]
	public void Resolve_ShouldMatchExactPath_WhenCaseAndSpacingDiffer()
	{
		IndexerCategories.Resolve("TV/Anime").ShouldBe(5070);
		IndexerCategories.Resolve("tv/anime").ShouldBe(5070);
		IndexerCategories.Resolve("Console/XBox 360").ShouldBe(1050);
		IndexerCategories.Resolve("Movies/WEB-DL").ShouldBe(2080);
		IndexerCategories.Resolve("Books/EBook").ShouldBe(7010);
	}

	[Fact]
	public void Resolve_ShouldFallBackToLongestKnownPrefix_WhenLeafUnknown()
		=> IndexerCategories.Resolve("Movies/Unknown").ShouldBe(2000);

	[Fact]
	public void Resolve_ShouldFallBackToParent_WhenSubcategoryMissingFromTree()
		=> IndexerCategories.Resolve("PC/ISO").ShouldBe(4000);

	[Fact]
	public void InclusiveDescendantIds_ShouldIncludeSelfAndChildren_WhenParentRequested()
	{
		var ids = IndexerCategories.InclusiveDescendantIds(5000);
		ids.ShouldContain(5000);
		ids.ShouldContain(5030);
		ids.ShouldContain(5070);
		ids.Count.ShouldBe(11);
	}

	[Fact]
	public void InclusiveDescendantIds_ShouldReturnOnlySelf_WhenLeafRequested()
		=> IndexerCategories.InclusiveDescendantIds(5070).ShouldBe([5070]);
}
