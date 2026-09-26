using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Modules;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Downloads;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.History;

/// <summary>
///     Library history: grabs, imports, upgrades, renames, deletions and failures.
/// </summary>
public sealed class HistoryModule : IEndpointModule, IServiceModule
{

	public void Register(IServiceCollection services, IConfiguration configuration)
		=> services.AddScoped<HistoryFailureService>();
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/history");
		group.MapGet("/", ListAsync);
		group.MapGet("/since", SinceAsync);
		group.MapGet("/series", SeriesAsync);
		group.MapGet("/movie", MovieAsync);
		group.MapPost("/failed/{id:int}", MarkFailedAsync);
	}

	private static async Task<Ok<PagedResult<HistoryEventDto>>> ListAsync(
		SubmarineDbContext db,
		HistoryEventType? eventType,
		int? seriesId,
		int? episodeId,
		int? movieId,
		string? downloadId,
		string? q,
		[AsParameters] PagingQuery query,
		CancellationToken cancellationToken)
	{
		var events = Base(db);
		if (eventType is not null)
		{
			events = events.Where(x => x.Type == eventType);
		}

		if (seriesId is not null)
		{
			events = events.Where(x => x.SeriesId == seriesId);
		}

		if (episodeId is not null)
		{
			events = events.Where(x => x.EpisodeId == episodeId);
		}

		if (movieId is not null)
		{
			events = events.Where(x => x.MovieId == movieId);
		}

		if (downloadId is not null)
		{
			events = events.Where(x => x.DownloadId == downloadId);
		}

		if (!string.IsNullOrWhiteSpace(q))
		{
			events = events.Where(x => x.SourceTitle.Contains(q));
		}

		var paged = await PagedResult<Core.Entities.HistoryEvent>.CreateAsync(events, query, cancellationToken);
		return TypedResults.Ok(new PagedResult<HistoryEventDto>([.. paged.Items.Select(ToDto)], paged.Page, paged.PageSize, paged.TotalCount));
	}

	private static async Task<Ok<IReadOnlyList<HistoryEventDto>>> SinceAsync(
		DateTime date,
		HistoryEventType? eventType,
		SubmarineDbContext db,
		CancellationToken cancellationToken)
	{
		var events = Base(db).Where(x => x.Date >= date);
		if (eventType is not null)
		{
			events = events.Where(x => x.Type == eventType);
		}

		return TypedResults.Ok<IReadOnlyList<HistoryEventDto>>([.. (await events.ToListAsync(cancellationToken)).Select(ToDto)]);
	}

	private static async Task<Ok<IReadOnlyList<HistoryEventDto>>> SeriesAsync(
		int seriesId,
		int? seasonNumber,
		HistoryEventType? eventType,
		SubmarineDbContext db,
		CancellationToken cancellationToken)
	{
		var events = Base(db).Where(x => x.SeriesId == seriesId);
		if (eventType is not null)
		{
			events = events.Where(x => x.Type == eventType);
		}

		if (seasonNumber is not null)
		{
			events = events.Where(x => x.Episode != null && x.Episode.SeasonNumber == seasonNumber);
		}

		var list = await events.ToListAsync(cancellationToken);
		return TypedResults.Ok<IReadOnlyList<HistoryEventDto>>([.. list.Select(ToDto)]);
	}

	private static async Task<Ok<IReadOnlyList<HistoryEventDto>>> MovieAsync(int movieId, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var list = await Base(db).Where(x => x.MovieId == movieId).ToListAsync(cancellationToken);
		return TypedResults.Ok<IReadOnlyList<HistoryEventDto>>([.. list.Select(ToDto)]);
	}

	private static async Task<Results<Ok<HistoryEventDto>, NotFound, BadRequest<string>>> MarkFailedAsync(
		int id,
		HistoryFailureService failureService,
		CancellationToken cancellationToken)
	{
		var result = await failureService.MarkFailedAsync(id, cancellationToken);
		if (result.FailedEvent is { } failedEvent)
			return TypedResults.Ok(ToDto(failedEvent));
		return result.Error is { } error ? TypedResults.BadRequest(error) : TypedResults.NotFound();
	}


	private static IQueryable<Core.Entities.HistoryEvent> Base(SubmarineDbContext db)
		=> db.HistoryEvents
			.AsNoTracking()
			.Include(x => x.Series)
			.Include(x => x.Episode)
			.Include(x => x.Movie)
			.OrderByDescending(x => x.Date);

	private static HistoryEventDto ToDto(Core.Entities.HistoryEvent history)
		=> new(
			history.Id,
			history.Type.ToString(),
			history.SeriesId,
			history.Series?.Title,
			history.EpisodeId,
			history.Episode?.Title,
			history.MovieId,
			history.Movie?.Title,
			history.MediaVersionId,
			history.SourceTitle,
			history.Quality,
			history.Languages,
			history.DownloadId,
			history.Data,
			history.Date);
}

/// <summary>One history entry.</summary>
/// <param name="Id">Id.</param>
/// <param name="Type">Event type.</param>
/// <param name="SeriesId">Related series.</param>
/// <param name="SeriesTitle">Related series title.</param>
/// <param name="EpisodeId">Related episode.</param>
/// <param name="EpisodeTitle">Related episode title.</param>
/// <param name="MovieId">Related movie.</param>
/// <param name="MovieTitle">Related movie title.</param>
/// <param name="MediaVersionId">Related version.</param>
/// <param name="SourceTitle">Title of the source release or file.</param>
/// <param name="Quality">Quality, if known.</param>
/// <param name="Languages">Languages, if known.</param>
/// <param name="DownloadId">Download client id, if any.</param>
/// <param name="Data">Additional data as JSON.</param>
/// <param name="Date">When the event happened.</param>
public sealed record HistoryEventDto(
	int Id,
	string Type,
	int? SeriesId,
	string? SeriesTitle,
	int? EpisodeId,
	string? EpisodeTitle,
	int? MovieId,
	string? MovieTitle,
	int? MediaVersionId,
	string SourceTitle,
	QualityModel? Quality,
	List<Language>? Languages,
	string? DownloadId,
	string? Data,
	DateTime Date);
