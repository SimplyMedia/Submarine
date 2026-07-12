using Microsoft.EntityFrameworkCore;
using Submarine.Api.Models.Request;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.Library;
using Xunit;

namespace Submarine.Api.Tests;

public class EpisodeServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task BatchMonitorAsync_ShouldUpdateMonitoredOnAllGivenEpisodes()
	{
		var series = new Series { TvdbId = 1, Title = "Show" };
		Context.Series.Add(series);
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		var episode1 = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 1, Monitored = false };
		var episode2 = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 2, Monitored = false };
		var episode3 = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 3, Monitored = false };
		Context.Episodes.AddRange(episode1, episode2, episode3);
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);
		Context.ChangeTracker.Clear();

		var service = new EpisodeService(new SeriesRepository(Context));

		await service.BatchMonitorAsync(new BatchMonitorEpisodesRequest
		{
			EpisodeIds = new List<int> { episode1.Id, episode2.Id }, Monitored = true
		});

		var episodes = await Context.Episodes.AsNoTracking().OrderBy(e => e.EpisodeNumber).ToListAsync(TestContext.Current.CancellationToken);
		Assert.True(episodes[0].Monitored);
		Assert.True(episodes[1].Monitored);
		Assert.False(episodes[2].Monitored);
	}
}
