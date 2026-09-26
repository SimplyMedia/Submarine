using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Submarine.Infrastructure.Indexers.Cardigann;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Infrastructure.Tests;

/// <summary>
///     Asserts the Infrastructure service module wires app data paths from the DataDirectory
///     singleton, not the process current directory.
/// </summary>
public sealed class InfrastructureServiceModuleTests
{
	[Fact]
	public void Register_ShouldResolveIndexerDefinitionLoader_UnderDataDirectory()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(new DataDirectory("/config"));
		new InfrastructureServiceModule().Register(services, new ConfigurationBuilder().Build());

		using var provider = services.BuildServiceProvider();
		var loader = provider.GetRequiredService<IndexerDefinitionLoader>();

		loader.AppDataPath.ShouldBe(Path.Combine("/config", "definitions"));
	}
}
