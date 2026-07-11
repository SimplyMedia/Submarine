using Microsoft.EntityFrameworkCore;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;

namespace Submarine.Api.Services;

public class CalendarService
{
	private const int MaxRangeDays = 90;

	private readonly ISeriesRepository _seriesRepository;
	private readonly IMovieRepository _movieRepository;

	public CalendarService(ISeriesRepository seriesRepository, IMovieRepository movieRepository)
	{
		_seriesRepository = seriesRepository;
		_movieRepository = movieRepository;
	}

	public async Task<IReadOnlyList<CalendarItemResponse>> GetAsync(DateTimeOffset start, DateTimeOffset end)
	{
		if (end - start > TimeSpan.FromDays(MaxRangeDays))
			throw new BadRequestException($"range cannot exceed {MaxRangeDays} days");

		var episodes = await _seriesRepository.FindEpisodesByAirDateAsync(start, end);

		var seriesIds = episodes.Select(e => e.SeriesId).Distinct().ToList();

		var seriesTitles = await _seriesRepository.Query()
			.Where(s => seriesIds.Contains(s.Id))
			.ToDictionaryAsync(s => s.Id, s => s.Title);

		// SQLite can't translate DateTimeOffset comparison operators, so the range filter runs in memory
		var movies = (await _movieRepository.Query().Where(m => m.ReleaseDate != null).ToListAsync())
			.Where(m => m.ReleaseDate >= start && m.ReleaseDate <= end)
			.ToList();

		return episodes
			.Select(e => CalendarItemResponse.FromEpisode(e, seriesTitles.GetValueOrDefault(e.SeriesId, "")))
			.Concat(movies.Select(CalendarItemResponse.FromMovie))
			.OrderBy(i => i.Date)
			.ToList();
	}
}
