using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Core.Entities;
using Submarine.Core.Quality;
using Submarine.Core.Profiles;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.QualityProfiles;

/// <summary>Native quality profile mutations shared by the native and compatibility transports.</summary>
public sealed class QualityProfileService(SubmarineDbContext db, IValidator<QualityProfileRequest> validator)
{
	public async Task<QualityProfile> CreateAsync(QualityProfileRequest request, CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		await EnsureUniqueNameAsync(request.Name, null, cancellationToken);

		var profile = new QualityProfile();
		ApplyRequest(profile, request);
		db.QualityProfiles.Add(profile);
		await db.SaveChangesAsync(cancellationToken);
		return profile;
	}

	public async Task<QualityProfile> CreateFromTemplateAsync(string name, CancellationToken cancellationToken)
	{
		var profile = QualityProfileTemplates.Create(name);
		await EnsureUniqueNameAsync(profile.Name, null, cancellationToken);
		db.QualityProfiles.Add(profile);
		await db.SaveChangesAsync(cancellationToken);
		return profile;
	}

	public async Task<QualityProfile?> CloneAsync(int id, CancellationToken cancellationToken)
	{
		var source = await db.QualityProfiles.AsNoTracking().FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
		if (source is null)
			return null;

		var clone = new QualityProfile
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
			clone.Name = $"{source.Name} Copy {suffix++}";

		db.QualityProfiles.Add(clone);
		await db.SaveChangesAsync(cancellationToken);
		return clone;
	}

	public async Task<QualityProfile?> UpdateAsync(int id, QualityProfileRequest request, CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		await EnsureUniqueNameAsync(request.Name, id, cancellationToken);
		var profile = await db.QualityProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
		if (profile is null)
			return null;
		ApplyRequest(profile, request);
		await db.SaveChangesAsync(cancellationToken);
		return profile;
	}

	public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
	{
		var profile = await db.QualityProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
		if (profile is null)
			return false;

		var usedByVersions = await db.MediaVersions.Where(version => version.QualityProfileId == id).Select(version => version.Name).Take(5).ToListAsync(cancellationToken);
		var usedByLists = await db.ImportLists.Where(list => list.QualityProfileId == id).Select(list => list.Name).Take(5).ToListAsync(cancellationToken);
		var usedByCollections = await db.Collections.Where(collection => collection.QualityProfileId == id).Select(collection => collection.Title).Take(5).ToListAsync(cancellationToken);
		if (usedByVersions.Count > 0 || usedByLists.Count > 0 || usedByCollections.Count > 0)
		{
			var users = string.Join(", ", usedByVersions.Concat(usedByLists).Concat(usedByCollections));
			throw new Submarine.Core.Common.ConflictException($"Quality profile '{profile.Name}' is in use by: {users}");
		}

		db.QualityProfiles.Remove(profile);
		await db.SaveChangesAsync(cancellationToken);
		return true;
	}


	private async Task EnsureUniqueNameAsync(string name, int? excludeId, CancellationToken cancellationToken)
	{
		if (await db.QualityProfiles.AnyAsync(profile => profile.Name == name && (excludeId == null || profile.Id != excludeId), cancellationToken))
			throw new Submarine.Core.Common.ConflictException($"A quality profile named '{name}' already exists");
	}

	private static void ApplyRequest(QualityProfile profile, QualityProfileRequest request)
	{
		profile.Name = request.Name;
		profile.UpgradeAllowed = request.UpgradeAllowed;
		profile.Cutoff = request.Cutoff;
		profile.Items = [.. request.Items.Select(item => new QualityProfileItem(
			new QualityResolutionModel(
				Enum.TryParse<QualitySource>(item.Quality.Source, out var source) ? source : null,
				Enum.TryParse<QualityResolution>(item.Quality.Resolution, out var resolution) ? resolution : null),
			item.Allowed))];
		profile.FormatItems = [.. (request.FormatItems ?? []).Select(item => new ProfileFormatItem(item.CustomFormatId, item.Score))];
		profile.MinFormatScore = request.MinFormatScore;
		profile.CutoffFormatScore = request.CutoffFormatScore;
		profile.MinUpgradeFormatScore = request.MinUpgradeFormatScore;
	}
}
