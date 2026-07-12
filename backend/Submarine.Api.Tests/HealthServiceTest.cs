using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Services;
using Submarine.Core.Library;
using Xunit;

namespace Submarine.Api.Tests;

public class HealthServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task CheckAsync_ShouldReturnWarnings_WhenDatabaseIsEmpty()
	{
		var service = new HealthService(Context, Settings(), new FakeMetadataClient(), new FakeMappingsClient(),
			NullLogger<HealthService>.Instance);

		var issues = await service.CheckAsync();

		Assert.Contains(issues, i => i.Type == "warning" && i.Source == "indexers");
		Assert.Contains(issues, i => i.Type == "warning" && i.Source == "downloadClients");
		Assert.Contains(issues, i => i.Type == "warning" && i.Source == "rootFolders");
	}

	[Fact]
	public async Task CheckAsync_ShouldReturnError_WhenRootFolderPathIsMissing()
	{
		var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

		Context.RootFolders.Add(new RootFolder { Path = missingPath, MediaKind = MediaKind.SERIES });
		await Context.SaveChangesAsync();

		var service = new HealthService(Context, Settings(), new FakeMetadataClient(), new FakeMappingsClient(),
			NullLogger<HealthService>.Instance);

		var issues = await service.CheckAsync();

		Assert.Contains(issues, i => i.Type == "error" && i.Source == "rootFolders" && i.Message.Contains(missingPath));
	}
}
