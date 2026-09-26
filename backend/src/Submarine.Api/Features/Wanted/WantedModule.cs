using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Features.Series;
using Submarine.Api.Modules;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Profiles;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Wanted;

/// <summary>
///     Wanted endpoints: missing episodes and movies, and files below the quality cutoff.
/// </summary>
public sealed class WantedModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/wanted");
		group.MapGet("/missing", MissingAsync);
		group.MapGet("/cutoff", CutoffAsync);
		group.MapPost("/missing/search", MissingSearchAsync);
		group.MapPost("/cutoff/search", CutoffSearchAsync);
	}

	private static async Task<Ok<PagedResult<WantedItemDto>>> MissingAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		[FromQuery] bool includeUnmonitored = false,
		CancellationToken cancellationToken = default)
	{
		var now = DateTime.UtcNow;
		var indexerConfig = await db.IndexerConfig.AsNoTracking().SingleAsync(cancellationToken);

		var episodes = await db.Episodes.AsNoTracking()
			.Include(x => x.Series).ThenInclude(x => x.Versions)
			.Include(x => x.Files)
			.Where(x => x.AirDateUtc != null && x.AirDateUtc <= now)
			.Where(x => x.Files.Count == 0)
			.ToListAsync(cancellationToken);
		var movies = await db.Movies.AsNoTracking()
			.Include(x => x.Versions)
			.Include(x => x.Files)
			.Where(x => x.Files.Count == 0)
			.ToListAsync(cancellationToken);

		var items = new List<WantedItemDto>();
		items.AddRange(episodes
			.Where(x => includeUnmonitored || (x.Monitored && x.Series.Monitored))
			.Where(x => x.Series.Versions.Any(v => v.Monitored))
			.Select(x => new WantedItemDto(
				"episode",
				x.Id,
				x.SeriesId,
				null,
				x.Series.Title,
				x.Title,
				x.SeasonNumber,
				x.EpisodeNumber,
				x.AirDateUtc,
				x.Monitored && x.Series.Monitored,
				null,
				null,
				null,
				null)));
		items.AddRange(movies
			.Where(x => includeUnmonitored || x.Monitored)
			.Where(x => x.Versions.Any(v => v.Monitored))
			.Where(x => MovieAvailabilityOf(x, indexerConfig.AvailabilityDelayDays, now))
			.Select(x => new WantedItemDto(
				"movie",
				x.Id,
				null,
				x.Id,
				x.Title,
				null,
				null,
				null,
				x.PhysicalReleaseDate ?? x.DigitalReleaseDate ?? x.InCinemasDate,
				x.Monitored,
				null,
				null,
				null,
				null)));

		return TypedResults.Ok(await PagedResult<WantedItemDto>.CreateAsync(
			items.AsQueryable(),
			query,
			cancellationToken));
	}

	private static async Task<Ok<PagedResult<WantedItemDto>>> CutoffAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		[FromQuery] bool includeUnmonitored = false,
		CancellationToken cancellationToken = default)
	{
		var profiles = await db.QualityProfiles.AsNoTracking().ToListAsync(cancellationToken);
		var profileById = profiles.ToDictionary(x => x.Id);

		var episodeFiles = await db.EpisodeFiles.AsNoTracking()
			.Include(x => x.Series).ThenInclude(x => x.Versions)
			.Include(x => x.Episodes)
			.ToListAsync(cancellationToken);
		var movieFiles = await db.MovieFiles.AsNoTracking()
			.Include(x => x.Movie).ThenInclude(x => x.Versions)
			.ToListAsync(cancellationToken);

		var items = new List<WantedItemDto>();
		foreach (var file in episodeFiles)
		{
			var version = file.Series.Versions.FirstOrDefault(v => v.Id == file.MediaVersionId);
			if (version is null || !profileById.TryGetValue(version.QualityProfileId, out var profile))
			{
				continue;
			}

			if (profile.MeetsCutoff(file.Quality))
			{
				continue;
			}

			if (!includeUnmonitored && (!file.Series.Monitored || file.Episodes.All(x => !x.Monitored)))
			{
				continue;
			}

			var first = file.Episodes.OrderBy(x => x.SeasonNumber).ThenBy(x => x.EpisodeNumber).First();
			items.Add(new WantedItemDto(
				"episode",
				first.Id,
				file.SeriesId,
				null,
				file.Series.Title,
				first.Title,
				first.SeasonNumber,
				first.EpisodeNumber,
				first.AirDateUtc,
				file.Series.Monitored,
				file.Id,
				file.Size,
				file.Quality,
				version.Name));
		}

		foreach (var file in movieFiles)
		{
			var version = file.Movie.Versions.FirstOrDefault(v => v.Id == file.MediaVersionId);
			if (version is null || !profileById.TryGetValue(version.QualityProfileId, out var profile))
			{
				continue;
			}

			if (profile.MeetsCutoff(file.Quality))
			{
				continue;
			}

			if (!includeUnmonitored && !file.Movie.Monitored)
			{
				continue;
			}

			items.Add(new WantedItemDto(
				"movie",
				file.MovieId,
				null,
				file.MovieId,
				file.Movie.Title,
				null,
				null,
				null,
				file.Movie.PhysicalReleaseDate ?? file.Movie.DigitalReleaseDate ?? file.Movie.InCinemasDate,
				file.Movie.Monitored,
				file.Id,
				file.Size,
				file.Quality,
				version.Name));
		}

		return TypedResults.Ok(await PagedResult<WantedItemDto>.CreateAsync(
			items.AsQueryable(),
			query,
			cancellationToken));
	}

	private static Task<Created<Command>> MissingSearchAsync(ICommandQueue queue, CancellationToken cancellationToken)
		=> SeriesModule.EnqueueAsync(queue, new MissingSearchCommand(), cancellationToken);

	private static Task<Created<Command>> CutoffSearchAsync(ICommandQueue queue, CancellationToken cancellationToken)
		=> SeriesModule.EnqueueAsync(queue, new CutoffUnmetSearchCommand(), cancellationToken);

	private static bool MovieAvailabilityOf(Movie movie, int availabilityDelayDays, DateTime now)
	{
		var date = movie.MinimumAvailability == Core.Enums.MinimumAvailability.IN_CINEMAS
			? movie.InCinemasDate
			: movie.PhysicalReleaseDate ?? movie.DigitalReleaseDate ?? movie.InCinemasDate;
		return date.HasValue && date.Value.AddDays(availabilityDelayDays) <= now;
	}
}

/// <summary>One wanted missing or cutoff item.</summary>
/// <param name="Type">Episode or movie.</param>
/// <param name="Id">Episode or movie id.</param>
/// <param name="SeriesId">Series id, episodes only.</param>
/// <param name="MovieId">Movie id, movies only.</param>
/// <param name="Title">Series or movie title.</param>
/// <param name="SubTitle">Episode title, episodes only.</param>
/// <param name="SeasonNumber">Season number, episodes only.</param>
/// <param name="EpisodeNumber">Episode number, episodes only.</param>
/// <param name="AirDateUtc">Air or release date.</param>
/// <param name="Monitored">Whether the item is monitored.</param>
/// <param name="FileId">File id, cutoff items only.</param>
/// <param name="Size">File size in bytes, cutoff items only.</param>
/// <param name="Quality">File quality, cutoff items only.</param>
/// <param name="VersionName">Version the file belongs to, cutoff items only.</param>
public sealed record WantedItemDto(
	string Type,
	int Id,
	int? SeriesId,
	int? MovieId,
	string Title,
	string? SubTitle,
	int? SeasonNumber,
	int? EpisodeNumber,
	DateTime? AirDateUtc,
	bool Monitored,
	int? FileId,
	long? Size,
	QualityModel? Quality,
	string? VersionName);
