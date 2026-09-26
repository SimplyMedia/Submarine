using Microsoft.AspNetCore.Http.HttpResults;
using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.ReleaseProfiles;

/// <summary>
///     Release profile CRUD.
/// </summary>
public sealed class ReleaseProfilesModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/release-profiles");
		group.MapGet("/", ListAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPost("/", CreateAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<PagedResult<ReleaseProfileResource>>> ListAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		CancellationToken cancellationToken)
		=> TypedResults.Ok(await Api.Common.PagedResult<ReleaseProfileResource>.CreateAsync(
			db.ReleaseProfiles.OrderBy(profile => profile.Id).Select(ReleaseProfileResource.From),
			query,
			cancellationToken));

	private static async Task<Results<Ok<ReleaseProfileResource>, NotFound>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var profile = await db.ReleaseProfiles.AsNoTracking().FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

		return profile is null ? TypedResults.NotFound() : TypedResults.Ok(ReleaseProfileResource.FromEntity(profile));
	}

	private static async Task<Created<ReleaseProfileResource>> CreateAsync(
		SubmarineDbContext db,
		IValidator<ReleaseProfileRequest> validator,
		ReleaseProfileRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		await ValidateIndexerAsync(db, request, cancellationToken);
		await ValidateTermsAsync(request);

		var profile = new Core.Entities.ReleaseProfile();
		await ApplyRequestAsync(db, profile, request, cancellationToken);

		db.ReleaseProfiles.Add(profile);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Created($"/api/v1/release-profiles/{profile.Id}", ReleaseProfileResource.FromEntity(profile));
	}

	private static async Task<Results<Ok<ReleaseProfileResource>, NotFound>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		IValidator<ReleaseProfileRequest> validator,
		ReleaseProfileRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		await ValidateIndexerAsync(db, request, cancellationToken);
		await ValidateTermsAsync(request);

		var profile = await db.ReleaseProfiles
			.Include(entity => entity.Tags)
			.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
		if (profile is null)
		{
			return TypedResults.NotFound();
		}

		await ApplyRequestAsync(db, profile, request, cancellationToken);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Ok(ReleaseProfileResource.FromEntity(profile));
	}

	private static async Task<Results<NoContent, NotFound>> DeleteAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var profile = await db.ReleaseProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
		if (profile is null)
		{
			return TypedResults.NotFound();
		}

		db.ReleaseProfiles.Remove(profile);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.NoContent();
	}

	private static async Task ValidateIndexerAsync(
		SubmarineDbContext db,
		ReleaseProfileRequest request,
		CancellationToken cancellationToken)
	{
		if (request.IndexerId is { } indexerId
		    && !await db.Indexers.AnyAsync(indexer => indexer.Id == indexerId, cancellationToken))
		{
			throw new FluentValidation.ValidationException($"Indexer {indexerId} does not exist");
		}
	}

	// every term is either a plain term or a /regex/, invalid regexes are rejected early
	private static Task ValidateTermsAsync(ReleaseProfileRequest request)
	{
		foreach (var term in request.Required.Concat(request.Ignored))
		{
			if (term is ['/', _, .., '/'])
			{
				try
				{
					_ = new System.Text.RegularExpressions.Regex(term[1..^1]);
				}
				catch (ArgumentException exception)
				{
					throw new FluentValidation.ValidationException($"Invalid regex term '{term}': {exception.Message}");
				}
			}
		}

		return Task.CompletedTask;
	}

	private static async Task ApplyRequestAsync(
		SubmarineDbContext db,
		Core.Entities.ReleaseProfile profile,
		ReleaseProfileRequest request,
		CancellationToken cancellationToken)
	{
		profile.Name = request.Name;
		profile.Enabled = request.Enabled;
		profile.Required = [.. request.Required];
		profile.Ignored = [.. request.Ignored];
		profile.IndexerId = request.IndexerId;
		profile.Tags = await ResolveTagsAsync(db, request.Tags, cancellationToken);
	}

	private static async Task<List<Core.Entities.Tag>> ResolveTagsAsync(
		SubmarineDbContext db,
		IReadOnlyList<int>? tagIds,
		CancellationToken cancellationToken)
	{
		if (tagIds is null || tagIds.Count == 0)
		{
			return [];
		}

		var tags = await db.Tags.Where(tag => tagIds.Contains(tag.Id)).ToListAsync(cancellationToken);

		if (tags.Count != tagIds.Distinct().Count())
		{
			throw new KeyNotFoundException("Release profile references unknown tags");
		}

		return tags;
	}
}

/// <summary>A release profile resource.</summary>
/// <param name="Id">Id.</param>
/// <param name="Name">Name.</param>
/// <param name="Enabled">Whether the profile is active.</param>
/// <param name="Required">Required terms, substring or /regex/.</param>
/// <param name="Ignored">Ignored terms, substring or /regex/.</param>
/// <param name="IndexerId">Restrict the profile to one indexer.</param>
/// <param name="Tags">Tag ids this profile applies to.</param>
public sealed record ReleaseProfileResource(
	int Id,
	string Name,
	bool Enabled,
	IReadOnlyList<string> Required,
	IReadOnlyList<string> Ignored,
	int? IndexerId,
	IReadOnlyList<int> Tags)
{
	/// <summary>Projection from entity to resource.</summary>
	public static Expression<Func<Core.Entities.ReleaseProfile, ReleaseProfileResource>> From =>
		profile => new(
			profile.Id,
			profile.Name,
			profile.Enabled,
			profile.Required,
			profile.Ignored,
			profile.IndexerId,
			profile.Tags.Select(tag => tag.Id).ToList());

	/// <summary>Maps an entity to the resource.</summary>
	public static ReleaseProfileResource FromEntity(Core.Entities.ReleaseProfile profile)
		=> new(
			profile.Id,
			profile.Name,
			profile.Enabled,
			profile.Required,
			profile.Ignored,
			profile.IndexerId,
			[.. profile.Tags.Select(tag => tag.Id)]);
}

/// <summary>Create or update request for a release profile.</summary>
/// <param name="Name">Name.</param>
/// <param name="Enabled">Whether the profile is active.</param>
/// <param name="Required">Required terms, substring or /regex/.</param>
/// <param name="Ignored">Ignored terms, substring or /regex/.</param>
/// <param name="IndexerId">Restrict the profile to one indexer.</param>
/// <param name="Tags">Tag ids this profile applies to.</param>
public sealed record ReleaseProfileRequest(
	string Name,
	bool Enabled,
	IReadOnlyList<string> Required,
	IReadOnlyList<string> Ignored,
	int? IndexerId,
	IReadOnlyList<int>? Tags);

/// <summary>Validator for <see cref="ReleaseProfileRequest" />.</summary>
public sealed class ReleaseProfileRequestValidator : AbstractValidator<ReleaseProfileRequest>
{
	/// <inheritdoc />
	public ReleaseProfileRequestValidator()
	{
		RuleFor(request => request.Name)
			.NotEmpty()
			.MaximumLength(200);
	}
}
