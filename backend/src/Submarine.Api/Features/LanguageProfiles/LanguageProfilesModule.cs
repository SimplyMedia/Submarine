using Microsoft.AspNetCore.Http.HttpResults;
using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Languages;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.LanguageProfiles;

/// <summary>
///     Language profile CRUD and schema.
/// </summary>
public sealed class LanguageProfilesModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/language-profiles");
		group.MapGet("/", ListAsync);
		group.MapGet("/schema", GetSchemaAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPost("/", CreateAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<PagedResult<LanguageProfileResource>>> ListAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		CancellationToken cancellationToken)
		=> TypedResults.Ok(await PagedResult<LanguageProfileResource>.CreateAsync(
			db.LanguageProfiles.OrderBy(profile => profile.Id).Select(LanguageProfileResource.From),
			query,
			cancellationToken));

	private static Ok<List<LanguageSchemaItem>> GetSchemaAsync()
		=> TypedResults.Ok(Enum.GetValues<Language>()
			.Select(language => new LanguageSchemaItem(language.ToString()))
			.ToList());

	private static async Task<Results<Ok<LanguageProfileResource>, NotFound>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var profile = await db.LanguageProfiles.AsNoTracking().FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

		return profile is null ? TypedResults.NotFound() : TypedResults.Ok(LanguageProfileResource.FromEntity(profile));
	}

	private static async Task<Created<LanguageProfileResource>> CreateAsync(
		SubmarineDbContext db,
		IValidator<LanguageProfileRequest> validator,
		LanguageProfileRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		await EnsureUniqueNameAsync(db, request.Name, null, cancellationToken);

		var profile = new Core.Entities.LanguageProfile();
		ApplyRequest(profile, request);

		db.LanguageProfiles.Add(profile);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Created($"/api/v1/language-profiles/{profile.Id}", LanguageProfileResource.FromEntity(profile));
	}

	private static async Task<Results<Ok<LanguageProfileResource>, NotFound>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		IValidator<LanguageProfileRequest> validator,
		LanguageProfileRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		await EnsureUniqueNameAsync(db, request.Name, id, cancellationToken);

		var profile = await db.LanguageProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
		if (profile is null)
		{
			return TypedResults.NotFound();
		}

		ApplyRequest(profile, request);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Ok(LanguageProfileResource.FromEntity(profile));
	}

	private static async Task<Results<NoContent, NotFound>> DeleteAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var profile = await db.LanguageProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
		if (profile is null)
		{
			return TypedResults.NotFound();
		}

		var users = await db.MediaVersions
			.Where(version => version.LanguageProfileId == id)
			.Select(version => version.Name)
			.Take(5)
			.ToListAsync(cancellationToken);
		users.AddRange(await db.ImportLists
			.Where(list => list.LanguageProfileId == id)
			.Select(list => list.Name)
			.Take(5)
			.ToListAsync(cancellationToken));

		if (users.Count > 0)
		{
			throw new Submarine.Core.Common.ConflictException($"Language profile '{profile.Name}' is in use by: {string.Join(", ", users)}");
		}

		db.LanguageProfiles.Remove(profile);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.NoContent();
	}

	private static async Task EnsureUniqueNameAsync(
		SubmarineDbContext db,
		string name,
		int? excludeId,
		CancellationToken cancellationToken)
	{
		var exists = await db.LanguageProfiles
			.AnyAsync(profile => profile.Name == name && (excludeId == null || profile.Id != excludeId), cancellationToken);

		if (exists)
		{
			throw new Submarine.Core.Common.ConflictException($"A language profile named '{name}' already exists");
		}
	}

	private static void ApplyRequest(Core.Entities.LanguageProfile profile, LanguageProfileRequest request)
	{
		profile.Name = request.Name;
		profile.Languages = [.. request.Languages];
		profile.Cutoff = request.Cutoff;
		profile.UpgradeAllowed = request.UpgradeAllowed;
	}
}

/// <summary>A language profile resource.</summary>
/// <param name="Id">Id.</param>
/// <param name="Name">Name.</param>
/// <param name="Languages">Wanted languages, best first.</param>
/// <param name="Cutoff">Cutoff language, upgrades stop once reached.</param>
/// <param name="UpgradeAllowed">Whether upgrades beyond the cutoff are allowed.</param>
public sealed record LanguageProfileResource(
	int Id,
	string Name,
	IReadOnlyList<Language> Languages,
	Language Cutoff,
	bool UpgradeAllowed)
{
	/// <summary>Expression mapping an entity to the resource.</summary>
	public static Expression<Func<Core.Entities.LanguageProfile, LanguageProfileResource>> From =>
		profile => new(profile.Id, profile.Name, profile.Languages, profile.Cutoff, profile.UpgradeAllowed);

	/// <summary>Maps an entity to the resource.</summary>
	public static LanguageProfileResource FromEntity(Core.Entities.LanguageProfile profile)
		=> new(profile.Id, profile.Name, profile.Languages, profile.Cutoff, profile.UpgradeAllowed);
}

/// <summary>Create or update request for a language profile.</summary>
/// <param name="Name">Name, unique.</param>
/// <param name="Languages">Wanted languages, best first.</param>
/// <param name="Cutoff">Cutoff language, must be part of Languages.</param>
/// <param name="UpgradeAllowed">Whether upgrades beyond the cutoff are allowed.</param>
public sealed record LanguageProfileRequest(string Name, IReadOnlyList<Language> Languages, Language Cutoff, bool UpgradeAllowed);

/// <summary>One language in the schema response.</summary>
/// <param name="Value">Language member name, for example ENGLISH.</param>
public sealed record LanguageSchemaItem(string Value);

/// <summary>Validator for <see cref="LanguageProfileRequest" />.</summary>
public sealed class LanguageProfileRequestValidator : AbstractValidator<LanguageProfileRequest>
{
	/// <inheritdoc />
	public LanguageProfileRequestValidator()
	{
		RuleFor(request => request.Name)
			.NotEmpty()
			.MaximumLength(200);

		RuleFor(request => request.Languages)
			.NotEmpty()
			.WithMessage("At least one language is required");

		RuleFor(request => request.Languages)
			.Must(languages => languages.Count == languages.Distinct().Count())
			.WithMessage("Languages must be unique");

		RuleFor(request => request)
			.Must(request => request.Languages.Contains(request.Cutoff))
			.WithMessage("The cutoff must be one of the languages");
	}
}
