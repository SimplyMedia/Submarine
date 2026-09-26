using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.Indexers;
using Submarine.Infrastructure.Indexers.Cardigann;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Indexers;

public class IndexerDefinitionLoaderTests : IDisposable
{
	private readonly string _bundled;
	private readonly string _appData;
	private readonly IndexerDefinitionLoader _loader;

	public IndexerDefinitionLoaderTests()
	{
		var root = Path.Combine(Path.GetTempPath(), "submarine-defs-" + Guid.NewGuid().ToString("N"));
		_bundled = Path.Combine(root, "bundled");
		_appData = Path.Combine(root, "appdata");
		Directory.CreateDirectory(_bundled);
		Directory.CreateDirectory(_appData);
		_loader = new IndexerDefinitionLoader(new NullLogger<IndexerDefinitionLoader>(), _bundled, _appData);
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(Path.GetDirectoryName(_bundled)!, recursive: true);
		}
		catch (IOException)
		{
		}
	}

	[Fact]
	public void LoadAll_ShouldLoadBundledDefinitions_WhenPresent()
	{
		File.WriteAllText(Path.Combine(_bundled, "alpha.yml"), Bundled("alpha", "Alpha"));
		File.WriteAllText(Path.Combine(_bundled, "beta.yml"), Bundled("beta", "Beta"));

		var definitions = _loader.LoadAll();

		definitions.Select(definition => definition.Id).ShouldBe(["alpha", "beta"]);
	}

	[Fact]
	public void LoadAll_ShouldPreferAppData_WhenIdsConflict()
	{
		File.WriteAllText(Path.Combine(_bundled, "alpha.yml"), Bundled("alpha", "Bundled Alpha"));
		File.WriteAllText(Path.Combine(_appData, "alpha.yml"), Bundled("alpha", "User Alpha"));

		var definitions = _loader.LoadAll();

		definitions.ShouldHaveSingleItem().Name.ShouldBe("User Alpha");
	}

	[Fact]
	public void LoadAll_ShouldKeepBundled_WhenOnlyAppDataDiffers()
	{
		File.WriteAllText(Path.Combine(_bundled, "alpha.yml"), Bundled("alpha", "Alpha"));
		File.WriteAllText(Path.Combine(_appData, "other.yml"), Bundled("other", "Other"));

		var definitions = _loader.LoadAll();

		definitions.Select(definition => definition.Id).ShouldBe(["alpha", "other"]);
	}

	[Fact]
	public void LoadAll_ShouldSkipBrokenFiles_WhenYamlInvalid()
	{
		File.WriteAllText(Path.Combine(_bundled, "good.yml"), Bundled("good", "Good"));
		File.WriteAllText(Path.Combine(_bundled, "broken.yml"), "not: [valid: yaml");

		var definitions = _loader.LoadAll();

		definitions.Select(definition => definition.Id).ShouldBe(["good"]);
	}

	[Fact]
	public void LoadAll_ShouldReturnEmpty_WhenFoldersMissing()
		=> new IndexerDefinitionLoader(new NullLogger<IndexerDefinitionLoader>(), "/does/not/exist", "/also/missing")
			.LoadAll().ShouldBeEmpty();

	[Fact]
	public void Load_ShouldFallBackToBundled_WhenAppDataMissing()
	{
		File.WriteAllText(Path.Combine(_bundled, "alpha.yml"), Bundled("alpha", "Alpha"));

		_loader.Load("alpha").ShouldNotBeNull().Name.ShouldBe("Alpha");
		_loader.Load("unknown").ShouldBeNull();
	}

	[Fact]
	public void Load_ShouldPreferAppData_WhenBothExist()
	{
		File.WriteAllText(Path.Combine(_bundled, "alpha.yml"), Bundled("alpha", "Bundled"));
		File.WriteAllText(Path.Combine(_appData, "alpha.yml"), Bundled("alpha", "User"));

		_loader.Load("alpha").ShouldNotBeNull().Name.ShouldBe("User");
	}

	[Fact]
	public void Parse_ShouldDeriveIdFromFileName_WhenYamlHasNoId()
	{
		var definition = CardigannDefinitionParser.Parse(Bundled("", "No Id"), "fromfile");

		definition.Id.ShouldBe("fromfile");
		definition.Name.ShouldBe("No Id");
	}

	[Fact]
	public void Parse_ShouldThrowIndexerException_WhenYamlBroken()
		=> Should.Throw<IndexerException>(() => CardigannDefinitionParser.Parse("not: [valid: yaml"));

	private static string Bundled(string id, string name)
		=> $"""
			---
			id: {id}
			name: {name}
			language: en-US
			type: public
			encoding: UTF-8
			links:
			  - https://{id}.example/
			caps:
			  modes:
			    search: [q]
			settings: []
			search:
			  paths:
			    - path: browse
			  rows:
			    selector: tr
			  fields:
			    title:
			      selector: td.title
			""";
}
