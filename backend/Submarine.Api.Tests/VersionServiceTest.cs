using Microsoft.EntityFrameworkCore;
using Submarine.Api.Exceptions;
using Submarine.Api.Services;
using Submarine.Core.Library;
using Xunit;

namespace Submarine.Api.Tests;

public class VersionServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task DeleteAsync_ShouldThrow_WhenDeletingLastVersionOfMedia()
	{
		var series = new Series { TvdbId = 1, Title = "Show", Monitored = true };
		Context.Series.Add(series);
		await Context.SaveChangesAsync();

		var version = new MediaVersion
		{
			SeriesId = series.Id, Name = "Default", Path = "/lib/Show", QualityProfileId = 1, LanguageProfileId = 1
		};
		Context.Versions.Add(version);
		await Context.SaveChangesAsync();

		var service = new VersionService(Context);

		await Assert.ThrowsAsync<BadRequestException>(() => service.DeleteAsync(version.Id, deleteFiles: false));

		Assert.Equal(1, await Context.Versions.CountAsync());
	}

	[Fact]
	public async Task DeleteAsync_ShouldRemoveVersion_WhenAnotherRemains()
	{
		var series = new Series { TvdbId = 1, Title = "Show", Monitored = true };
		Context.Series.Add(series);
		await Context.SaveChangesAsync();

		var keep = new MediaVersion
		{
			SeriesId = series.Id, Name = "1080p", Path = "/lib/1080p/Show", QualityProfileId = 1, LanguageProfileId = 1
		};
		var remove = new MediaVersion
		{
			SeriesId = series.Id, Name = "4K", Path = "/lib/4K/Show", QualityProfileId = 1, LanguageProfileId = 1
		};
		Context.Versions.AddRange(keep, remove);
		await Context.SaveChangesAsync();

		var service = new VersionService(Context);

		await service.DeleteAsync(remove.Id, deleteFiles: false);

		var remaining = await Context.Versions.AsNoTracking().SingleAsync();
		Assert.Equal(keep.Id, remaining.Id);
	}
}
