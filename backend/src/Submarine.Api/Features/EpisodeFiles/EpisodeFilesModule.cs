using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Features.MediaFiles;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Languages;
using Submarine.Core.Modules;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.EpisodeFiles;

/// <summary>
///     Episode file inspection, editing and deletion.
/// </summary>
public sealed class EpisodeFilesModule : IEndpointModule, IServiceModule
{
	/// <inheritdoc />
	public void Register(IServiceCollection services, IConfiguration configuration)
		=> services.AddScoped<EpisodeFileDeletionService>();

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
		EpisodeFileDeletionService deletionService,
		CancellationToken cancellationToken)
	{
		var deleted = await deletionService.DeleteAsync(id, cancellationToken);
		return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
	}

	private static async Task<Ok<BulkEpisodeFileDeleteResult>> BulkDeleteAsync(
		EpisodeFileDeletionService deletionService,
		IValidator<BulkEpisodeFileDeleteRequest> validator,
		[FromBody] BulkEpisodeFileDeleteRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var deleted = await deletionService.DeleteManyAsync(request.Ids, cancellationToken);
		return TypedResults.Ok(new BulkEpisodeFileDeleteResult(deleted));
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
