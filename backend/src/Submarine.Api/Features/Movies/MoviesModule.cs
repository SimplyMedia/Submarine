using Microsoft.AspNetCore.Http.HttpResults;
using Submarine.Api.Features.MediaFiles;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Api.Common;
using FluentValidation;
using Submarine.Api.Features.Series;
using Submarine.Api.Modules;
using Submarine.Core.Commands;
using Submarine.Core.Enums;
using Submarine.Core.Library;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Library;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Movies;

/// <summary>
///     Movie library endpoints.
/// </summary>
public sealed class MoviesModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/movies");
		group.MapGet("/", ListAsync);
		group.MapGet("/lookup", LookupAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPost("/", CreateAsync);
		group.MapPut("/editor", BulkUpdateAsync);
		group.MapDelete("/editor", BulkDeleteAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
		group.MapPost("/{id:int}/refresh", RefreshAsync);
		group.MapPost("/{id:int}/rescan", RescanAsync);
		group.MapPost("/{id:int}/search", SearchAsync);
	}

	private static async Task<Ok<PagedResult<MovieListItemDto>>> ListAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		[FromQuery] bool? monitored,
		[FromQuery] string? term,
		[FromQuery] bool? isAnime,
		[FromQuery] int? tagId,
		[FromQuery] int? rootFolderId,
		[FromQuery] MovieStatus? status,
		[FromQuery] bool? hasFile,
		CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;
		var indexerConfig = await db.IndexerConfig.AsNoTracking().SingleAsync(cancellationToken);
		var movies = db.Movies.AsNoTracking().AsQueryable();
		if (monitored.HasValue)
		{
			movies = movies.Where(x => x.Monitored == monitored.Value);
		}

		if (isAnime.HasValue)
		{
			movies = movies.Where(x => x.IsAnime == isAnime.Value);
		}

		if (status.HasValue)
		{
			movies = movies.Where(x => x.Status == status.Value);
		}

		if (tagId.HasValue)
		{
			movies = movies.Where(x => x.Tags.Any(t => t.Id == tagId.Value));
		}

		if (rootFolderId.HasValue)
		{
			movies = movies.Where(x => x.Versions.Any(v => v.RootFolderId == rootFolderId.Value));
		}

		if (hasFile.HasValue)
		{
			movies = hasFile.Value
				? movies.Where(x => x.Files.Any())
				: movies.Where(x => !x.Files.Any());
		}

		if (!string.IsNullOrWhiteSpace(term))
		{
			var cleaned = TitleNormalizer.CleanTitle(term);
			movies = movies.Where(x => x.CleanTitle.Contains(cleaned) || x.TmdbId.ToString() == cleaned);
		}

		// Custom sorts that need entity-level ordering (or aggregates) run on the entity query,
		// before projection; ProjectedDTO ordering via reflection cannot translate a property
		// access on a record built through a constructor. Everything else is sorted by
		// PagedResult on the projected DTO.
		var sortKey = query.SortKey?.Trim().ToLowerInvariant();
		var descending = PagingQuery.IsDescending(query.SortDirection);
		if (sortKey is "incinemas" or "digitalrelease" or "physicalrelease" or "sizeondisk")
		{
			movies = sortKey switch
			{
				"incinemas" => movies.ApplyOrder(x => x.InCinemasDate, descending),
				"digitalrelease" => movies.ApplyOrder(x => x.DigitalReleaseDate, descending),
				"physicalrelease" => movies.ApplyOrder(x => x.PhysicalReleaseDate, descending),
				_ => movies.ApplyOrder(x => x.Files.Sum(f => (long?)f.Size) ?? 0, descending)
			};
		}
		else
		{
			movies = PagedResult<Submarine.Core.Entities.Movie>.ApplySort(movies, query.SortKey, query.SortDirection, defaultToId: true);
		}

		query = query with { SortKey = null };

		var page = await PagedResult<MovieListItemDto>.CreateAsync(
			movies.Select(Project(db, indexerConfig.AvailabilityDelayDays, now)),
			query,
			cancellationToken);
		return TypedResults.Ok(page);
	}

	private static async Task<Results<Ok<IEnumerable<MovieLookupDto>>, ProblemHttpResult>> LookupAsync(
		SubmarineDbContext db,
		IMetadataClient metadata,
		[FromQuery] string term,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(term))
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "A search term is required");
		}

		IReadOnlyList<Contracts.Metadata.SearchResultResource> hits;
		if (term.StartsWith("tmdb:", StringComparison.OrdinalIgnoreCase) && int.TryParse(term[5..], out var tmdbId))
		{
			hits = await ToHitsAsync(await metadata.GetMovieAsync(tmdbId, cancellationToken));
		}
		else if (term.StartsWith("imdb:", StringComparison.OrdinalIgnoreCase) && term.Length > 5)
		{
			hits = await ToHitsAsync(await metadata.GetMovieByImdbAsync(term[5..], cancellationToken));
		}
		else if (term.StartsWith("imdb:", StringComparison.OrdinalIgnoreCase))
		{
			hits = [];
		}
		else
		{
			int? year = null;
			var searchTerm = term;
			var separator = term.LastIndexOf(' ');
			if (separator > 0 && int.TryParse(term[(separator + 1)..], out var parsedYear))
			{
				year = parsedYear;
				searchTerm = term[..separator];
			}

			hits = await metadata.SearchMoviesAsync(searchTerm, year, cancellationToken);
		}

		var tmdbIds = hits.Select(x => x.TmdbId).OfType<int>().ToList();
		var existing = await db.Movies
			.Where(x => tmdbIds.Contains(x.TmdbId))
			.ToDictionaryAsync(x => x.TmdbId, x => x.Id, cancellationToken);
		return TypedResults.Ok(hits.Select(x => new MovieLookupDto(
			x.TvdbId,
			x.TmdbId,
			x.ImdbId,
			x.Title,
			x.Year,
			x.Overview,
			x.PosterUrl,
			x.Status,
			x.Provider,
			x.TmdbId is { } id && existing.TryGetValue(id, out var movieId) ? movieId : null)));
	}

	private static async Task<Ok<MovieDetailDto>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var movie = await db.Movies.AsNoTracking()
			.Include(x => x.Tags)
			.Include(x => x.Files)
			.Include(x => x.Versions)
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Movie {id} not found");
		var rootPaths = await db.RootFolders.ToDictionaryAsync(x => x.Id, x => x.Path, cancellationToken);
		var indexerConfig = await db.IndexerConfig.AsNoTracking().SingleAsync(cancellationToken);
		return TypedResults.Ok(new MovieDetailDto(
			MoviesMapper.ToListItem(movie, rootPaths, indexerConfig.AvailabilityDelayDays, DateTime.UtcNow),
			[.. movie.Files.Select(f => new MovieFileDto(
				f.Id,
				f.MediaVersionId,
				f.Size,
				f.Quality,
				[.. f.Languages],
				f.ReleaseGroup,
				f.SceneName,
				f.Edition,
				MediaInfoMapper.ToDto(f.MediaInfo)))]));
	}

	private static async Task<Created<MovieDetailDto>> CreateAsync(
		SubmarineDbContext db,
		LibraryAdder adder,
		IValidator<AddMovieRequest> validator,
		[FromBody] AddMovieRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var movie = await adder.AddMovieAsync(
			new AddMovieOptions(
				request.TmdbId ?? throw new FluentValidation.ValidationException("tmdbId is required"),
				request.Title,
				request.RootFolderId,
				request.IsAnime ?? false,
				request.Monitored ?? true,
				request.MinimumAvailability ?? MinimumAvailability.RELEASED,
				request.TagIds ?? [],
				[.. (request.Versions ?? []).Select(v => new VersionOptions(
					string.IsNullOrWhiteSpace(v.Name) ? "main" : v.Name,
					v.QualityProfileId,
					v.LanguageProfileId,
					v.RootFolderId))],
				request.SearchOnAdd ?? false),
			cancellationToken);
		return TypedResults.Created($"/api/v1/movies/{movie.Id}", await ReloadDetailAsync(db, movie.Id, cancellationToken));
	}

	private static async Task<Ok<MovieDetailDto>> UpdateAsync(
		SubmarineDbContext db,
		LibraryMutator mutator,
		IValidator<UpdateMovieRequest> validator,
		int id,
		[FromBody] UpdateMovieRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		await mutator.UpdateMovieAsync(
			id,
			new UpdateMovieOptions(
				request.Monitored,
				request.MinimumAvailability,
				request.IsAnime,
				request.TagIds,
				request.RootFolderId,
				request.MoveFiles ?? false,
				[.. (request.Versions ?? []).Select(v => new UpdateVersionOptions(v.Id, v.Name, v.QualityProfileId, v.LanguageProfileId, v.Path))]),
			cancellationToken);
		return TypedResults.Ok(await ReloadDetailAsync(db, id, cancellationToken));
	}

	private static async Task<NoContent> DeleteAsync(
		LibraryMutator mutator,
		int id,
		[FromQuery] bool deleteFiles = false,
		[FromQuery] bool addImportListExclusion = false,
		CancellationToken cancellationToken = default)
	{
		await mutator.DeleteMovieAsync(id, deleteFiles, addImportListExclusion, cancellationToken);
		return TypedResults.NoContent();
	}

	private static async Task<Ok<BulkUpdateResultDto>> BulkUpdateAsync(
		LibraryMutator mutator,
		IValidator<MovieEditorRequest> validator,
		[FromBody] MovieEditorRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var updated = await mutator.BulkUpdateMoviesAsync(
			new BulkUpdateMovieOptions(
				request.Ids,
				request.Monitored,
				request.MinimumAvailability,
				request.QualityProfileId,
				request.LanguageProfileId,
				request.RootFolderId,
				request.MoveFiles ?? false,
				request.Tags is null ? null : new TagOperation(
					request.Tags.Mode switch
					{
						"add" => TagOperationMode.ADD,
						"remove" => TagOperationMode.REMOVE,
						_ => TagOperationMode.REPLACE
					},
					request.Tags.TagIds)),
			cancellationToken);
		return TypedResults.Ok(new BulkUpdateResultDto(updated));
	}

	private static async Task<NoContent> BulkDeleteAsync(
		LibraryMutator mutator,
		IValidator<BulkDeleteRequest> validator,
		[FromBody] BulkDeleteRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		await mutator.BulkDeleteMoviesAsync(request.Ids, request.DeleteFiles ?? false, request.AddImportListExclusion ?? false, cancellationToken);
		return TypedResults.NoContent();
	}

	private static Task<Created<Command>> RefreshAsync(int id, ICommandQueue queue, CancellationToken cancellationToken)
		=> SeriesModule.EnqueueAsync(queue, new RefreshMovieCommand(id), cancellationToken);

	private static Task<Created<Command>> RescanAsync(int id, ICommandQueue queue, CancellationToken cancellationToken)
		=> SeriesModule.EnqueueAsync(queue, new RescanMovieCommand(id), cancellationToken);

	private static Task<Created<Command>> SearchAsync(int id, ICommandQueue queue, CancellationToken cancellationToken)
		=> SeriesModule.EnqueueAsync(queue, new MovieSearchCommand([id]), cancellationToken);

	private static Expression<Func<Movie, MovieListItemDto>> Project(SubmarineDbContext db, int availabilityDelayDays, DateTime now)
		=> x => new MovieListItemDto(
			x.Id,
			x.TmdbId,
			x.ImdbId,
			x.Title,
			x.SortTitle,
			x.OriginalTitle,
			x.Overview,
			x.Year,
			x.Runtime,
			x.Studio,
			x.PosterUrl,
			x.BackdropUrl,
			x.Status,
			x.IsAnime,
			x.Monitored,
			x.MinimumAvailability,
			x.TmdbCollectionId,
			x.CollectionTitle,
			x.Genres,
			x.Certification,
			x.YouTubeTrailerId,
			x.InCinemasDate,
			x.DigitalReleaseDate,
			x.PhysicalReleaseDate,
			x.CreatedAt,
			x.Tags.Select(t => t.Id).ToList(),
			x.Versions.Select(v => new VersionDto(
				v.Id,
				v.Name,
				v.SeriesId,
				v.MovieId,
				v.QualityProfileId,
				v.LanguageProfileId,
				v.RootFolderId,
				db.RootFolders.Where(r => r.Id == v.RootFolderId).Select(r => r.Path).FirstOrDefault(),
				v.Path,
				v.Monitored)).ToList(),
			x.Files.Any(),
			x.Files.Sum(f => (long?)f.Size) ?? 0,
			((x.MinimumAvailability == MinimumAvailability.IN_CINEMAS
					? x.InCinemasDate
					: x.PhysicalReleaseDate ?? x.DigitalReleaseDate ?? x.InCinemasDate) ?? DateTime.MaxValue)
				.AddDays(availabilityDelayDays) <= now);

	private static async Task<MovieDetailDto?> ReloadDetailAsync(SubmarineDbContext db, int id, CancellationToken cancellationToken)
	{
		var movie = await db.Movies.AsNoTracking()
			.Include(x => x.Tags)
			.Include(x => x.Files)
			.Include(x => x.Versions)
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (movie is null)
		{
			return null;
		}

		var rootPaths = await db.RootFolders.ToDictionaryAsync(x => x.Id, x => x.Path, cancellationToken);
		var indexerConfig = await db.IndexerConfig.AsNoTracking().SingleAsync(cancellationToken);
		return new MovieDetailDto(
			MoviesMapper.ToListItem(movie, rootPaths, indexerConfig.AvailabilityDelayDays, DateTime.UtcNow),
			[.. movie.Files.Select(f => new MovieFileDto(
				f.Id,
				f.MediaVersionId,
				f.Size,
				f.Quality,
				[.. f.Languages],
				f.ReleaseGroup,
				f.SceneName,
				f.Edition,
				MediaInfoMapper.ToDto(f.MediaInfo)))]);
	}

	private static async Task<IReadOnlyList<Contracts.Metadata.SearchResultResource>> ToHitsAsync(Contracts.Metadata.MovieResource? movie)
		=> movie is null
			? []
			: [new Contracts.Metadata.SearchResultResource(
				null,
				movie.TmdbId,
				movie.ImdbId,
				movie.Title,
				movie.Year,
				movie.Overview,
				movie.PosterUrl,
				movie.Status.ToString(),
				"tmdb")];
}
