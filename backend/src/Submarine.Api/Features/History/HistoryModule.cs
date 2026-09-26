using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
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
public sealed class HistoryModule : IEndpointModule
{
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
		SubmarineDbContext db,
		IDownloadClientProvider clientProvider,
		ICommandQueue commandQueue,
		IEventBus eventBus,
		TimeProvider timeProvider,
		CancellationToken cancellationToken)
	{
		var grabbed = await db.HistoryEvents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (grabbed is null)
		{
			return TypedResults.NotFound();
		}

		if (grabbed.Type != HistoryEventType.GRABBED || grabbed.DownloadId is null)
		{
			return TypedResults.BadRequest("Only a grabbed history entry with a download id can be marked as failed");
		}

		var download = await db.TrackedDownloads.FirstOrDefaultAsync(x => x.DownloadId == grabbed.DownloadId, cancellationToken);
		var now = timeProvider.GetUtcNow().UtcDateTime;
		var episodeIds = download?.EpisodeIds ?? (grabbed.EpisodeId is { } episodeId ? [episodeId] : []);

		await eventBus.PublishAsync(
			new DownloadFailedEvent(grabbed.DownloadId, grabbed.SourceTitle, grabbed.SeriesId, grabbed.MovieId, episodeIds, grabbed.MediaVersionId, "Marked as failed"),
			cancellationToken);

		var failedEvent = new Core.Entities.HistoryEvent
		{
			Type = HistoryEventType.FAILED,
			SeriesId = grabbed.SeriesId,
			EpisodeId = grabbed.EpisodeId,
			MovieId = grabbed.MovieId,
			MediaVersionId = grabbed.MediaVersionId,
			SourceTitle = grabbed.SourceTitle,
			Quality = grabbed.Quality,
			Languages = grabbed.Languages,
			DownloadId = grabbed.DownloadId,
			Date = now
		};
		db.HistoryEvents.Add(failedEvent);

		db.BlocklistItems.Add(new BlocklistItem
		{
			ReleaseTitle = grabbed.SourceTitle,
			Protocol = download?.Protocol ?? Protocol.BITTORRENT,
			SeriesId = grabbed.SeriesId,
			MovieId = grabbed.MovieId,
			EpisodeIds = episodeIds,
			Reason = "Marked as failed",
			Date = now
		});

		if (download is not null)
		{
			var client = await clientProvider.GetAsync(download.DownloadClientId, cancellationToken);
			if (client is not null)
			{
				try
				{
					await client.Instance.RemoveAsync(download.DownloadId, deleteData: true, cancellationToken);
				}
				catch (Core.Download.DownloadClientException)
				{
					// best effort
				}
			}

			db.TrackedDownloads.Remove(download);
		}

		if (grabbed.SeriesId is not null && episodeIds.Count > 0)
		{
			await commandQueue.EnqueueAsync(new EpisodeSearchCommand(episodeIds), CommandTrigger.MANUAL, CommandPriority.HIGH, cancellationToken);
		}
		else if (grabbed.MovieId is { } movieId)
		{
			await commandQueue.EnqueueAsync(new MovieSearchCommand([movieId]), CommandTrigger.MANUAL, CommandPriority.HIGH, cancellationToken);
		}

		await db.SaveChangesAsync(cancellationToken);
		await eventBus.PublishAsync(new QueueUpdatedEvent(), cancellationToken);
		return TypedResults.Ok(ToDto(failedEvent));
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
