using Microsoft.EntityFrameworkCore;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Library;

/// <summary>
///     Adds the movies of a TMDB collection that are missing from the library, using the
///     collection's configured root folder, quality profile and language profile.
/// </summary>
public sealed class CollectionAddMissingService(
	SubmarineDbContext db,
	IMetadataClient metadata,
	LibraryAdder adder,
	ICommandQueue commandQueue)
{
	/// <summary>
	///     Add the movies of <paramref name="collection" /> that are not yet in the library.
	///     Returns 0 when the collection has no root folder, quality profile or language profile
	///     configured.
	/// </summary>
	/// <exception cref="KeyNotFoundException">The metadata provider has no data for the collection.</exception>
	public async Task<int> AddMissingAsync(Collection collection, CancellationToken cancellationToken = default)
	{
		if (collection.RootFolderId is null || collection.QualityProfileId is null || collection.LanguageProfileId is null)
		{
			return 0;
		}

		var resource = await metadata.GetCollectionAsync(collection.TmdbCollectionId, cancellationToken)
			?? throw new KeyNotFoundException($"Collection {collection.TmdbCollectionId} not found on the metadata provider");

		var existing = await db.Movies.Select(x => x.TmdbId).ToListAsync(cancellationToken);
		var added = 0;
		foreach (var movie in resource.Movies)
		{
			if (existing.Contains(movie.TmdbId))
			{
				continue;
			}

			var entity = await adder.AddMovieAsync(
				new AddMovieOptions(
					movie.TmdbId,
					movie.Title,
					collection.RootFolderId.Value,
					false,
					collection.Monitored,
					collection.MinimumAvailability,
					[],
					[
						new VersionOptions(
							"main",
							collection.QualityProfileId.Value,
							collection.LanguageProfileId.Value,
							null)
					],
					collection.SearchOnAdd),
				cancellationToken);
			if (collection.SearchOnAdd)
			{
				await commandQueue.EnqueueAsync(new MovieSearchCommand([entity.Id]), CommandTrigger.SYSTEM, CommandPriority.LOW, cancellationToken);
			}

			added++;
		}

		return added;
	}
}
