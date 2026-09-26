using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Core.Provider;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.DelayProfiles;

/// <summary>
///     Delay profile CRUD and reordering.
/// </summary>
public sealed class DelayProfilesModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/delay-profiles");
		group.MapGet("/", ListAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPost("/", CreateAsync);
		group.MapPut("/reorder", ReorderAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<List<DelayProfileResource>>> ListAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var profiles = await db.DelayProfiles
			.Include(profile => profile.Tags)
			.OrderBy(profile => profile.Order)
			.ToListAsync(cancellationToken);

		return TypedResults.Ok(profiles.Select(DelayProfileResource.FromEntity).ToList());
	}

	private static async Task<Results<Ok<DelayProfileResource>, NotFound>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var profile = await db.DelayProfiles
			.Include(entity => entity.Tags)
			.AsNoTracking()
			.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

		return profile is null ? TypedResults.NotFound() : TypedResults.Ok(DelayProfileResource.FromEntity(profile));
	}

	private static async Task<Created<DelayProfileResource>> CreateAsync(
		SubmarineDbContext db,
		DelayProfileRequest request,
		CancellationToken cancellationToken)
	{
		var profile = new Core.Entities.DelayProfile
		{
			Order = (await db.DelayProfiles.MaxAsync(existing => (int?)existing.Order, cancellationToken) ?? 0) + 1
		};
		await ApplyRequestAsync(db, profile, request, cancellationToken);

		db.DelayProfiles.Add(profile);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Created($"/api/v1/delay-profiles/{profile.Id}", DelayProfileResource.FromEntity(profile));
	}

	private static async Task<NoContent> ReorderAsync(
		SubmarineDbContext db,
		int[] ids,
		CancellationToken cancellationToken)
	{
		var profiles = await db.DelayProfiles.ToListAsync(cancellationToken);
		var knownIds = profiles.Select(profile => profile.Id).ToHashSet();

		if (ids.Any(id => !knownIds.Contains(id)))
		{
			throw new KeyNotFoundException("Reorder list contains unknown delay profile ids");
		}

		foreach (var profile in profiles)
		{
			var index = Array.IndexOf(ids, profile.Id);
			profile.Order = index >= 0 ? index + 1 : ids.Length + 1;
		}

		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.NoContent();
	}

	private static async Task<Results<Ok<DelayProfileResource>, NotFound>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		DelayProfileRequest request,
		CancellationToken cancellationToken)
	{
		var profile = await db.DelayProfiles
			.Include(entity => entity.Tags)
			.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
		if (profile is null)
		{
			return TypedResults.NotFound();
		}

		await ApplyRequestAsync(db, profile, request, cancellationToken);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Ok(DelayProfileResource.FromEntity(profile));
	}

	private static async Task<Results<NoContent, NotFound>> DeleteAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var profile = await db.DelayProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
		if (profile is null)
		{
			return TypedResults.NotFound();
		}

		db.DelayProfiles.Remove(profile);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.NoContent();
	}

	private static async Task ApplyRequestAsync(
		SubmarineDbContext db,
		Core.Entities.DelayProfile profile,
		DelayProfileRequest request,
		CancellationToken cancellationToken)
	{
		profile.Name = request.Name;
		profile.PreferredProtocol = request.PreferredProtocol;
		profile.EnableUsenet = request.EnableUsenet;
		profile.EnableTorrent = request.EnableTorrent;
		profile.UsenetDelayMinutes = request.UsenetDelayMinutes;
		profile.TorrentDelayMinutes = request.TorrentDelayMinutes;
		profile.BypassIfHighestQuality = request.BypassIfHighestQuality;
		profile.BypassIfAboveCustomFormatScore = request.BypassIfAboveCustomFormatScore;
		profile.MinimumCustomFormatScore = request.MinimumCustomFormatScore;
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
			throw new KeyNotFoundException("Delay profile references unknown tags");
		}

		return tags;
	}
}

/// <summary>A delay profile resource.</summary>
/// <param name="Id">Id.</param>
/// <param name="Name">Name.</param>
/// <param name="PreferredProtocol">Protocol preferred when both are available.</param>
/// <param name="EnableUsenet">Whether usenet releases are grabbed for tagged media.</param>
/// <param name="EnableTorrent">Whether torrent releases are grabbed for tagged media.</param>
/// <param name="UsenetDelayMinutes">Delay in minutes for usenet releases.</param>
/// <param name="TorrentDelayMinutes">Delay in minutes for torrent releases.</param>
/// <param name="BypassIfHighestQuality">Bypass the delay when the release is the highest allowed quality.</param>
/// <param name="BypassIfAboveCustomFormatScore">Bypass the delay when the release scores above the custom format threshold.</param>
/// <param name="MinimumCustomFormatScore">Custom format score required for the score bypass.</param>
/// <param name="Order">Sort order, lower matches first.</param>
/// <param name="Tags">Tag ids this profile applies to.</param>
public sealed record DelayProfileResource(
	int Id,
	string Name,
	Protocol PreferredProtocol,
	bool EnableUsenet,
	bool EnableTorrent,
	int UsenetDelayMinutes,
	int TorrentDelayMinutes,
	bool BypassIfHighestQuality,
	bool BypassIfAboveCustomFormatScore,
	int MinimumCustomFormatScore,
	int Order,
	IReadOnlyList<int> Tags)
{
	/// <summary>Maps an entity to the resource.</summary>
	public static DelayProfileResource FromEntity(Core.Entities.DelayProfile profile)
		=> new(
			profile.Id,
			profile.Name,
			profile.PreferredProtocol,
			profile.EnableUsenet,
			profile.EnableTorrent,
			profile.UsenetDelayMinutes,
			profile.TorrentDelayMinutes,
			profile.BypassIfHighestQuality,
			profile.BypassIfAboveCustomFormatScore,
			profile.MinimumCustomFormatScore,
			profile.Order,
			[.. profile.Tags.Select(tag => tag.Id)]);
}

/// <summary>Create or update request for a delay profile.</summary>
/// <param name="Name">Name.</param>
/// <param name="PreferredProtocol">Protocol preferred when both are available.</param>
/// <param name="EnableUsenet">Whether usenet releases are grabbed for tagged media.</param>
/// <param name="EnableTorrent">Whether torrent releases are grabbed for tagged media.</param>
/// <param name="UsenetDelayMinutes">Delay in minutes for usenet releases.</param>
/// <param name="TorrentDelayMinutes">Delay in minutes for torrent releases.</param>
/// <param name="BypassIfHighestQuality">Bypass the delay when the release is the highest allowed quality.</param>
/// <param name="BypassIfAboveCustomFormatScore">Bypass the delay when the release scores above the custom format threshold.</param>
/// <param name="MinimumCustomFormatScore">Custom format score required for the score bypass.</param>
/// <param name="Tags">Tag ids this profile applies to.</param>
public sealed record DelayProfileRequest(
	string Name,
	Protocol PreferredProtocol,
	bool EnableUsenet,
	bool EnableTorrent,
	int UsenetDelayMinutes,
	int TorrentDelayMinutes,
	bool BypassIfHighestQuality,
	bool BypassIfAboveCustomFormatScore,
	int MinimumCustomFormatScore,
	IReadOnlyList<int>? Tags);
