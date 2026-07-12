using Submarine.Api.Exceptions;
using Submarine.Api.Models.Request;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.Provider;
using Xunit;

namespace Submarine.Api.Tests;

public class DelayProfileServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task GetAllAsync_ShouldSeedDefaultProfile_WhenNoneExist()
	{
		var service = new DelayProfileService(new DelayProfileRepository(Context));

		var profiles = await service.GetAllAsync();

		var defaultProfile = Assert.Single(profiles);
		Assert.Equal(int.MaxValue, defaultProfile.Order);
		Assert.Empty(defaultProfile.Tags);
	}

	[Fact]
	public async Task DeleteAsync_ShouldThrow_WhenDeletingDefaultProfile()
	{
		var service = new DelayProfileService(new DelayProfileRepository(Context));
		var defaultProfile = Assert.Single(await service.GetAllAsync());

		await Assert.ThrowsAsync<BadRequestException>(() => service.DeleteAsync(defaultProfile.Id));
	}

	[Fact]
	public async Task DeleteAsync_ShouldRemoveProfile_WhenNotDefault()
	{
		var service = new DelayProfileService(new DelayProfileRepository(Context));
		await service.GetAllAsync();

		var created = await service.CreateAsync(new CreateDelayProfileRequest
		{
			Name = "Anime", PreferredProtocol = Protocol.USENET, Order = 0, Tags = new List<string> { "anime" }
		});
		Context.ChangeTracker.Clear();

		var deleted = await service.DeleteAsync(created.Id);

		Assert.Equal(created.Id, deleted.Id);
		var remaining = await service.GetAllAsync();
		Assert.DoesNotContain(remaining, p => p.Id == created.Id);
	}
}
