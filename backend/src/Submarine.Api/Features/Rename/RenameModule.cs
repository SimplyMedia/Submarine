using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Naming;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Rename;

/// <summary>
///     Previews and queues renaming of episode and movie files to match the naming templates.
/// </summary>
public sealed class RenameModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/rename");
		group.MapGet("/", PreviewAsync);
		group.MapPost("/", EnqueueAsync);
	}

	private static async Task<Results<Ok<IReadOnlyList<RenamePreviewDto>>, NotFound>> PreviewAsync(
		int? seriesId,
		int? movieId,
		int? mediaVersionId,
		SubmarineDbContext db,
		NamingService namingService,
		CancellationToken cancellationToken)
	{
		var naming = await db.NamingConfig.AsNoTracking().SingleAsync(cancellationToken);
		var result = new List<RenamePreviewDto>();

		if (seriesId is { } sid)
		{
			var series = await db.Series.FirstOrDefaultAsync(x => x.Id == sid, cancellationToken);
			if (series is null)
			{
				return TypedResults.NotFound();
			}

			var query = db.EpisodeFiles.Include(x => x.Episodes).Include(x => x.MediaVersion).Where(x => x.SeriesId == sid);
			if (mediaVersionId is not null)
			{
				query = query.Where(x => x.MediaVersionId == mediaVersionId);
			}

			foreach (var file in await query.ToListAsync(cancellationToken))
			{
				var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == file.MediaVersion.RootFolderId, cancellationToken);
				var episodes = file.Episodes.OrderBy(x => x.EpisodeNumber).ToList();
				if (root is null || episodes.Count == 0)
				{
					continue;
				}

				var versionFolder = Path.Combine(root.Path, file.MediaVersion.Path);
				var targetFolder = series.SeasonFolder
					? Path.Combine(versionFolder, namingService.RenderSeasonFolder(naming, series, episodes[0].SeasonNumber))
					: versionFolder;
				var extension = Path.GetExtension(file.RelativePath);
				var newRelative = Path.GetRelativePath(versionFolder, Path.Combine(targetFolder, namingService.RenderEpisodeFileName(series, episodes, file, naming) + extension));

				if (!string.Equals(newRelative, file.RelativePath, StringComparison.Ordinal))
				{
					result.Add(new RenamePreviewDto(file.Id, file.RelativePath, newRelative));
				}
			}
		}
		else if (movieId is { } mid)
		{
			var movie = await db.Movies.FirstOrDefaultAsync(x => x.Id == mid, cancellationToken);
			if (movie is null)
			{
				return TypedResults.NotFound();
			}

			var query = db.MovieFiles.Include(x => x.MediaVersion).Where(x => x.MovieId == mid);
			if (mediaVersionId is not null)
			{
				query = query.Where(x => x.MediaVersionId == mediaVersionId);
			}

			foreach (var file in await query.ToListAsync(cancellationToken))
			{
				var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == file.MediaVersion.RootFolderId, cancellationToken);
				if (root is null)
				{
					continue;
				}

				var extension = Path.GetExtension(file.RelativePath);
				var newRelative = namingService.RenderMovieFileName(movie, file, naming) + extension;

				if (!string.Equals(newRelative, file.RelativePath, StringComparison.Ordinal))
				{
					result.Add(new RenamePreviewDto(file.Id, file.RelativePath, newRelative));
				}
			}
		}

		return TypedResults.Ok<IReadOnlyList<RenamePreviewDto>>(result);
	}

	private static async Task<Results<Ok<Command>, BadRequest<string>>> EnqueueAsync(
		ICommandQueue commandQueue,
		IValidator<RenameRequest> validator,
		[FromBody] RenameRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);

		Command command;
		if (request.SeriesId is { } seriesId)
		{
			command = await commandQueue.EnqueueAsync(new RenameSeriesCommand(seriesId, request.FileIds), CommandTrigger.MANUAL, CommandPriority.HIGH, cancellationToken);
		}
		else if (request.MovieId is { } movieId)
		{
			command = await commandQueue.EnqueueAsync(new RenameMovieCommand(movieId, request.FileIds), CommandTrigger.MANUAL, CommandPriority.HIGH, cancellationToken);
		}
		else
		{
			return TypedResults.BadRequest("Either seriesId or movieId is required");
		}

		return TypedResults.Ok(command);
	}
}

/// <summary>One file whose name would change.</summary>
/// <param name="FileId">Episode or movie file id.</param>
/// <param name="ExistingPath">Current path, relative to the version folder.</param>
/// <param name="NewPath">Path the file would be renamed to, relative to the version folder.</param>
public sealed record RenamePreviewDto(int FileId, string ExistingPath, string NewPath);

/// <summary>Rename request.</summary>
/// <param name="SeriesId">Series to rename.</param>
/// <param name="MovieId">Movie to rename.</param>
/// <param name="FileIds">Specific file ids, null renames every file.</param>
public sealed record RenameRequest(int? SeriesId, int? MovieId, List<int>? FileIds);

/// <summary>Validator for <see cref="RenameRequest" />.</summary>
public sealed class RenameRequestValidator : AbstractValidator<RenameRequest>
{
	/// <inheritdoc />
	public RenameRequestValidator()
		=> RuleFor(x => x).Must(x => x.SeriesId is not null || x.MovieId is not null).WithMessage("Either seriesId or movieId is required");
}
