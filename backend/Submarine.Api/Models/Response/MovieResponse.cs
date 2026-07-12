using Submarine.Core.Library;

namespace Submarine.Api.Models.Response;

/// <summary>
///     A Movie including its versions
/// </summary>
public record MovieResponse(
	int Id,
	int TmdbId,
	string? ImdbId,
	string Title,
	string? SortTitle,
	string? Overview,
	int? Year,
	int? Runtime,
	string? Studio,
	DateTimeOffset? ReleaseDate,
	bool IsAnime,
	bool Monitored,
	List<string> Tags,
	IReadOnlyList<MediaVersionResponse> Versions)
{
	public static MovieResponse FromMovie(Movie movie)
		=> new(movie.Id, movie.TmdbId, movie.ImdbId, movie.Title, movie.SortTitle, movie.Overview, movie.Year,
			movie.Runtime, movie.Studio, movie.ReleaseDate, movie.IsAnime, movie.Monitored, movie.Tags,
			movie.Versions.Select(MediaVersionResponse.FromVersion).ToList());
}
