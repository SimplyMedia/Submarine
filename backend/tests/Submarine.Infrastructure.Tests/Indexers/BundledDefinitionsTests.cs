using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.Http;
using Submarine.Infrastructure.Indexers;
using Submarine.Infrastructure.Indexers.Cardigann;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Indexers;

public class BundledDefinitionsTests
{
	public static IEnumerable<object[]> BundledDefinitionPaths()
		=> Directory.GetFiles(FindDefinitionsFolder(), "*.yml")
			.Select(path => new object[] { path })
			.ToArray();

	[Theory]
	[MemberData(nameof(BundledDefinitionPaths))]
	public void Parse_ShouldLoadBundledDefinition_WhenFileValid(string path)
	{
		var definition = CardigannDefinitionParser.Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));

		definition.Id.ShouldNotBeEmpty();
		definition.Name.ShouldNotBeEmpty();
		definition.Links.ShouldNotBeEmpty();
		definition.Caps.Modes.ShouldContainKey("search");
		definition.Search.Paths.ShouldNotBeEmpty();
		definition.Search.Rows.Selector.ShouldNotBeEmpty();
		definition.Search.Fields.ShouldContainKey("title");
		(definition.Search.Fields.ContainsKey("download") || definition.Search.Fields.ContainsKey("infohash") || definition.Download is not null)
			.ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(BundledDefinitionPaths))]
	public void ToInfo_ShouldResolveStandardCategories_WhenMappingsPresent(string path)
	{
		var definition = CardigannDefinitionParser.Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
		var info = definition.ToInfo();

		info.Id.ShouldBe(definition.Id);
		info.Settings.ShouldNotBeNull();
		info.Links.ShouldBe(definition.Links);
		info.Protocol.ShouldBeOneOf("torrent", "usenet");
		info.Type.ShouldBeOneOf("public", "private", "semi-private");
	}

	[Fact]
	public void BundledFolder_ShouldContainTheExpectedPublicDefinitions()
	{
		var ids = Directory.GetFiles(FindDefinitionsFolder(), "*.yml")
			.Select(path => Path.GetFileNameWithoutExtension(path))
			.ToList();

		foreach (var expected in new[]
		{
			"1337x", "thepiratebay", "yts", "eztv", "nyaasi", "limetorrents", "kickasstorrents-to",
			"torrentproject2", "extratorrent-st", "btdirectory", "internetarchive", "animetosho-xyz", "best-torrents",
			"comicat"
		})
		{
			ids.ShouldContain(expected);
		}

		ids.Count.ShouldBe(14);
	}

	[Fact]
	public void BundledFolder_ShouldCoverEveryResponseTypeAndLoginMode()
	{
		var definitions = Directory.GetFiles(FindDefinitionsFolder(), "*.yml")
			.Select(path => CardigannDefinitionParser.Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path)))
			.ToList();

		definitions.Select(definition => definition.Search.Paths[0].Response?.Type ?? "html").Distinct().ShouldContain("json");
		definitions.Select(definition => definition.Search.Paths[0].Response?.Type ?? "html").Distinct().ShouldContain("xml");
		definitions.Where(definition => definition.Login is not null)
			.Select(definition => definition.Login!.Method)
			.Distinct()
			.ShouldContain("form");
		definitions.Where(definition => definition.Login is not null)
			.Select(definition => definition.Login!.Method)
			.Distinct()
			.ShouldContain("get");
	}

	[Fact]
	public async Task Factory_ShouldCreateCardigannIndexerFromBundledDefinition()
	{
		var services = new ServiceCollection();
		services.AddHttpClient();
		services.AddLogging();
		using var provider = services.BuildServiceProvider();

		var loader = new IndexerDefinitionLoader(
			new NullLogger<IndexerDefinitionLoader>(),
			Path.Combine(Path.GetTempPath(), "submarine-factory-test-" + Guid.NewGuid().ToString("N")),
			FindDefinitionsFolder());
		var outboundProxyProvider = Substitute.For<IOutboundProxyProvider>();
		outboundProxyProvider.GetSnapshotAsync(Arg.Any<CancellationToken>())
			.Returns(new OutboundProxySnapshot(false, Submarine.Core.Enums.IndexerProxyType.HTTP, "", 0, null, null, "", true, CertificateValidationType.ENABLED));
		var factory = new IndexerFactory(
			new IndexerHttpClientFactory(provider.GetRequiredService<IHttpClientFactory>(), outboundProxyProvider, new NullLogger<IndexerHttpClientFactory>()),
			loader,
			provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());

		await using var indexer = factory.Create(
			IndexerImplementation.CARDIGANN,
			"""{"definitionId": "1337x"}""");

		indexer.Name.ShouldBe("1337x");
		indexer.Protocol.ShouldBe(Submarine.Core.Provider.Protocol.BITTORRENT);

		var errors = factory.Validate(IndexerImplementation.CARDIGANN, """{"definitionId": "does-not-exist"}""");
		errors.ShouldBeEmpty();
	}

	internal static string FindDefinitionsFolder()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			var candidate = Path.Combine(directory.FullName, "backend", "src", "Submarine.Infrastructure", "Definitions");
			if (Directory.Exists(candidate))
				return candidate;

			directory = directory.Parent!;
		}

		throw new InvalidOperationException("Definitions folder not found above the test output");
	}
}
