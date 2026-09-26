using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Library;

/// <summary>
///     Keeps the Collection row for a movie's TMDB collection in sync: creates it the first time
///     a member movie is added or refreshed, seeded with defaults from that movie, and refreshes
///     title/overview/poster from the metadata provider on every sync.
/// </summary>
public sealed class CollectionSyncService(SubmarineDbContext db, IMetadataClient metadata)
{
	/// <summary>
	///     Create or refresh the Collection row for <paramref name="movie" />'s TmdbCollectionId.
	///     A new row is seeded with RootFolderId/QualityProfileId/LanguageProfileId from the
	///     movie's first version and MinimumAvailability from the movie itself, left
	///     unmonitored until the user opts in. Returns null when the movie has no collection.
	/// </summary>
	public async Task<Collection?> SyncAsync(Movie movie, CancellationToken cancellationToken = default)
	{
		if (movie.TmdbCollectionId is not { } tmdbCollectionId)
		{
			return null;
		}

		var resource = await metadata.GetCollectionAsync(tmdbCollectionId, cancellationToken);
		var collection = await db.Collections.FirstOrDefaultAsync(x => x.TmdbCollectionId == tmdbCollectionId, cancellationToken);
		var title = resource?.Title ?? movie.CollectionTitle ?? $"Collection {tmdbCollectionId}";

		if (collection is null)
		{
			var firstVersion = movie.Versions.FirstOrDefault();
			collection = new Collection
			{
				TmdbCollectionId = tmdbCollectionId,
				Title = title,
				Overview = resource?.Overview,
				PosterUrl = resource?.PosterUrl,
				Monitored = false,
				RootFolderId = firstVersion?.RootFolderId,
				QualityProfileId = firstVersion?.QualityProfileId,
				LanguageProfileId = firstVersion?.LanguageProfileId,
				MinimumAvailability = movie.MinimumAvailability
			};
			db.Collections.Add(collection);
		}
		else
		{
			collection.Title = title;
			collection.Overview = resource?.Overview ?? collection.Overview;
			collection.PosterUrl = resource?.PosterUrl ?? collection.PosterUrl;
		}

		await db.SaveChangesAsync(cancellationToken);
		return collection;
	}
}
