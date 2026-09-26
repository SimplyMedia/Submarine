using System.Security.Cryptography;
using System.Text;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Calendar;

/// <summary>
///     Calendar endpoints: JSON calendar for the UI and an anonymous iCal feed.
/// </summary>
public sealed class CalendarModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/calendar");
		group.MapGet("/", CalendarAsync);
		group.MapGet("/feed.ics", FeedAsync).AllowAnonymous().Produces(StatusCodes.Status200OK, contentType: "text/calendar");
	}

	private static async Task<Ok<List<CalendarEventDto>>> CalendarAsync(
		SubmarineDbContext db,
		[FromQuery] DateTime? start,
		[FromQuery] DateTime? end,
		[FromQuery] bool unmonitored = false,
		[FromQuery] string? tags = null,
		CancellationToken cancellationToken = default)
	{
		var (from, to) = NormalizeRange(start, end);
		var tagIds = ParseTags(tags);

		var episodes = await db.Episodes.AsNoTracking()
			.Include(x => x.Series).ThenInclude(x => x.Tags)
			.Include(x => x.Files)
			.Where(x => x.AirDateUtc != null && x.AirDateUtc >= from && x.AirDateUtc <= to)
			.Where(x => unmonitored || (x.Monitored && x.Series.Monitored))
			.OrderBy(x => x.AirDateUtc)
			.ToListAsync(cancellationToken);
		var movies = await db.Movies.AsNoTracking()
			.Include(x => x.Tags)
			.Include(x => x.Files)
			.Where(x => (x.InCinemasDate != null || x.DigitalReleaseDate != null || x.PhysicalReleaseDate != null))
			.Where(x => unmonitored || x.Monitored)
			.ToListAsync(cancellationToken);
		var activeDownloads = await db.TrackedDownloads.AsNoTracking()
			.Where(x => x.State == TrackedDownloadState.DOWNLOADING || x.State == TrackedDownloadState.IMPORT_PENDING)
			.Select(x => new { x.EpisodeIds, x.MovieId })
			.ToListAsync(cancellationToken);
		var downloadingEpisodeIds = activeDownloads.SelectMany(x => x.EpisodeIds).ToHashSet();
		var downloadingMovieIds = activeDownloads.Where(x => x.MovieId != null).Select(x => x.MovieId!.Value).ToHashSet();

		var result = new List<CalendarEventDto>();
		foreach (var episode in episodes.Where(x => MatchesTags(x.Series.Tags, tagIds)))
		{
			result.Add(new CalendarEventDto(
				"episode",
				episode.Id,
				episode.SeriesId,
				episode.Series.Title,
				episode.SeasonNumber,
				episode.EpisodeNumber,
				episode.Title,
				episode.AirDateUtc!.Value,
				"aired",
				episode.Monitored && episode.Series.Monitored,
				episode.Files.Count > 0,
				downloadingEpisodeIds.Contains(episode.Id)));
		}

		foreach (var movie in movies.Where(x => MatchesTags(x.Tags, tagIds)))
		{
			var hasFile = movie.Files.Count > 0;
			var downloading = downloadingMovieIds.Contains(movie.Id);
			if (movie.InCinemasDate is { } inCinemas && inCinemas >= from && inCinemas <= to)
			{
				result.Add(new CalendarEventDto(
					"movie",
					movie.Id,
					movie.Id,
					movie.Title,
					null,
					null,
					null,
					inCinemas,
					"inCinemas",
					movie.Monitored,
					hasFile,
					downloading));
			}

			if (movie.DigitalReleaseDate is { } digital && digital >= from && digital <= to)
			{
				result.Add(new CalendarEventDto(
					"movie",
					movie.Id,
					movie.Id,
					movie.Title,
					null,
					null,
					null,
					digital,
					"digital",
					movie.Monitored,
					hasFile,
					downloading));
			}

			if (movie.PhysicalReleaseDate is { } physical && physical >= from && physical <= to)
			{
				result.Add(new CalendarEventDto(
					"movie",
					movie.Id,
					movie.Id,
					movie.Title,
					null,
					null,
					null,
					physical,
					"physical",
					movie.Monitored,
					hasFile,
					downloading));
			}
		}

		return TypedResults.Ok(result.OrderBy(x => x.Date).ToList());
	}

	private static async Task<IResult> FeedAsync(
		SubmarineDbContext db,
		[FromQuery] string? token,
		[FromQuery] int pastDays = 7,
		[FromQuery] int futureDays = 30,
		[FromQuery] string? tags = null,
		[FromQuery] bool unmonitored = false,
		CancellationToken cancellationToken = default)
	{
		var feedToken = await db.GeneralConfig.AsNoTracking().Select(x => x.FeedToken).FirstAsync(cancellationToken);
		if (string.IsNullOrEmpty(token) || !ConstantTimeEquals(token, feedToken))
		{
			return Results.Unauthorized();
		}

		var (from, to) = NormalizeRange(DateTime.UtcNow.AddDays(-Math.Clamp(pastDays, 0, 365)), DateTime.UtcNow.AddDays(Math.Clamp(futureDays, 0, 365)));
		var tagIds = ParseTags(tags);

		var episodes = await db.Episodes.AsNoTracking()
			.Include(x => x.Series).ThenInclude(x => x.Tags)
			.Where(x => x.AirDateUtc != null && x.AirDateUtc >= from && x.AirDateUtc <= to)
			.Where(x => unmonitored || (x.Monitored && x.Series.Monitored))
			.OrderBy(x => x.AirDateUtc)
			.ToListAsync(cancellationToken);
		var movies = await db.Movies.AsNoTracking()
			.Include(x => x.Tags)
			.Where(x => unmonitored || x.Monitored)
			.ToListAsync(cancellationToken);

		var calendar = new Ical.Net.Calendar();
		foreach (var episode in episodes.Where(x => MatchesTags(x.Series.Tags, tagIds)))
		{
			calendar.Events.Add(new CalendarEvent
			{
				Uid = $"episode-{episode.Id}",
				Summary = $"{episode.Series.Title} {episode.SeasonNumber:00}x{episode.EpisodeNumber:00}",
				Description = episode.Title,
				Start = new CalDateTime(DateTime.SpecifyKind(episode.AirDateUtc!.Value, DateTimeKind.Utc)),
				End = new CalDateTime(DateTime.SpecifyKind(episode.AirDateUtc!.Value.AddMinutes(episode.Runtime ?? 30), DateTimeKind.Utc))
			});
		}

		foreach (var movie in movies.Where(x => MatchesTags(x.Tags, tagIds)))
		{
			foreach (var (date, kind) in new (DateTime?, string)[]
				{
					(movie.InCinemasDate, "in cinemas"),
					(movie.DigitalReleaseDate, "digital release"),
					(movie.PhysicalReleaseDate, "physical release")
				})
			{
				if (date is not { } releaseDate || releaseDate < from || releaseDate > to)
				{
					continue;
				}

				calendar.Events.Add(new CalendarEvent
				{
					Uid = $"movie-{movie.Id}-{kind}",
					Summary = $"{movie.Title} ({kind})",
					Start = new CalDateTime(DateTime.SpecifyKind(releaseDate, DateTimeKind.Utc)),
					End = new CalDateTime(DateTime.SpecifyKind(releaseDate.AddHours(2), DateTimeKind.Utc))
				});
			}
		}

		var serialized = new CalendarSerializer().SerializeToString(calendar);
		return Results.Text(serialized, "text/calendar", Encoding.UTF8);
	}

	internal static (DateTime From, DateTime To) NormalizeRange(DateTime? start, DateTime? end)
	{
		var today = DateTime.UtcNow.Date;
		var from = (start ?? today).ToUniversalTime();
		var to = (end ?? today.AddDays(7)).ToUniversalTime();
		if (to < from)
		{
			(from, to) = (to, from);
		}

		if (to > from.AddDays(90))
		{
			to = from.AddDays(90);
		}

		return (from, to);
	}

	private static List<int> ParseTags(string? tags)
		=> string.IsNullOrWhiteSpace(tags)
			? []
			: tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(x => int.TryParse(x, out var id) ? id : -1)
				.Where(x => x > 0)
				.ToList();

	private static bool MatchesTags(IEnumerable<Tag> entityTags, List<int> tagIds)
		=> tagIds.Count == 0 || entityTags.Any(x => tagIds.Contains(x.Id));

	private static bool ConstantTimeEquals(string candidate, string expected)
	{
		var candidateBytes = Encoding.UTF8.GetBytes(candidate);
		var expectedBytes = Encoding.UTF8.GetBytes(expected);
		return candidateBytes.Length == expectedBytes.Length
			&& CryptographicOperations.FixedTimeEquals(candidateBytes, expectedBytes);
	}
}

/// <summary>One calendar entry.</summary>
/// <param name="Type">Episode or movie.</param>
/// <param name="Id">Episode or movie id.</param>
/// <param name="SeriesOrMovieId">Owning series or movie id.</param>
/// <param name="Title">Series or movie title.</param>
/// <param name="SeasonNumber">Season number, episodes only.</param>
/// <param name="EpisodeNumber">Episode number, episodes only.</param>
/// <param name="EpisodeTitle">Episode title, episodes only.</param>
/// <param name="Date">Air or release date, UTC.</param>
/// <param name="Kind">Aired, inCinemas, digital or physical.</param>
/// <param name="Monitored">Whether the item is monitored.</param>
/// <param name="HasFile">Whether the episode or movie has a file on disk.</param>
/// <param name="Downloading">Whether a tracked download is in progress or pending import for this item.</param>
public sealed record CalendarEventDto(
	string Type,
	int Id,
	int SeriesOrMovieId,
	string Title,
	int? SeasonNumber,
	int? EpisodeNumber,
	string? EpisodeTitle,
	DateTime Date,
	string Kind,
	bool Monitored,
	bool HasFile,
	bool Downloading);
