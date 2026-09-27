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

namespace Submarine.Api.Features.MovieFiles;

/// <summary>
///     Movie file inspection, editing and deletion.
/// </summary>
public sealed class MovieFilesModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/movie-files");
		group.MapGet("/", ListAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/bulk", BulkDeleteAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<IReadOnlyList<MovieFileDto>>> ListAsync(
		int? movieId,
		[FromQuery] int[]? movieFileIds,
		SubmarineDbContext db,
		CancellationToken cancellationToken)
	{
		var query = Base(db);
		if (movieId is not null)
		{
			query = query.Where(x => x.MovieId == movieId);
		}

		if (movieFileIds is { Length: > 0 })
		{
			query = query.Where(x => movieFileIds.Contains(x.Id));
		}

		var files = await query.ToListAsync(cancellationToken);
		return TypedResults.Ok<IReadOnlyList<MovieFileDto>>([.. files.Select(ToDto)]);
	}

	private static async Task<Results<Ok<MovieFileDto>, NotFound>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var file = await Base(db).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		return file is null ? TypedResults.NotFound() : TypedResults.Ok(ToDto(file));
	}

	private static async Task<Results<Ok<MovieFileDto>, NotFound>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		IValidator<MovieFileUpdateRequest> validator,
		[FromBody] MovieFileUpdateRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var file = await db.MovieFiles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
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
		MovieFileOperationService movieFiles,
		CancellationToken cancellationToken)
		=> await movieFiles.DeleteAsync(id, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();

	private static async Task<Ok<BulkMovieFileDeleteResult>> BulkDeleteAsync(
		MovieFileOperationService movieFiles,
		IValidator<BulkMovieFileDeleteRequest> validator,
		[FromBody] BulkMovieFileDeleteRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var count = await movieFiles.DeleteManyAsync(request.Ids, cancellationToken);
		return TypedResults.Ok(new BulkMovieFileDeleteResult(count));
	}

	private static IQueryable<MovieFile> Base(SubmarineDbContext db)
		=> db.MovieFiles.AsNoTracking().OrderByDescending(x => x.DateAdded);

	private static MovieFileDto ToDto(MovieFile file)
		=> new(
			file.Id,
			file.MovieId,
			file.MediaVersionId,
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

/// <summary>An imported movie file.</summary>
/// <param name="Id">Id.</param>
/// <param name="MovieId">Owning movie.</param>
/// <param name="MediaVersionId">Owning version.</param>
/// <param name="RelativePath">Path relative to the version folder.</param>
/// <param name="Size">File size in bytes.</param>
/// <param name="DateAdded">When the file was imported.</param>
/// <param name="Quality">Quality of the file.</param>
/// <param name="Languages">Languages of the file.</param>
/// <param name="ReleaseGroup">Release group.</param>
/// <param name="SceneName">Original scene release name, if known.</param>
/// <param name="Edition">Edition of the file.</param>
/// <param name="MediaInfo">Technical media info.</param>
public sealed record MovieFileDto(
	int Id,
	int MovieId,
	int MediaVersionId,
	string RelativePath,
	long Size,
	DateTime DateAdded,
	QualityModel Quality,
	List<Language> Languages,
	string? ReleaseGroup,
	string? SceneName,
	string? Edition,
	MediaInfoDto? MediaInfo);

/// <summary>Edit request for a movie file. Null fields keep their value.</summary>
/// <param name="Quality">New quality.</param>
/// <param name="Languages">New languages.</param>
/// <param name="ReleaseGroup">New release group.</param>
/// <param name="SceneName">New scene name.</param>
/// <param name="Edition">New edition.</param>
public sealed record MovieFileUpdateRequest(QualityModel? Quality, List<Language>? Languages, string? ReleaseGroup, string? SceneName, string? Edition);

/// <summary>Bulk delete request.</summary>
/// <param name="Ids">Movie file ids to delete.</param>
public sealed record BulkMovieFileDeleteRequest(List<int> Ids);

/// <summary>Result of a bulk delete.</summary>
/// <param name="Deleted">Number of files deleted.</param>
public sealed record BulkMovieFileDeleteResult(int Deleted);

/// <summary>Validator for <see cref="BulkMovieFileDeleteRequest" />.</summary>
public sealed class BulkMovieFileDeleteRequestValidator : AbstractValidator<BulkMovieFileDeleteRequest>
{
	/// <inheritdoc />
	public BulkMovieFileDeleteRequestValidator()
	{
		RuleFor(x => x.Ids).NotNull().NotEmpty();
		RuleForEach(x => x.Ids).GreaterThan(0);
	}
}

/// <summary>Validator for <see cref="MovieFileUpdateRequest" />.</summary>
public sealed class MovieFileUpdateRequestValidator : AbstractValidator<MovieFileUpdateRequest>
{
	/// <inheritdoc />
	public MovieFileUpdateRequestValidator()
	{
		RuleForEach(x => x.Languages).IsInEnum().When(x => x.Languages is not null);
	}
}
