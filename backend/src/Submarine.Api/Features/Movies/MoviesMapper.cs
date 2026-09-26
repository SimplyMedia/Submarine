using Submarine.Api.Features.Movies;
using Submarine.Api.Features.Series;
using Submarine.Core.Entities;
using Submarine.Core.Library;

namespace Submarine.Api.Features.Movies;

/// <summary>
///     Maps movie entities to API records.
/// </summary>
public static class MoviesMapper
{
	/// <summary>Map a movie with versions and tags to the list record.</summary>
	public static MovieListItemDto ToListItem(Movie movie, IReadOnlyDictionary<int, string> rootPaths, int availabilityDelayDays, DateTime now)
		=> new(
			movie.Id,
			movie.TmdbId,
			movie.ImdbId,
			movie.Title,
			movie.SortTitle,
			movie.OriginalTitle,
			movie.Overview,
			movie.Year,
			movie.Runtime,
			movie.Studio,
			movie.PosterUrl,
			movie.BackdropUrl,
			movie.Status,
			movie.IsAnime,
			movie.Monitored,
			movie.MinimumAvailability,
			movie.TmdbCollectionId,
			movie.CollectionTitle,
			[.. movie.Genres],
			movie.Certification,
			movie.YouTubeTrailerId,
			movie.InCinemasDate,
			movie.DigitalReleaseDate,
			movie.PhysicalReleaseDate,
			movie.CreatedAt,
			[.. movie.Tags.Select(x => x.Id)],
			[.. movie.Versions.Select(x => SeriesMapper.ToDto(x, rootPaths))],
			movie.Files.Count > 0,
			movie.Files.Sum(x => x.Size),
			MovieAvailability.IsAvailable(movie, movie.MinimumAvailability, availabilityDelayDays, now));
}
