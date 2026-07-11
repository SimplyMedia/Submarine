using Microsoft.EntityFrameworkCore;
using Submarine.Api.Models.Database;
using Submarine.Api.Services;

namespace Submarine.Api.Jobs;

/// <summary>
///     Periodically refreshes metadata of all monitored series and movies
/// </summary>
public sealed class MetadataRefreshJob : IScheduledJob
{
	public string Name => "MetadataRefresh";

	public TimeSpan Interval => TimeSpan.FromHours(12);

	public async Task ExecuteAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
	{
		var context = scopedProvider.GetRequiredService<SubmarineDatabaseContext>();
		var refreshService = scopedProvider.GetRequiredService<SeriesRefreshService>();
		var logger = scopedProvider.GetRequiredService<ILogger<MetadataRefreshJob>>();

		var series = await context.Series.Where(s => s.Monitored).ToListAsync(cancellationToken);

		foreach (var item in series)
		{
			try
			{
				await refreshService.RefreshSeriesAsync(item, cancellationToken);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Refreshing series {SeriesId} failed", item.Id);
			}
		}

		var movies = await context.Movies.Where(m => m.Monitored).ToListAsync(cancellationToken);

		foreach (var item in movies)
		{
			try
			{
				await refreshService.RefreshMovieAsync(item, cancellationToken);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Refreshing movie {MovieId} failed", item.Id);
			}
		}
	}
}
