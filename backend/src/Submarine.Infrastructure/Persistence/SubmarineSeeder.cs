using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;

namespace Submarine.Infrastructure.Persistence;

/// <summary>
///     Seeds config singletons, quality definitions, default profiles and scheduled tasks.
///     Every step is idempotent, so seeding can run on every startup.
/// </summary>
public sealed class SubmarineSeeder(SubmarineDbContext db, TimeProvider timeProvider)
{
	private static readonly (string Name, int IntervalMinutes)[] DefaultTasks =
	[
		("RssSync", 30),
		("DownloadMonitor", 1),
		("RefreshMetadata", 720),
		("ImportListSync", 360),
		("Backup", 10080),
		("HealthCheck", 30),
		("IndexerDefinitionSync", 1440),
		("RecycleBinCleanup", 1440),
		("CommandCleanup", 1440)
	];

	/// <summary>
	///     Seed all defaults. Call once per scope.
	/// </summary>
	public async Task SeedAsync(CancellationToken cancellationToken = default)
	{
		var now = timeProvider.GetUtcNow().UtcDateTime;

		var general = GetOrCreate(() => new GeneralConfig
		{
			ApiKey = NewToken(),
			FeedToken = NewToken()
		}, now);
		if (string.IsNullOrEmpty(general.ApiKey))
		{
			general.ApiKey = NewToken();
		}

		if (string.IsNullOrEmpty(general.FeedToken))
		{
			general.FeedToken = NewToken();
		}

		GetOrCreate(() => new NamingConfig(), now);
		GetOrCreate(() => new MediaManagementConfig(), now);
		GetOrCreate(() => new IndexerConfig(), now);
		GetOrCreate(() => new DownloadConfig(), now);
		GetOrCreate(() => new UiConfig(), now);

		await SeedQualityDefinitionsAsync(cancellationToken);

		if (!await db.QualityProfiles.AnyAsync(cancellationToken))
		{
			var items = QualityResolutionModel.All.Select(quality => new QualityProfileItem(quality, true)).ToList();
			db.QualityProfiles.Add(new QualityProfile
			{
				Name = "Any",
				UpgradeAllowed = true,
				Cutoff = items.Count - 1,
				Items = items
			});
		}

		if (!await db.LanguageProfiles.AnyAsync(cancellationToken))
		{
			db.LanguageProfiles.Add(new LanguageProfile
			{
				Name = "English",
				Languages = [Language.ENGLISH],
				Cutoff = Language.ENGLISH,
				UpgradeAllowed = true
			});
		}

		if (!await db.DelayProfiles.AnyAsync(cancellationToken))
		{
			db.DelayProfiles.Add(new DelayProfile
			{
				Name = "Default",
				PreferredProtocol = Protocol.USENET,
				UsenetDelayMinutes = 0,
				TorrentDelayMinutes = 60
			});
		}

		var existingTasks = (await db.ScheduledTasks.Select(x => x.Name).ToListAsync(cancellationToken))
			.ToHashSet(StringComparer.Ordinal);
		foreach (var (name, interval) in DefaultTasks)
		{
			if (existingTasks.Contains(name))
			{
				continue;
			}

			// HealthCheck and IndexerDefinitionSync run once immediately on a fresh install, so a
			// broken setup (unreachable sibling service, missing definitions) is visible right away
			// instead of only after the first scheduled interval.
			var nextRun = name is "HealthCheck" or "IndexerDefinitionSync" ? now : now.AddMinutes(interval);
			db.ScheduledTasks.Add(new ScheduledTask
			{
				Name = name,
				IntervalMinutes = interval,
				NextRun = nextRun
			});
		}

		await db.SaveChangesAsync(cancellationToken);
	}

	private async Task SeedQualityDefinitionsAsync(CancellationToken cancellationToken)
	{
		var existing = (await db.QualityDefinitions
				.Select(x => new { x.Source, x.Resolution })
				.ToListAsync(cancellationToken))
			.Select(x => (x.Source, x.Resolution))
			.ToHashSet();

		foreach (var quality in QualityResolutionModel.All)
		{
			if (existing.Contains((quality.Source, quality.Resolution)))
			{
				continue;
			}

			db.QualityDefinitions.Add(new QualityDefinition
			{
				Source = quality.Source,
				Resolution = quality.Resolution,
				Title = quality.Name
			});
		}
	}

	private T GetOrCreate<T>(Func<T> factory, DateTime now)
		where T : SingletonEntity, new()
	{
		var existing = db.Set<T>().Local.FirstOrDefault()
			?? db.Set<T>().OrderBy(x => x.Id).FirstOrDefault();
		if (existing is not null)
		{
			return existing;
		}

		var created = factory();
		created.Id = 1;
		created.UpdatedAt = now;
		db.Set<T>().Add(created);
		return created;
	}

	private static string NewToken() => RandomNumberGenerator.GetHexString(32);
}
