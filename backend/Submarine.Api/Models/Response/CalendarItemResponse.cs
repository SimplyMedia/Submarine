using Submarine.Core.Library;

namespace Submarine.Api.Models.Response;

/// <summary>
///     A single item on the calendar, either an upcoming episode or movie release
/// </summary>
public record CalendarItemResponse(
	string Type,
	int Id,
	string Title,
	CalendarEpisodeInfo? EpisodeInfo,
	DateTimeOffset Date,
	bool Monitored,
	bool HasFile)
{
	public static CalendarItemResponse FromEpisode(Episode episode, string seriesTitle)
		=> new("episode", episode.Id, seriesTitle,
			new CalendarEpisodeInfo(episode.SeasonNumber, episode.EpisodeNumber, episode.Title),
			episode.AirDate!.Value, episode.Monitored, episode.EpisodeFileId != null);

	public static CalendarItemResponse FromMovie(Movie movie)
		=> new("movie", movie.Id, movie.Title, null, movie.ReleaseDate!.Value, movie.Monitored,
			movie.MovieFileId != null);
}

/// <summary>
///     Episode numbering shown on a <see cref="CalendarItemResponse" />
/// </summary>
public record CalendarEpisodeInfo(int SeasonNumber, int EpisodeNumber, string? EpisodeTitle);
