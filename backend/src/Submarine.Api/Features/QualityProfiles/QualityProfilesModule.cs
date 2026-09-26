using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Profiles;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.QualityProfiles;

/// <summary>
///     Quality profile CRUD, schema, cloning and templates.
/// </summary>
public sealed class QualityProfilesModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/quality-profiles");
		group.MapGet("/", ListAsync);
		group.MapGet("/schema", GetSchemaAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPost("/", CreateAsync);
		group.MapPost("/from-template/{name}", CreateFromTemplateAsync);
		group.MapPost("/{id:int}/clone", CloneAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<PagedResult<QualityProfileResource>>> ListAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		CancellationToken cancellationToken)
	{
		// Items and FormatItems are JSON columns, so paging happens on materialized resources
		var profiles = await db.QualityProfiles
			.AsNoTracking()
			.OrderBy(profile => profile.Id)
			.ToListAsync(cancellationToken);

		return TypedResults.Ok(await PagedResult<QualityProfileResource>.CreateAsync(
			profiles.Select(ToResource).AsQueryable(),
			query,
			cancellationToken));
	}

	private static Ok<List<QualitySchemaGroup>> GetSchemaAsync()
		=> TypedResults.Ok(QualityResolutionModel.All
			.GroupBy(quality => quality.Resolution?.ToString() ?? "UNKNOWN")
			.Select(group => new QualitySchemaGroup(
				group.Key,
				group.Select(quality => new QualityModelResource(
					quality.Source?.ToString(),
					quality.Resolution?.ToString(),
					quality.Name))
					.ToList()))
			.ToList());

	private static async Task<Results<Ok<QualityProfileResource>, NotFound>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var profile = await db.QualityProfiles.AsNoTracking().FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

		return profile is null ? TypedResults.NotFound() : TypedResults.Ok(ToResource(profile));
	}

	private static async Task<Created<QualityProfileResource>> CreateAsync(
		SubmarineDbContext db,
		IValidator<QualityProfileRequest> validator,
		QualityProfileRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		await EnsureUniqueNameAsync(db, request.Name, null, cancellationToken);

		var profile = new Core.Entities.QualityProfile();
		ApplyRequest(profile, request);

		db.QualityProfiles.Add(profile);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Created($"/api/v1/quality-profiles/{profile.Id}", ToResource(profile));
	}

	private static async Task<Created<QualityProfileResource>> CreateFromTemplateAsync(
		string name,
		SubmarineDbContext db,
		CancellationToken cancellationToken)
	{
		var profile = QualityProfileTemplates.Create(name);
		await EnsureUniqueNameAsync(db, profile.Name, null, cancellationToken);

		db.QualityProfiles.Add(profile);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Created($"/api/v1/quality-profiles/{profile.Id}", ToResource(profile));
	}

	private static async Task<Results<Created<QualityProfileResource>, NotFound>> CloneAsync(
		int id,
		SubmarineDbContext db,
		CancellationToken cancellationToken)
	{
		var source = await db.QualityProfiles.AsNoTracking().FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
		if (source is null)
		{
			return TypedResults.NotFound();
		}

		var clone = new Core.Entities.QualityProfile
		{
			Name = $"{source.Name} Copy",
			UpgradeAllowed = source.UpgradeAllowed,
			Cutoff = source.Cutoff,
			Items = [.. source.Items],
			FormatItems = [.. source.FormatItems],
			MinFormatScore = source.MinFormatScore,
			CutoffFormatScore = source.CutoffFormatScore,
			MinUpgradeFormatScore = source.MinUpgradeFormatScore
		};

		var suffix = 2;
		while (await db.QualityProfiles.AnyAsync(profile => profile.Name == clone.Name, cancellationToken))
		{
			clone.Name = $"{source.Name} Copy {suffix++}";
		}

		db.QualityProfiles.Add(clone);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Created($"/api/v1/quality-profiles/{clone.Id}", ToResource(clone));
	}

	private static async Task<Results<Ok<QualityProfileResource>, NotFound>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		IValidator<QualityProfileRequest> validator,
		QualityProfileRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		await EnsureUniqueNameAsync(db, request.Name, id, cancellationToken);

		var profile = await db.QualityProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
		if (profile is null)
		{
			return TypedResults.NotFound();
		}

		ApplyRequest(profile, request);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Ok(ToResource(profile));
	}

	private static async Task<Results<NoContent, NotFound>> DeleteAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var profile = await db.QualityProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
		if (profile is null)
		{
			return TypedResults.NotFound();
		}

		var usedByVersions = await db.MediaVersions
			.Where(version => version.QualityProfileId == id)
			.Select(version => version.Name)
			.Take(5)
			.ToListAsync(cancellationToken);
		var usedByLists = await db.ImportLists
			.Where(list => list.QualityProfileId == id)
			.Select(list => list.Name)
			.Take(5)
			.ToListAsync(cancellationToken);
		var usedByCollections = await db.Collections
			.Where(collection => collection.QualityProfileId == id)
			.Select(collection => collection.Title)
			.Take(5)
			.ToListAsync(cancellationToken);

		if (usedByVersions.Count > 0 || usedByLists.Count > 0 || usedByCollections.Count > 0)
		{
			var users = string.Join(", ", usedByVersions.Concat(usedByLists).Concat(usedByCollections));

			throw new Submarine.Core.Common.ConflictException(
				$"Quality profile '{profile.Name}' is in use by: {users}");
		}

		db.QualityProfiles.Remove(profile);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.NoContent();
	}

	private static async Task EnsureUniqueNameAsync(
		SubmarineDbContext db,
		string name,
		int? excludeId,
		CancellationToken cancellationToken)
	{
		var exists = await db.QualityProfiles
			.AnyAsync(profile => profile.Name == name && (excludeId == null || profile.Id != excludeId), cancellationToken);

		if (exists)
		{
			throw new Submarine.Core.Common.ConflictException($"A quality profile named '{name}' already exists");
		}
	}

	private static void ApplyRequest(Core.Entities.QualityProfile profile, QualityProfileRequest request)
	{
		profile.Name = request.Name;
		profile.UpgradeAllowed = request.UpgradeAllowed;
		profile.Cutoff = request.Cutoff;
		profile.Items = [.. request.Items.Select(item => new Core.Entities.QualityProfileItem(
			new QualityResolutionModel(
				Enum.TryParse<QualitySource>(item.Quality.Source, out var source) ? source : null,
				Enum.TryParse<QualityResolution>(item.Quality.Resolution, out var resolution) ? resolution : null),
			item.Allowed))];
		profile.FormatItems = [.. (request.FormatItems ?? []).Select(item => new Core.Entities.ProfileFormatItem(item.CustomFormatId, item.Score))];
		profile.MinFormatScore = request.MinFormatScore;
		profile.CutoffFormatScore = request.CutoffFormatScore;
		profile.MinUpgradeFormatScore = request.MinUpgradeFormatScore;
	}

	private static QualityProfileResource ToResource(Core.Entities.QualityProfile profile)
		=> new(
			profile.Id,
			profile.Name,
			profile.UpgradeAllowed,
			profile.Cutoff,
			[.. profile.Items.Select(item => new QualityProfileItemResource(
				new QualityModelResource(
					item.Quality.Source?.ToString(),
					item.Quality.Resolution?.ToString(),
					item.Quality.Name),
				item.Allowed))],
			[.. profile.FormatItems.Select(item => new FormatItemResource(item.CustomFormatId, item.Score))],
			profile.MinFormatScore,
			profile.CutoffFormatScore,
			profile.MinUpgradeFormatScore);
}

/// <summary>A quality profile resource.</summary>
/// <param name="Id">Id.</param>
/// <param name="Name">Name.</param>
/// <param name="UpgradeAllowed">Whether upgrades beyond the cutoff are allowed.</param>
/// <param name="Cutoff">Index into Items of the cutoff quality.</param>
/// <param name="Items">Quality items, ordered low to high.</param>
/// <param name="FormatItems">Custom format scores.</param>
/// <param name="MinFormatScore">Minimum total custom format score to grab.</param>
/// <param name="CutoffFormatScore">Custom format score required to upgrade past the cutoff.</param>
/// <param name="MinUpgradeFormatScore">Minimum custom format score for further upgrades.</param>
public sealed record QualityProfileResource(
	int Id,
	string Name,
	bool UpgradeAllowed,
	int Cutoff,
	IReadOnlyList<QualityProfileItemResource> Items,
	IReadOnlyList<FormatItemResource> FormatItems,
	int MinFormatScore,
	int CutoffFormatScore,
	int MinUpgradeFormatScore);

/// <summary>One quality entry of a quality profile resource.</summary>
/// <param name="Quality">The quality.</param>
/// <param name="Allowed">Whether this quality may be grabbed.</param>
public sealed record QualityProfileItemResource(QualityModelResource Quality, bool Allowed);

/// <summary>A quality as source and resolution member names.</summary>
/// <param name="Source">Quality source member name, for example WEB_DL.</param>
/// <param name="Resolution">Resolution member name, for example R1080_P.</param>
/// <param name="Name">Display name, for example WebDL-1080p.</param>
public sealed record QualityModelResource(string? Source, string? Resolution, string Name);

/// <summary>A custom format score inside a profile.</summary>
/// <param name="CustomFormatId">Id of the custom format.</param>
/// <param name="Score">Score added when the format matches.</param>
public sealed record FormatItemResource(int CustomFormatId, int Score);

/// <summary>Create or update request for a quality profile.</summary>
/// <param name="Name">Name, unique.</param>
/// <param name="UpgradeAllowed">Whether upgrades beyond the cutoff are allowed.</param>
/// <param name="Cutoff">Index into Items of the cutoff quality, must be an allowed item.</param>
/// <param name="Items">Quality items, ordered low to high, at least one allowed.</param>
/// <param name="FormatItems">Custom format scores.</param>
/// <param name="MinFormatScore">Minimum total custom format score to grab.</param>
/// <param name="CutoffFormatScore">Custom format score required to upgrade past the cutoff.</param>
/// <param name="MinUpgradeFormatScore">Minimum custom format score for further upgrades.</param>
public sealed record QualityProfileRequest(
	string Name,
	bool UpgradeAllowed,
	int Cutoff,
	IReadOnlyList<QualityProfileItemRequest> Items,
	IReadOnlyList<FormatItemResource>? FormatItems,
	int MinFormatScore,
	int CutoffFormatScore,
	int MinUpgradeFormatScore);

/// <summary>One quality entry of a quality profile request.</summary>
/// <param name="Quality">The quality, given as enum member names.</param>
/// <param name="Allowed">Whether this quality may be grabbed.</param>
public sealed record QualityProfileItemRequest(QualityModelResource Quality, bool Allowed);

/// <summary>One quality in the schema response.</summary>
/// <param name="Resolution">Resolution member name the group is keyed by.</param>
/// <param name="Qualities">All sources available at this resolution.</param>
public sealed record QualitySchemaGroup(string Resolution, IReadOnlyList<QualityModelResource> Qualities);

/// <summary>Validator for <see cref="QualityProfileRequest" />.</summary>
public sealed class QualityProfileRequestValidator : AbstractValidator<QualityProfileRequest>
{
	/// <inheritdoc />
	public QualityProfileRequestValidator()
	{
		RuleFor(request => request.Name)
			.NotEmpty()
			.MaximumLength(200);

		RuleFor(request => request.Items)
			.NotEmpty()
			.WithMessage("At least one quality item is required");

		RuleFor(request => request.Items)
			.Must(items => items.Any(item => item.Allowed))
			.WithMessage("At least one quality must be allowed");

		RuleFor(request => request)
			.Must(request => request.Cutoff >= 0 && request.Cutoff < request.Items.Count)
			.WithMessage("The cutoff must point to an item of the profile")
			.Must(request => request.Cutoff >= 0 && request.Cutoff < request.Items.Count && request.Items[request.Cutoff].Allowed)
			.WithMessage("The cutoff must be an allowed quality");

		RuleForEach(request => request.Items)
			.Must(item => IsValidQuality(item.Quality))
			.WithMessage("Each quality must be a known source and resolution combination");
	}

	// Source-only qualities (UNKNOWN, CAM, ...) are valid members of QualityResolutionModel.All.
	private static bool IsValidQuality(QualityModelResource quality)
	{
		if (!Enum.TryParse<QualitySource>(quality.Source, out var source))
		{
			return false;
		}

		QualityResolution? resolution = Enum.TryParse<QualityResolution>(quality.Resolution, out var parsed) ? parsed : null;
		if (resolution is null && !string.IsNullOrEmpty(quality.Resolution))
		{
			return false;
		}

		return QualityResolutionModel.All.Any(known => known.Source == source && known.Resolution == resolution);
	}
}
