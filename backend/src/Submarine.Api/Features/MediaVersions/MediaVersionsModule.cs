using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Api.Features.Compat.Shared;
using Submarine.Api.Common;
using Submarine.Api.Features.Series;
using Submarine.Core.Enums;
using Submarine.Api.Modules;
using Submarine.Core.Modules;
using System.Linq.Expressions;
using Submarine.Core.Entities;
using Submarine.Core.Common;
using Submarine.Core.MediaFiles;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.MediaVersions;

/// <summary>
///     Version endpoints: create versions for series and movies, edit and delete them.
/// </summary>
public sealed class MediaVersionsModule : IServiceModule, IEndpointModule
{
	/// <inheritdoc />
	public void Register(IServiceCollection services, IConfiguration configuration)
	{
		services.AddScoped<LibraryAdderAdapter>();
		services.AddScoped<MediaVersionMover>();
	}

	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var series = endpoints.MapGroup("/api/v1/series/{seriesId:int}/versions");
		series.MapGet("/", ListSeriesAsync);
		series.MapPost("/", CreateSeriesAsync);

		var movies = endpoints.MapGroup("/api/v1/movies/{movieId:int}/versions");
		movies.MapGet("/", ListMoviesAsync);
		movies.MapPost("/", CreateMovieAsync);

		var versions = endpoints.MapGroup("/api/v1/media-versions");
		versions.MapPut("/{id:int}", UpdateAsync);
		versions.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<List<VersionDto>>> ListSeriesAsync(int seriesId, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		if (!await db.Series.AnyAsync(x => x.Id == seriesId, cancellationToken))
		{
			throw new KeyNotFoundException($"Series {seriesId} not found");
		}

		return await ListAsync(db, x => x.SeriesId == seriesId, cancellationToken);
	}

	private static async Task<Ok<List<VersionDto>>> ListMoviesAsync(int movieId, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		if (!await db.Movies.AnyAsync(x => x.Id == movieId, cancellationToken))
		{
			throw new KeyNotFoundException($"Movie {movieId} not found");
		}

		return await ListAsync(db, x => x.MovieId == movieId, cancellationToken);
	}

	private static async Task<Ok<List<VersionDto>>> ListAsync(
		SubmarineDbContext db,
		Expression<Func<MediaVersion, bool>> filter,
		CancellationToken cancellationToken)
	{
		var versions = await db.MediaVersions.AsNoTracking()
			.Where(filter)
			.OrderBy(x => x.Id)
			.ToListAsync(cancellationToken);
		var rootPaths = await db.RootFolders.ToDictionaryAsync(x => x.Id, x => x.Path, cancellationToken);
		return TypedResults.Ok(versions.Select(x => SeriesMapper.ToDto(x, rootPaths)).ToList());
	}

	private static async Task<Created<VersionDto>> CreateSeriesAsync(
		int seriesId,
		SubmarineDbContext db,
		LibraryAdderAdapter adder,
		IValidator<AddVersionRequest> validator,
		[FromBody] AddVersionRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var series = await db.Series.Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == seriesId, cancellationToken)
			?? throw new KeyNotFoundException($"Series {seriesId} not found");
		var version = await adder.CreateVersionAsync(
			db,
			series,
			request.Name,
			request.QualityProfileId,
			request.LanguageProfileId,
			request.RootFolderId ?? throw new FluentValidation.ValidationException("rootFolderId is required"),
			cancellationToken);
		return TypedResults.Created($"/api/v1/media-versions/{version.Id}", SeriesMapper.ToDto(version, await RootPathsAsync(db, cancellationToken)));
	}

	private static async Task<Created<VersionDto>> CreateMovieAsync(
		int movieId,
		SubmarineDbContext db,
		LibraryAdderAdapter adder,
		IValidator<AddVersionRequest> validator,
		[FromBody] AddVersionRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var movie = await db.Movies.Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken)
			?? throw new KeyNotFoundException($"Movie {movieId} not found");
		var version = await adder.CreateVersionAsync(
			db,
			movie,
			request.Name,
			request.QualityProfileId,
			request.LanguageProfileId,
			request.RootFolderId ?? throw new FluentValidation.ValidationException("rootFolderId is required"),
			cancellationToken);
		return TypedResults.Created($"/api/v1/media-versions/{version.Id}", SeriesMapper.ToDto(version, await RootPathsAsync(db, cancellationToken)));
	}

	private static async Task<Ok<VersionDto>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		MediaVersionMover mover,
		IValidator<UpdateMediaVersionRequest> validator,
		[FromBody] UpdateMediaVersionRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		if (request.Path is not null && request.RootFolderId is not null && request.MoveFiles == true)
		{
			throw new ValidationException("Path cannot be changed together with a root-folder move");
		}
		var version = await db.MediaVersions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Version {id} not found");

		if (request.Name is { } name)
		{
			version.Name = name;
		}

		if (request.QualityProfileId is { } qualityProfileId)
		{
			_ = await db.QualityProfiles.FirstOrDefaultAsync(x => x.Id == qualityProfileId, cancellationToken)
				?? throw new KeyNotFoundException($"Quality profile {qualityProfileId} not found");
			version.QualityProfileId = qualityProfileId;
		}

		if (request.LanguageProfileId is { } languageProfileId)
		{
			_ = await db.LanguageProfiles.FirstOrDefaultAsync(x => x.Id == languageProfileId, cancellationToken)
				?? throw new KeyNotFoundException($"Language profile {languageProfileId} not found");
			version.LanguageProfileId = languageProfileId;
		}

		if (request.Path is { } path)
		{
			version.Path = path;
		}

		if (request.Monitored is { } monitored)
		{
			version.Monitored = monitored;
		}

		if (request.RootFolderId is { } rootFolderId)
		{
			await mover.ChangeRootFolderAsync(id, rootFolderId, request.MoveFiles ?? false, cancellationToken);
		}

		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(SeriesMapper.ToDto(version, await RootPathsAsync(db, cancellationToken)));
	}

	private static async Task<NoContent> DeleteAsync(
		int id,
		SubmarineDbContext db,
		CompatVersionSelection selection,
		[FromQuery] bool deleteFiles = false,
		CancellationToken cancellationToken = default)
	{
		var version = await db.MediaVersions
			.Include(x => x.EpisodeFiles)
			.Include(x => x.MovieFiles)
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Version {id} not found");
		var siblingCount = version.SeriesId is { } ownerSeriesId
			? await db.MediaVersions.CountAsync(x => x.Id != id && x.SeriesId == ownerSeriesId, cancellationToken)
			: await db.MediaVersions.CountAsync(x => x.Id != id && x.MovieId == version.MovieId, cancellationToken);
		if (siblingCount == 0)
		{
			throw new ConflictException("The last version of a series or movie cannot be deleted");
		}
		await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
		try
		{
			if (version.SeriesId is { } seriesId)
				await selection.RebindSeriesBeforeVersionRemovalAsync(seriesId, id, cancellationToken);
			else if (version.MovieId is { } movieId)
				await selection.RebindMovieBeforeVersionRemovalAsync(movieId, id, cancellationToken);
		}
		catch (InvalidOperationException exception)
		{
			throw new ConflictException(exception.Message);
		}
		if (deleteFiles)
		{
			var rootPath = await db.RootFolders
				.Where(x => x.Id == version.RootFolderId)
				.Select(x => x.Path)
				.FirstOrDefaultAsync(cancellationToken);
			if (rootPath is not null)
			{
				try
				{
					var fullPath = MediaVersionPathGuard.ResolveUnderRoot(rootPath, version.Path);
					if (Directory.Exists(fullPath))
					{
						Directory.Delete(fullPath, true);
					}
				}
				catch (IOException)
				{
					// A locked folder must not block removing the version row.
				}
				catch (UnauthorizedAccessException)
				{
					// A locked folder must not block removing the version row.
				}
				catch (InvalidOperationException)
				{
					// An invalid version path must never touch anything outside its root folder.
				}
			}
		}

		db.MediaVersions.Remove(version);
		await db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return TypedResults.NoContent();
	}

	private static async Task<IReadOnlyDictionary<int, string>> RootPathsAsync(SubmarineDbContext db, CancellationToken cancellationToken)
		=> await db.RootFolders.ToDictionaryAsync(x => x.Id, x => x.Path, cancellationToken);
}

/// <summary>Update version request, null fields keep their value.</summary>
/// <param name="Name">New display name.</param>
/// <param name="QualityProfileId">New quality profile.</param>
/// <param name="LanguageProfileId">New language profile.</param>
/// <param name="RootFolderId">New root folder.</param>
/// <param name="Path">New folder name inside the root folder.</param>
/// <param name="Monitored">New monitored flag.</param>
/// <param name="MoveFiles">Whether files move on disk when the root folder changes.</param>
public sealed record UpdateMediaVersionRequest(
	string? Name,
	int? QualityProfileId,
	int? LanguageProfileId,
	int? RootFolderId,
	string? Path,
	bool? Monitored,
	bool? MoveFiles = null);

/// <summary>Validator for <see cref="AddVersionRequest" /> (shared with the series add request).</summary>
public sealed class AddVersionRequestValidator : AbstractValidator<AddVersionRequest>
{
	/// <inheritdoc />
	public AddVersionRequestValidator()
	{
		RuleFor(x => x.QualityProfileId).GreaterThan(0);
		RuleFor(x => x.LanguageProfileId).GreaterThan(0);
		RuleFor(x => x.RootFolderId).GreaterThan(0).When(x => x.RootFolderId.HasValue);
	}
}

/// <summary>Validator for <see cref="UpdateMediaVersionRequest" />.</summary>
public sealed class UpdateMediaVersionRequestValidator : AbstractValidator<UpdateMediaVersionRequest>
{
	/// <inheritdoc />
	public UpdateMediaVersionRequestValidator()
	{
		RuleFor(x => x.Path)
			.Must(MediaVersionPathGuard.IsSingleRelativeSegment)
			.WithMessage("Path must be a single relative folder segment (no separators, not '.' or '..')")
			.When(x => x.Path is not null);
		RuleFor(x => x)
			.Must(x => x.Path is null || x.RootFolderId is null || x.MoveFiles != true)
			.WithMessage("Path cannot be changed in the same request as moving files to another root folder");
	}
}

/// <summary>
///     Creates versions for existing series and movies. Separate from LibraryAdder
///     because that owns the add-from-metadata flow.
/// </summary>
public sealed class LibraryAdderAdapter(Submarine.Core.Naming.IFolderNameRenderer folderNameRenderer)
{
	/// <summary>Create a version for a series, rendering the folder name when the path is unset.</summary>
	public async Task<MediaVersion> CreateVersionAsync(
		SubmarineDbContext db,
		Submarine.Core.Entities.Series series,
		string? name,
		int qualityProfileId,
		int languageProfileId,
		int rootFolderId,
		CancellationToken cancellationToken)
	{
		var version = await BuildVersionAsync(
			db,
			qualityProfileId,
			languageProfileId,
			rootFolderId,
			Submarine.Core.Enums.MediaKind.SERIES,
			folderNameRenderer.RenderSeriesFolder(await NamingAsync(db, cancellationToken), series),
			cancellationToken);
		version.SeriesId = series.Id;
		series.Versions.Add(version);
		await db.SaveChangesAsync(cancellationToken);
		version.Name = string.IsNullOrWhiteSpace(name) ? DefaultName(series.Versions) : name;
		await db.SaveChangesAsync(cancellationToken);
		return version;
	}

	/// <summary>Create a version for a movie, rendering the folder name when the path is unset.</summary>
	public async Task<MediaVersion> CreateVersionAsync(
		SubmarineDbContext db,
		Movie movie,
		string? name,
		int qualityProfileId,
		int languageProfileId,
		int rootFolderId,
		CancellationToken cancellationToken)
	{
		var version = await BuildVersionAsync(
			db,
			qualityProfileId,
			languageProfileId,
			rootFolderId,
			Submarine.Core.Enums.MediaKind.MOVIES,
			folderNameRenderer.RenderMovieFolder(await NamingAsync(db, cancellationToken), movie),
			cancellationToken);
		version.MovieId = movie.Id;
		movie.Versions.Add(version);
		await db.SaveChangesAsync(cancellationToken);
		version.Name = string.IsNullOrWhiteSpace(name) ? DefaultName(movie.Versions) : name;
		await db.SaveChangesAsync(cancellationToken);
		return version;
	}

	private static async Task<NamingConfig> NamingAsync(SubmarineDbContext db, CancellationToken cancellationToken)
		=> await db.NamingConfig.SingleAsync(cancellationToken);

	private async Task<MediaVersion> BuildVersionAsync(
		SubmarineDbContext db,
		int qualityProfileId,
		int languageProfileId,
		int rootFolderId,
		Submarine.Core.Enums.MediaKind kind,
		string folderName,
		CancellationToken cancellationToken)
	{
		_ = await db.QualityProfiles.FirstOrDefaultAsync(x => x.Id == qualityProfileId, cancellationToken)
			?? throw new KeyNotFoundException($"Quality profile {qualityProfileId} not found");
		_ = await db.LanguageProfiles.FirstOrDefaultAsync(x => x.Id == languageProfileId, cancellationToken)
			?? throw new KeyNotFoundException($"Language profile {languageProfileId} not found");
		var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == rootFolderId, cancellationToken)
			?? throw new KeyNotFoundException($"Root folder {rootFolderId} not found");
		if (root.MediaKind != kind)
		{
			throw new FluentValidation.ValidationException($"Root folder '{root.Path}' does not match the version");
		}

		return new MediaVersion
		{
			Name = "main",
			QualityProfileId = qualityProfileId,
			LanguageProfileId = languageProfileId,
			RootFolderId = rootFolderId,
			Path = folderName
		};
	}

	private static string DefaultName(IEnumerable<MediaVersion> versions)
	{
		var count = versions.Count();
		return count > 1 ? $"main {count}" : "main";
	}
}
