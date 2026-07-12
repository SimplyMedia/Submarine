using Submarine.Api.Models.Request;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Xunit;

namespace Submarine.Api.Tests;

public class ReleaseProfileServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task CreateGetUpdateDelete_ShouldRoundtrip_WhenCalledInSequence()
	{
		var service = new ReleaseProfileService(new ReleaseProfileRepository(Context));

		var created = await service.CreateAsync(new CreateReleaseProfileRequest
		{
			Name = "Preferred",
			Required = new List<string> { "PROPER" },
			Ignored = new List<string> { "CAM" },
			Indexer = "Indexer1",
			Tags = new List<string> { "hd" }
		});
		Context.ChangeTracker.Clear();

		var fetched = await service.GetAsync(created.Id);
		Assert.Equal("Preferred", fetched.Name);
		Assert.Equal(new List<string> { "PROPER" }, fetched.Required);

		var updated = await service.UpdateAsync(created.Id, new UpdateReleaseProfileRequest
		{
			Name = "Preferred v2",
			Enabled = false,
			Required = new List<string> { "PROPER", "REPACK" },
			Ignored = new List<string> { "CAM" },
			Indexer = null,
			Tags = new List<string>()
		});
		Assert.Equal("Preferred v2", updated.Name);
		Assert.False(updated.Enabled);
		Assert.Null(updated.Indexer);
		Context.ChangeTracker.Clear();

		var deleted = await service.DeleteAsync(created.Id);
		Assert.Equal(created.Id, deleted.Id);
		await Assert.ThrowsAsync<Submarine.Api.Exceptions.NotFoundException>(() => service.GetAsync(created.Id));
	}
}
