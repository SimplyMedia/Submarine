using Microsoft.EntityFrameworkCore;
using Submarine.Api.Clients;
using Submarine.Api.Models.Database;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;
using Submarine.Core.ImportList;
using Submarine.Core.Library;

namespace Submarine.Api.Jobs;

/// <summary>
///     Periodically syncs all enabled import lists, adding media which is not in the library yet
/// </summary>
public sealed class ImportListSyncJob : IScheduledJob
{
	public string Name => "ImportListSync";

	public TimeSpan Interval => TimeSpan.FromHours(6);

	public async Task ExecuteAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
	{
		var context = scopedProvider.GetRequiredService<SubmarineDatabaseContext>();
		var fetcher = scopedProvider.GetRequiredService<IImportListFetcher>();
		var seriesService = scopedProvider.GetRequiredService<SeriesService>();
		var movieService = scopedProvider.GetRequiredService<MovieService>();
		var metadataClient = scopedProvider.GetRequiredService<IMetadataClient>();
		var logger = scopedProvider.GetRequiredService<ILogger<ImportListSyncJob>>();

		var lists = await context.ImportLists.AsNoTracking()
			.Where(l => l.Enable)
			.ToListAsync(cancellationToken);

		foreach (var list in lists)
		{
			IReadOnlyList<ImportListItem> items;

			try
			{
				items = await fetcher.FetchAsync(list, cancellationToken);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Fetching import list {Name} failed", list.Name);
				continue;
			}

			foreach (var item in items)
			{
				try
				{
					await SyncItemAsync(context, seriesService, movieService, metadataClient, list, item, logger,
						cancellationToken);
				}
				catch (Exception ex)
				{
					logger.LogWarning(ex, "Adding import list item {Title} from list {Name} failed", item.Title,
						list.Name);
				}
			}
		}
	}

	private static async Task SyncItemAsync(SubmarineDatabaseContext context, SeriesService seriesService,
		MovieService movieService, IMetadataClient metadataClient, ImportList list, ImportListItem item,
		ILogger logger, CancellationToken cancellationToken)
	{
		if (item.IsSeries)
		{
			if (item.TmdbId != null &&
			    await context.Series.AsNoTracking().AnyAsync(s => s.TmdbId == item.TmdbId, cancellationToken))
				return;

			var tvdbId = item.TvdbId ?? await ResolveSeriesTvdbIdAsync(metadataClient, item, logger,
				cancellationToken);

			if (tvdbId == null)
				return;

			if (await context.Series.AsNoTracking().AnyAsync(s => s.TvdbId == tvdbId, cancellationToken))
				return;

			await seriesService.AddAsync(new AddSeriesRequest
			{
				TvdbId = tvdbId.Value,
				RootFolderId = list.RootFolderId,
				QualityProfileId = list.QualityProfileId,
				LanguageProfileId = list.LanguageProfileId,
				Type = item.AniListId != null ? SeriesType.ANIME : null,
				Monitored = list.Monitored,
				Tags = list.Tags
			});
		}
		else
		{
			var tmdbId = item.TmdbId ?? await ResolveMovieTmdbIdAsync(metadataClient, item, logger,
				cancellationToken);

			if (tmdbId == null)
				return;

			if (await context.Movies.AsNoTracking().AnyAsync(m => m.TmdbId == tmdbId, cancellationToken))
				return;

			await movieService.AddAsync(new AddMovieRequest
			{
				TmdbId = tmdbId.Value,
				RootFolderId = list.RootFolderId,
				QualityProfileId = list.QualityProfileId,
				LanguageProfileId = list.LanguageProfileId,
				IsAnime = item.AniListId != null ? true : null,
				Monitored = list.Monitored,
				Tags = list.Tags
			});
		}
	}

	private static async Task<int?> ResolveSeriesTvdbIdAsync(IMetadataClient metadataClient, ImportListItem item,
		ILogger logger, CancellationToken cancellationToken)
	{
		var results = await metadataClient.SearchSeriesAsync(item.Title, cancellationToken: cancellationToken);
		var match = results.FirstOrDefault(r => r.Title == item.Title);

		if (match == null)
			logger.LogDebug("No exact series title match for import list item {Title}, skipping", item.Title);

		return match?.TvdbId;
	}

	private static async Task<int?> ResolveMovieTmdbIdAsync(IMetadataClient metadataClient, ImportListItem item,
		ILogger logger, CancellationToken cancellationToken)
	{
		var results = await metadataClient.SearchMoviesAsync(item.Title, cancellationToken);
		var match = results.FirstOrDefault(r => r.Title == item.Title);

		if (match == null)
			logger.LogDebug("No exact movie title match for import list item {Title}, skipping", item.Title);

		return match?.TmdbId;
	}
}
