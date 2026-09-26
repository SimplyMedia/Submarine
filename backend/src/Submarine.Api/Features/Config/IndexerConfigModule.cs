using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Config;

/// <summary>
///     Indexer behaviour configuration.
/// </summary>
public sealed class IndexerConfigModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/config/indexer");
		group.MapGet("/", GetAsync);
		group.MapPut("/", PutAsync);
	}

	private static async Task<Ok<IndexerConfigResource>> GetAsync(SubmarineDbContext db, CancellationToken cancellationToken)
		=> TypedResults.Ok(IndexerConfigResource.FromEntity(await db.IndexerConfig.AsNoTracking().SingleAsync(cancellationToken)));

	private static async Task<Ok<IndexerConfigResource>> PutAsync(
		SubmarineDbContext db,
		IValidator<IndexerConfigResource> validator,
		IndexerConfigResource request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var config = await db.IndexerConfig.SingleAsync(cancellationToken);

		config.RssSyncIntervalMinutes = request.RssSyncIntervalMinutes;
		config.MinimumAgeMinutes = request.MinimumAgeMinutes;
		config.RetentionDays = request.RetentionDays;
		config.MaximumSizeMb = request.MaximumSizeMb;
		config.AvailabilityDelayDays = request.AvailabilityDelayDays;
		config.AllowHardcodedSubs = request.AllowHardcodedSubs;
		config.WhitelistedHardcodedSubs = request.WhitelistedHardcodedSubs;

		// The scheduler enqueues RssSync on this interval; 0 disables it entirely.
		var rssSyncTask = await db.ScheduledTasks.FirstOrDefaultAsync(x => x.Name == "RssSync", cancellationToken);
		if (rssSyncTask is not null)
		{
			rssSyncTask.IntervalMinutes = request.RssSyncIntervalMinutes;
		}

		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Ok(IndexerConfigResource.FromEntity(config));
	}
}

/// <summary>Indexer behaviour configuration resource.</summary>
/// <param name="RssSyncIntervalMinutes">Minutes between RSS syncs, 0 disables the sync.</param>
/// <param name="MinimumAgeMinutes">Minimum age in minutes for usenet releases.</param>
/// <param name="RetentionDays">Retention in days required for usenet releases, 0 disables.</param>
/// <param name="MaximumSizeMb">Maximum release size in MB, 0 disables.</param>
/// <param name="AvailabilityDelayDays">Days a movie must be past its release date before grabbing, 0 disables.</param>
/// <param name="AllowHardcodedSubs">Allow releases reporting hardcoded subtitles.</param>
/// <param name="WhitelistedHardcodedSubs">Comma separated release groups allowed to have hardcoded subtitles even when <see cref="AllowHardcodedSubs" /> is off.</param>
public sealed record IndexerConfigResource(
	int RssSyncIntervalMinutes,
	int MinimumAgeMinutes,
	int RetentionDays,
	int MaximumSizeMb,
	int AvailabilityDelayDays,
	bool AllowHardcodedSubs,
	string WhitelistedHardcodedSubs)
{
	/// <summary>Maps the singleton to the resource.</summary>
	public static IndexerConfigResource FromEntity(Core.Entities.IndexerConfig config)
		=> new(
			config.RssSyncIntervalMinutes,
			config.MinimumAgeMinutes,
			config.RetentionDays,
			config.MaximumSizeMb,
			config.AvailabilityDelayDays,
			config.AllowHardcodedSubs,
			config.WhitelistedHardcodedSubs);
}

/// <summary>Validator for <see cref="IndexerConfigResource" /> PUT requests.</summary>
public sealed class IndexerConfigResourceValidator : AbstractValidator<IndexerConfigResource>
{
	/// <inheritdoc />
	public IndexerConfigResourceValidator()
	{
		RuleFor(x => x.RssSyncIntervalMinutes).GreaterThanOrEqualTo(0);
		RuleFor(x => x.MinimumAgeMinutes).GreaterThanOrEqualTo(0);
		RuleFor(x => x.RetentionDays).GreaterThanOrEqualTo(0);
		RuleFor(x => x.MaximumSizeMb).GreaterThanOrEqualTo(0);
		RuleFor(x => x.AvailabilityDelayDays).GreaterThanOrEqualTo(0);
	}
}
