using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Modules;
using Submarine.Core.Profiles;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.QualityProfiles;

/// <summary>
///     Quality profile CRUD, schema, cloning and templates.
/// </summary>
public sealed class QualityProfilesModule : IEndpointModule, IServiceModule
{
	public void Register(IServiceCollection services, IConfiguration configuration)
		=> services.AddScoped<QualityProfileService>();

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
		QualityProfileService service,
		QualityProfileRequest request,
		CancellationToken cancellationToken)
	{
		var profile = await service.CreateAsync(request, cancellationToken);
		return TypedResults.Created($"/api/v1/quality-profiles/{profile.Id}", ToResource(profile));
	}

	private static async Task<Created<QualityProfileResource>> CreateFromTemplateAsync(
		string name,
		QualityProfileService service,
		CancellationToken cancellationToken)
	{
		var profile = await service.CreateFromTemplateAsync(name, cancellationToken);
		return TypedResults.Created($"/api/v1/quality-profiles/{profile.Id}", ToResource(profile));
	}

	private static async Task<Results<Created<QualityProfileResource>, NotFound>> CloneAsync(
		int id,
		QualityProfileService service,
		CancellationToken cancellationToken)
	{
		var clone = await service.CloneAsync(id, cancellationToken);
		return clone is null
			? TypedResults.NotFound()
			: TypedResults.Created($"/api/v1/quality-profiles/{clone.Id}", ToResource(clone));
	}

	private static async Task<Results<Ok<QualityProfileResource>, NotFound>> UpdateAsync(
		int id,
		QualityProfileService service,
		QualityProfileRequest request,
		CancellationToken cancellationToken)
	{
		var profile = await service.UpdateAsync(id, request, cancellationToken);
		return profile is null ? TypedResults.NotFound() : TypedResults.Ok(ToResource(profile));
	}

	private static async Task<Results<NoContent, NotFound>> DeleteAsync(
		int id,
		QualityProfileService service,
		CancellationToken cancellationToken)
		=> await service.DeleteAsync(id, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();


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
