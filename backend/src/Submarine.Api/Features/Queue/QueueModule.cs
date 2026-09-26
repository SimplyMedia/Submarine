using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Download;
using Submarine.Core.Languages;
using Submarine.Core.Quality;
using Submarine.Core.Events;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Downloads;
using Submarine.Infrastructure.Import;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Queue;

/// <summary>
///     The download queue: tracked downloads awaiting completion or import.
/// </summary>
public sealed class QueueModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/queue");
		group.MapGet("/", ListAsync);
		group.MapGet("/status", StatusAsync);
		group.MapGet("/details", DetailsAsync);
		group.MapDelete("/bulk", BulkDeleteAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
		group.MapPost("/grab/{id:int}", RetryAsync);
		group.MapPost("/{id:int}/import", ForceImportAsync);
	}

	private static async Task<Ok<PagedResult<QueueItemDto>>> ListAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		CancellationToken cancellationToken)
	{
		var items = await Base(db).ToListAsync(cancellationToken);
		return TypedResults.Ok(await PagedResult<QueueItemDto>.CreateAsync(
			items.Select(ToDto).AsQueryable(),
			query,
			cancellationToken));
	}

	private static async Task<Ok<QueueStatusDto>> StatusAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var items = await db.TrackedDownloads.AsNoTracking().ToListAsync(cancellationToken);
		return TypedResults.Ok(new QueueStatusDto(
			items.Count,
			items.Count(x => x.Status == TrackedDownloadStatus.FAILED || x.State is TrackedDownloadState.FAILED or TrackedDownloadState.FAILED_PENDING),
			items.Count(x => x.Status == TrackedDownloadStatus.WARNING),
			items.Count(x => x.SeriesId is null && x.MovieId is null)));
	}

	private static async Task<Ok<IReadOnlyList<QueueItemDto>>> DetailsAsync(
		int? seriesId,
		int? movieId,
		[FromQuery] int[]? episodeIds,
		SubmarineDbContext db,
		CancellationToken cancellationToken)
	{
		var query = Base(db);
		if (seriesId is not null)
		{
			query = query.Where(x => x.SeriesId == seriesId);
		}

		if (movieId is not null)
		{
			query = query.Where(x => x.MovieId == movieId);
		}

		var items = await query.ToListAsync(cancellationToken);
		if (episodeIds is { Length: > 0 })
		{
			items = [.. items.Where(x => x.EpisodeIds.Any(episodeIds.Contains))];
		}

		return TypedResults.Ok<IReadOnlyList<QueueItemDto>>([.. items.Select(ToDto)]);
	}

	private static async Task<Results<NoContent, NotFound>> DeleteAsync(
		int id,
		SubmarineDbContext db,
		IDownloadClientProvider clientProvider,
		ICommandQueue commandQueue,
		IEventBus eventBus,
		TimeProvider timeProvider,
		[AsParameters] QueueDeleteOptions options,
		CancellationToken cancellationToken)
	{
		var download = await db.TrackedDownloads.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (download is null)
		{
			return TypedResults.NotFound();
		}

		await RemoveOneAsync(download, options, db, clientProvider, commandQueue, timeProvider, cancellationToken);
		await db.SaveChangesAsync(cancellationToken);
		await eventBus.PublishAsync(new QueueUpdatedEvent(), cancellationToken);
		return TypedResults.NoContent();
	}

	private static async Task<Ok<BulkQueueDeleteResult>> BulkDeleteAsync(
		SubmarineDbContext db,
		IDownloadClientProvider clientProvider,
		ICommandQueue commandQueue,
		IEventBus eventBus,
		TimeProvider timeProvider,
		IValidator<BulkQueueDeleteRequest> validator,
		[FromBody] BulkQueueDeleteRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var downloads = await db.TrackedDownloads.Where(x => request.Ids.Contains(x.Id)).ToListAsync(cancellationToken);
		var options = new QueueDeleteOptions(request.RemoveFromClient, request.Blocklist, request.SkipRedownload);
		foreach (var download in downloads)
		{
			await RemoveOneAsync(download, options, db, clientProvider, commandQueue, timeProvider, cancellationToken);
		}

		await db.SaveChangesAsync(cancellationToken);
		await eventBus.PublishAsync(new QueueUpdatedEvent(), cancellationToken);
		return TypedResults.Ok(new BulkQueueDeleteResult(downloads.Count));
	}

	private static async Task<Results<Ok<Command>, NotFound>> RetryAsync(
		int id,
		SubmarineDbContext db,
		ICommandQueue commandQueue,
		CancellationToken cancellationToken)
	{
		var download = await db.TrackedDownloads.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (download is null)
		{
			return TypedResults.NotFound();
		}

		Command command;
		if (download.SeriesId is not null && download.EpisodeIds.Count > 0)
		{
			command = await commandQueue.EnqueueAsync(new EpisodeSearchCommand(download.EpisodeIds), CommandTrigger.MANUAL, CommandPriority.HIGH, cancellationToken);
		}
		else if (download.MovieId is { } movieId)
		{
			command = await commandQueue.EnqueueAsync(new MovieSearchCommand([movieId]), CommandTrigger.MANUAL, CommandPriority.HIGH, cancellationToken);
		}
		else
		{
			return TypedResults.NotFound();
		}

		db.TrackedDownloads.Remove(download);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(command);
	}

	private static async Task<Results<Ok<ImportRunSummaryDto>, NotFound>> ForceImportAsync(
		int id,
		SubmarineDbContext db,
		IImportService importService,
		CancellationToken cancellationToken)
	{
		var exists = await db.TrackedDownloads.AnyAsync(x => x.Id == id, cancellationToken);
		if (!exists)
		{
			return TypedResults.NotFound();
		}

		var summary = await importService.ImportTrackedDownloadAsync(id, cancellationToken);
		return TypedResults.Ok(new ImportRunSummaryDto(summary.AnyImported, summary.Files.Count, summary.Files.Count(x => x.Imported)));
	}

	private static async Task RemoveOneAsync(
		TrackedDownload download,
		QueueDeleteOptions options,
		SubmarineDbContext db,
		IDownloadClientProvider clientProvider,
		ICommandQueue commandQueue,
		TimeProvider timeProvider,
		CancellationToken cancellationToken)
	{
		if (options.RemoveFromClient ?? false)
		{
			var client = await clientProvider.GetAsync(download.DownloadClientId, cancellationToken);
			if (client is not null)
			{
				try
				{
					await client.Instance.RemoveAsync(download.DownloadId, deleteData: true, cancellationToken);
				}
				catch (DownloadClientException)
				{
					// best effort; the row is still removed below
				}
			}
		}

		if (options.Blocklist ?? false)
		{
			db.BlocklistItems.Add(new BlocklistItem
			{
				ReleaseTitle = download.ReleaseTitle ?? download.Title,
				Protocol = download.Protocol,
				IndexerId = download.IndexerId,
				SeriesId = download.SeriesId,
				MovieId = download.MovieId,
				EpisodeIds = download.EpisodeIds,
				Reason = "Removed from queue",
				Size = download.Size,
				Date = timeProvider.GetUtcNow().UtcDateTime
			});
		}

		if (!(options.SkipRedownload ?? false))
		{
			if (download.SeriesId is not null && download.EpisodeIds.Count > 0)
			{
				await commandQueue.EnqueueAsync(new EpisodeSearchCommand(download.EpisodeIds), CommandTrigger.SYSTEM, cancellationToken: cancellationToken);
			}
			else if (download.MovieId is { } movieId)
			{
				await commandQueue.EnqueueAsync(new MovieSearchCommand([movieId]), CommandTrigger.SYSTEM, cancellationToken: cancellationToken);
			}
		}

		db.TrackedDownloads.Remove(download);
	}

	private static IQueryable<TrackedDownload> Base(SubmarineDbContext db)
		=> db.TrackedDownloads
			.AsNoTracking()
			.Include(x => x.DownloadClient)
			.Include(x => x.Series)
			.Include(x => x.Movie)
			.Include(x => x.Indexer)
			.OrderByDescending(x => x.Added);

	private static QueueItemDto ToDto(TrackedDownload download)
		=> new(
			download.Id,
			download.Title,
			download.Series?.Title,
			download.Movie?.Title,
			download.SeriesId,
			download.EpisodeIds,
			download.MovieId,
			download.Protocol.ToString(),
			download.Status.ToString(),
			download.State.ToString(),
			download.Size,
			download.SizeLeft,
			download.StatusMessages,
			download.Indexer?.Name,
			download.DownloadClient.Name,
			download.Added,
			download.Quality,
			[.. download.Languages],
			download.ReleaseGroup,
			download.DownloadId,
			download.OutputPath);
}

/// <summary>One item in the download queue.</summary>
/// <param name="Id">Id.</param>
/// <param name="Title">Client side title.</param>
/// <param name="SeriesTitle">Matched series title, if any.</param>
/// <param name="MovieTitle">Matched movie title, if any.</param>
/// <param name="SeriesId">Matched series id.</param>
/// <param name="EpisodeIds">Matched episode ids.</param>
/// <param name="MovieId">Matched movie id.</param>
/// <param name="Protocol">Download protocol.</param>
/// <param name="Status">Client status.</param>
/// <param name="TrackedDownloadState">Import pipeline state.</param>
/// <param name="Size">Total size in bytes.</param>
/// <param name="SizeLeft">Remaining bytes.</param>
/// <param name="StatusMessages">Status messages from the pipeline.</param>
/// <param name="Indexer">Indexer name, if known.</param>
/// <param name="DownloadClient">Download client name.</param>
/// <param name="Added">When tracking started.</param>
/// <param name="Quality">Parsed quality, if known.</param>
/// <param name="Languages">Parsed languages.</param>
/// <param name="ReleaseGroup">Parsed release group, if known.</param>
/// <param name="DownloadId">Download id inside the client, for manual import.</param>
/// <param name="OutputPath">Output path on the client, for manual import.</param>
public sealed record QueueItemDto(
	int Id,
	string Title,
	string? SeriesTitle,
	string? MovieTitle,
	int? SeriesId,
	List<int> EpisodeIds,
	int? MovieId,
	string Protocol,
	string Status,
	string TrackedDownloadState,
	long Size,
	long SizeLeft,
	List<string> StatusMessages,
	string? Indexer,
	string DownloadClient,
	DateTime Added,
	QualityModel? Quality,
	List<Language> Languages,
	string? ReleaseGroup,
	string DownloadId,
	string? OutputPath);

/// <summary>Queue counts.</summary>
/// <param name="Total">Total tracked downloads.</param>
/// <param name="Errors">Downloads in a failed state.</param>
/// <param name="Warnings">Downloads with a warning status.</param>
/// <param name="Unknown">Downloads not matched to any library item.</param>
public sealed record QueueStatusDto(int Total, int Errors, int Warnings, int Unknown);

/// <summary>Result of a forced import.</summary>
/// <param name="AnyImported">Whether at least one file was imported.</param>
/// <param name="FilesConsidered">Number of candidate files considered.</param>
/// <param name="FilesImported">Number of files imported.</param>
public sealed record ImportRunSummaryDto(bool AnyImported, int FilesConsidered, int FilesImported);

/// <summary>Removal options shared by single and bulk queue deletes.</summary>
/// <param name="RemoveFromClient">Also remove the download from its client.</param>
/// <param name="Blocklist">Blocklist the release.</param>
/// <param name="SkipRedownload">Skip queuing a replacement search.</param>
public sealed record QueueDeleteOptions(bool? RemoveFromClient, bool? Blocklist, bool? SkipRedownload);

/// <summary>Bulk queue delete request.</summary>
/// <param name="Ids">Ids to remove.</param>
/// <param name="RemoveFromClient">Also remove the downloads from their clients.</param>
/// <param name="Blocklist">Blocklist the releases.</param>
/// <param name="SkipRedownload">Skip queuing replacement searches.</param>
public sealed record BulkQueueDeleteRequest(List<int> Ids, bool? RemoveFromClient, bool? Blocklist, bool? SkipRedownload);

/// <summary>Result of a bulk queue delete.</summary>
/// <param name="Removed">Number of rows removed.</param>
public sealed record BulkQueueDeleteResult(int Removed);

/// <summary>Validator for <see cref="BulkQueueDeleteRequest" />.</summary>
public sealed class BulkQueueDeleteRequestValidator : AbstractValidator<BulkQueueDeleteRequest>
{
	/// <inheritdoc />
	public BulkQueueDeleteRequestValidator()
	{
		RuleFor(x => x.Ids).NotNull().NotEmpty();
		RuleForEach(x => x.Ids).GreaterThan(0);
	}
}
