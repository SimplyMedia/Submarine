using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Shared;

public sealed class CompatLanguageProfileEndpoints : IEndpointModule
{
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = CompatRoutes.CreateRestGroup(endpoints, "sonarr", "v3");
		group.MapGet("/languageprofile", (SubmarineDbContext db, CancellationToken ct) => ListAsync(db, ct));
		group.MapGet("/languageprofile/{id:int}", (int id, SubmarineDbContext db, CancellationToken ct) => GetAsync(id, db, ct));
	}

	private static async Task<IResult> ListAsync(SubmarineDbContext db, CancellationToken ct)
	{
		var profiles = await db.LanguageProfiles.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
		return Results.Json(profiles.Select(Project).ToArray(), CompatJson.Options);
	}

	private static async Task<IResult> GetAsync(int id, SubmarineDbContext db, CancellationToken ct)
	{
		var profile = await db.LanguageProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
		return profile is null
			? CompatErrors.Message("Language profile not found.", StatusCodes.Status404NotFound)
			: Results.Json(Project(profile), CompatJson.Options);
	}

	private static object Project(LanguageProfile profile)
		=> new
		{
			id = profile.Id,
			name = profile.Name,
			upgradeAllowed = profile.UpgradeAllowed,
			cutoff = LanguageResource(profile.Cutoff),
			languages = profile.Languages.Select(language => new { language = LanguageResource(language), allowed = true }).ToArray()
		};

	private static object LanguageResource(Submarine.Core.Languages.Language language)
		=> new { id = CompatLanguageMap.ToUpstreamId(language, "sonarr"), name = CompatLanguageMap.Name(language) };
}
