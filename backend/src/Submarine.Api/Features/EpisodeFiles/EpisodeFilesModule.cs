using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Features.MediaFiles;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Languages;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Import;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.EpisodeFiles;

/// <summary>
///     Episode file inspection, editing and deletion.
/// </summary>
public sealed class EpisodeFilesModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/episode-files");
		group.MapGet("/", ListAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/bulk", BulkDeleteAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<IReadOnlyList<EpisodeFileDto>>> ListAsync(
		int? seriesId,
		[FromQuery] int[]? episodeFileIds,
		SubmarineDbContext db,
		CancellationToken cancellationToken)
	{
		var query = Base(db);
		if (seriesId is not null)
		{
			query = query.Where(x => x.SeriesId == seriesId);
		}

		if (episodeFileIds is { Length: > 0 })
		{
			query = query.Where(x => episodeFileIds.Contains(x.Id));
		}

		var files = await query.ToListAsync(cancellationToken);
		return TypedResults.Ok<IReadOnlyList<EpisodeFileDto>>([.. files.Select(ToDto)]);
	}

	private static async Task<Results<Ok<EpisodeFileDto>, NotFound>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var file = await Base(db).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		return file is null ? TypedResults.NotFound() : TypedResults.Ok(ToDto(file));
	}

	private static async Task<Results<Ok<EpisodeFileDto>, NotFound>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		IValidator<EpisodeFileUpdateRequest> validator,
		[FromBody] EpisodeFileUpdateRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var file = await db.EpisodeFiles.Include(x => x.Episodes).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (file is null)
		{
			return TypedResults.NotFound();
		}

		if (request.Quality is not null)
		{
			file.Quality = request.Quality;
		}

		if (request.Languages is not null)
		{
			file.Languages = request.Languages;
		}

		if (request.ReleaseGroup is not null)
		{
			file.ReleaseGroup = request.ReleaseGroup;
		}

		if (request.SceneName is not null)
		{
			file.SceneName = request.SceneName;
		}

		if (request.Edition is not null)
		{
			file.Edition = request.Edition;
		}

		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(ToDto(file));
	}

	private static async Task<Results<NoContent, NotFound>> DeleteAsync(
		int id,
		SubmarineDbContext db,
		IRecycleBinService recycleBinService,
		IEventBus eventBus,
		TimeProvider timeProvider,
		CancellationToken cancellationToken)
	{
		var file = await db.EpisodeFiles.Include(x => x.Episodes).Include(x => x.MediaVersion).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (file is null)
		{
			return TypedResults.NotFound();
		}

		await DeleteOneAsync(file, db, recycleBinService, eventBus, timeProvider, cancellationToken);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.NoContent();
	}

	private static async Task<Ok<BulkEpisodeFileDeleteResult>> BulkDeleteAsync(
		SubmarineDbContext db,
		IRecycleBinService recycleBinService,
		IEventBus eventBus,
		TimeProvider timeProvider,
		IValidator<BulkEpisodeFileDeleteRequest> validator,
		[FromBody] BulkEpisodeFileDeleteRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var files = await db.EpisodeFiles.Include(x => x.Episodes).Include(x => x.MediaVersion)
			.Where(x => request.Ids.Contains(x.Id))
			.ToListAsync(cancellationToken);

		foreach (var file in files)
		{
			await DeleteOneAsync(file, db, recycleBinService, eventBus, timeProvider, cancellationToken);
		}

		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(new BulkEpisodeFileDeleteResult(files.Count));
	}

	private static async Task DeleteOneAsync(
		EpisodeFile file,
		SubmarineDbContext db,
		IRecycleBinService recycleBinService,
		IEventBus eventBus,
		TimeProvider timeProvider,
		CancellationToken cancellationToken)
	{
		var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == file.MediaVersion.RootFolderId, cancellationToken);
		if (root is not null)
		{
			var mediaManagement = await db.MediaManagementConfig.AsNoTracking().SingleAsync(cancellationToken);
			var fullPath = Path.GetFullPath(Path.Combine(root.Path, file.MediaVersion.Path, file.RelativePath));
			recycleBinService.Recycle(fullPath, root.Path, mediaManagement.RecycleBinPath, timeProvider);
			await eventBus.PublishAsync(
				new EpisodeFileDeletedEvent(file.SeriesId, file.MediaVersionId, [.. file.Episodes.Select(x => x.Id)], fullPath, FileDeleteReason.MANUAL),
				cancellationToken);
		}

		db.HistoryEvents.Add(new HistoryEvent
		{
			Type = HistoryEventType.DELETED,
			SeriesId = file.SeriesId,
			EpisodeId = file.Episodes.FirstOrDefault()?.Id,
			MediaVersionId = file.MediaVersionId,
			SourceTitle = file.SceneName ?? file.RelativePath,
			Quality = file.Quality,
			Languages = file.Languages,
			Date = timeProvider.GetUtcNow().UtcDateTime
		});

		db.EpisodeFiles.Remove(file);
	}

	private static IQueryable<EpisodeFile> Base(SubmarineDbContext db)
		=> db.EpisodeFiles.AsNoTracking().Include(x => x.Episodes).OrderByDescending(x => x.DateAdded);

	private static EpisodeFileDto ToDto(EpisodeFile file)
		=> new(
			file.Id,
			file.SeriesId,
			file.MediaVersionId,
			[.. file.Episodes.Select(x => x.Id)],
			file.RelativePath,
			file.Size,
			file.DateAdded,
			file.Quality,
			file.Languages,
			file.ReleaseGroup,
			file.SceneName,
			file.Edition,
			MediaInfoMapper.ToDto(file.MediaInfo));
}

/// <summary>An imported episode file.</summary>
/// <param name="Id">Id.</param>
/// <param name="SeriesId">Owning series.</param>
/// <param name="MediaVersionId">Owning version.</param>
/// <param name="EpisodeIds">Episodes covered by this file.</param>
/// <param name="RelativePath">Path relative to the version folder.</param>
/// <param name="Size">File size in bytes.</param>
/// <param name="DateAdded">When the file was imported.</param>
/// <param name="Quality">Quality of the file.</param>
/// <param name="Languages">Languages of the file.</param>
/// <param name="ReleaseGroup">Release group.</param>
/// <param name="SceneName">Original scene release name, if known.</param>
/// <param name="Edition">Edition of the file.</param>
/// <param name="MediaInfo">Technical media info.</param>
public sealed record EpisodeFileDto(
	int Id,
	int SeriesId,
	int MediaVersionId,
	List<int> EpisodeIds,
	string RelativePath,
	long Size,
	DateTime DateAdded,
	QualityModel Quality,
	List<Language> Languages,
	string? ReleaseGroup,
	string? SceneName,
	string? Edition,
	MediaInfoDto? MediaInfo);

/// <summary>Edit request for an episode file. Null fields keep their value.</summary>
/// <param name="Quality">New quality.</param>
/// <param name="Languages">New languages.</param>
/// <param name="ReleaseGroup">New release group.</param>
/// <param name="SceneName">New scene name.</param>
/// <param name="Edition">New edition.</param>
public sealed record EpisodeFileUpdateRequest(QualityModel? Quality, List<Language>? Languages, string? ReleaseGroup, string? SceneName, string? Edition);

/// <summary>Bulk delete request.</summary>
/// <param name="Ids">Episode file ids to delete.</param>
public sealed record BulkEpisodeFileDeleteRequest(List<int> Ids);

/// <summary>Result of a bulk delete.</summary>
/// <param name="Deleted">Number of files deleted.</param>
public sealed record BulkEpisodeFileDeleteResult(int Deleted);

/// <summary>Validator for <see cref="BulkEpisodeFileDeleteRequest" />.</summary>
public sealed class BulkEpisodeFileDeleteRequestValidator : AbstractValidator<BulkEpisodeFileDeleteRequest>
{
	/// <inheritdoc />
	public BulkEpisodeFileDeleteRequestValidator()
	{
		RuleFor(x => x.Ids).NotNull().NotEmpty();
		RuleForEach(x => x.Ids).GreaterThan(0);
	}
}

/// <summary>Validator for <see cref="EpisodeFileUpdateRequest" />.</summary>
public sealed class EpisodeFileUpdateRequestValidator : AbstractValidator<EpisodeFileUpdateRequest>
{
	/// <inheritdoc />
	public EpisodeFileUpdateRequestValidator()
	{
		RuleForEach(x => x.Languages).IsInEnum().When(x => x.Languages is not null);
	}
}
