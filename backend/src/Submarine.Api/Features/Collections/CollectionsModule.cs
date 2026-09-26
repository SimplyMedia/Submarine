using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Submarine.Api.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Library;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Collections;

/// <summary>
///     Collection endpoints: monitored TMDB collections and collections referenced by library movies.
/// </summary>
public sealed class CollectionsModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/collections");
		group.MapGet("/", ListAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapPost("/{id:int}/add-missing", AddMissingAsync);
	}

	private static async Task<Ok<List<CollectionDto>>> ListAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var movieCounts = await db.Movies.AsNoTracking()
			.Where(x => x.TmdbCollectionId != null)
			.GroupBy(x => x.TmdbCollectionId!.Value)
			.Select(x => new { CollectionId = x.Key, MovieCount = x.Count() })
			.ToDictionaryAsync(x => x.CollectionId, x => x.MovieCount, cancellationToken);
		var rows = await db.Collections.AsNoTracking().ToListAsync(cancellationToken);

		var knownIds = rows.Select(x => x.TmdbCollectionId).ToHashSet();
		var result = rows.Select(x => ToDto(x.Id, x, movieCounts.GetValueOrDefault(x.TmdbCollectionId), 0)).ToList();
		foreach (var (collectionId, movieCount) in movieCounts.Where(x => !knownIds.Contains(x.Key)))
		{
			var title = await db.Movies
				.Where(x => x.TmdbCollectionId == collectionId && x.CollectionTitle != null)
				.Select(x => x.CollectionTitle!)
				.FirstOrDefaultAsync(cancellationToken);
			result.Add(new CollectionDto(
				null,
				collectionId,
				title ?? $"Collection {collectionId}",
				null,
				null,
				false,
				null,
				null,
				null,
				MinimumAvailability.RELEASED,
				false,
				movieCount,
				0));
		}

		return TypedResults.Ok(result.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ToList());
	}

	private static async Task<Ok<CollectionDto>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var collection = await db.Collections.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Collection {id} not found");
		var movieCount = await db.Movies.CountAsync(x => x.TmdbCollectionId == collection.TmdbCollectionId, cancellationToken);
		return TypedResults.Ok(ToDto(collection.Id, collection, movieCount, 0));
	}

	private static async Task<Ok<CollectionDto>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		IValidator<UpdateCollectionRequest> validator,
		[FromBody] UpdateCollectionRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var collection = await db.Collections.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Collection {id} not found");

		if (request.Monitored is { } monitored)
		{
			collection.Monitored = monitored;
		}

		if (request.QualityProfileId is { } qualityProfileId)
		{
			_ = await db.QualityProfiles.FirstOrDefaultAsync(x => x.Id == qualityProfileId, cancellationToken)
				?? throw new KeyNotFoundException($"Quality profile {qualityProfileId} not found");
			collection.QualityProfileId = qualityProfileId;
		}

		if (request.LanguageProfileId is { } languageProfileId)
		{
			_ = await db.LanguageProfiles.FirstOrDefaultAsync(x => x.Id == languageProfileId, cancellationToken)
				?? throw new KeyNotFoundException($"Language profile {languageProfileId} not found");
			collection.LanguageProfileId = languageProfileId;
		}

		if (request.RootFolderId is { } rootFolderId)
		{
			var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == rootFolderId, cancellationToken)
				?? throw new KeyNotFoundException($"Root folder {rootFolderId} not found");
			if (root.MediaKind != MediaKind.MOVIES)
			{
				throw new FluentValidation.ValidationException($"Root folder '{root.Path}' does not hold movies");
			}

			collection.RootFolderId = rootFolderId;
		}

		if (request.MinimumAvailability is { } minimumAvailability)
		{
			collection.MinimumAvailability = minimumAvailability;
		}

		if (request.SearchOnAdd is { } searchOnAdd)
		{
			collection.SearchOnAdd = searchOnAdd;
		}

		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(ToDto(collection.Id, collection, 0, 0));
	}

	private static async Task<Ok<CollectionAddMissingDto>> AddMissingAsync(
		int id,
		SubmarineDbContext db,
		CollectionAddMissingService addMissingService,
		CancellationToken cancellationToken)
	{
		var collection = await db.Collections.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Collection {id} not found");
		if (collection.RootFolderId is null || collection.QualityProfileId is null || collection.LanguageProfileId is null)
		{
			throw new InvalidOperationException("The collection needs a root folder, quality profile and language profile before adding movies");
		}

		var added = await addMissingService.AddMissingAsync(collection, cancellationToken);
		return TypedResults.Ok(new CollectionAddMissingDto(added));
	}

	private static CollectionDto ToDto(int id, Collection collection, int movieCount, int missingCount)
		=> new(
			id,
			collection.TmdbCollectionId,
			collection.Title,
			collection.Overview,
			collection.PosterUrl,
			collection.Monitored,
			collection.RootFolderId,
			collection.QualityProfileId,
			collection.LanguageProfileId,
			collection.MinimumAvailability,
			collection.SearchOnAdd,
			movieCount,
			missingCount);
}

/// <summary>A collection row or a collection referenced by library movies.</summary>
/// <param name="Id">Collection row id, null when the collection is only referenced by movies.</param>
/// <param name="TmdbCollectionId">TMDB collection id.</param>
/// <param name="Title">Title.</param>
/// <param name="Overview">Overview.</param>
/// <param name="PosterUrl">Poster url.</param>
/// <param name="Monitored">Whether the collection is monitored for new movies.</param>
/// <param name="RootFolderId">Root folder for added movies.</param>
/// <param name="QualityProfileId">Quality profile for added movies.</param>
/// <param name="LanguageProfileId">Language profile for added movies.</param>
/// <param name="MinimumAvailability">Earliest availability before grabbing.</param>
/// <param name="SearchOnAdd">Search for movies on add.</param>
/// <param name="MovieCount">Movies of the collection in the library.</param>
/// <param name="MissingCount">Movies of the collection missing from the library.</param>
public sealed record CollectionDto(
	int? Id,
	int TmdbCollectionId,
	string Title,
	string? Overview,
	string? PosterUrl,
	bool Monitored,
	int? RootFolderId,
	int? QualityProfileId,
	int? LanguageProfileId,
	MinimumAvailability MinimumAvailability,
	bool SearchOnAdd,
	int MovieCount,
	int MissingCount);

/// <summary>Update collection request, null fields keep their value.</summary>
/// <param name="Monitored">New monitored flag.</param>
/// <param name="RootFolderId">New root folder.</param>
/// <param name="QualityProfileId">New quality profile.</param>
/// <param name="LanguageProfileId">New language profile.</param>
/// <param name="MinimumAvailability">New minimum availability.</param>
/// <param name="SearchOnAdd">New search on add flag.</param>
public sealed record UpdateCollectionRequest(
	bool? Monitored,
	int? RootFolderId,
	int? QualityProfileId,
	int? LanguageProfileId,
	MinimumAvailability? MinimumAvailability,
	bool? SearchOnAdd);

/// <summary>Validator for <see cref="UpdateCollectionRequest" />.</summary>
public sealed class UpdateCollectionRequestValidator : AbstractValidator<UpdateCollectionRequest>
{
	/// <inheritdoc />
	public UpdateCollectionRequestValidator()
	{
		RuleFor(x => x.MinimumAvailability).IsInEnum().When(x => x.MinimumAvailability.HasValue);
		RuleFor(x => x.RootFolderId).GreaterThan(0).When(x => x.RootFolderId.HasValue);
		RuleFor(x => x.QualityProfileId).GreaterThan(0).When(x => x.QualityProfileId.HasValue);
		RuleFor(x => x.LanguageProfileId).GreaterThan(0).When(x => x.LanguageProfileId.HasValue);
	}
}

/// <summary>Result of adding the missing movies of a collection.</summary>
/// <param name="Added">Number of movies added.</param>
public sealed record CollectionAddMissingDto(int Added);
