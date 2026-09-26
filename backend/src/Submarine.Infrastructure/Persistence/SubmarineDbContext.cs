using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence.Converters;

namespace Submarine.Infrastructure.Persistence;

/// <summary>
///     Main application database. Register a derived context for the concrete provider.
/// </summary>
public class SubmarineDbContext(DbContextOptions options, TimeProvider timeProvider) : DbContext(options)
{
	/// <summary>Users able to log in.</summary>
	public DbSet<User> Users => Set<User>();

	/// <summary>General configuration singleton.</summary>
	public DbSet<GeneralConfig> GeneralConfig => Set<GeneralConfig>();

	/// <summary>Naming configuration singleton.</summary>
	public DbSet<NamingConfig> NamingConfig => Set<NamingConfig>();

	/// <summary>Media management configuration singleton.</summary>
	public DbSet<MediaManagementConfig> MediaManagementConfig => Set<MediaManagementConfig>();

	/// <summary>Indexer configuration singleton.</summary>
	public DbSet<IndexerConfig> IndexerConfig => Set<IndexerConfig>();

	/// <summary>Download configuration singleton.</summary>
	public DbSet<DownloadConfig> DownloadConfig => Set<DownloadConfig>();

	/// <summary>UI configuration singleton.</summary>
	public DbSet<UiConfig> UiConfig => Set<UiConfig>();

	/// <summary>Tags.</summary>
	public DbSet<Tag> Tags => Set<Tag>();

	/// <summary>Root folders.</summary>
	public DbSet<RootFolder> RootFolders => Set<RootFolder>();

	/// <summary>Series.</summary>
	public DbSet<Series> Series => Set<Series>();

	/// <summary>Seasons.</summary>
	public DbSet<Season> Seasons => Set<Season>();

	/// <summary>Episodes.</summary>
	public DbSet<Episode> Episodes => Set<Episode>();

	/// <summary>Movies.</summary>
	public DbSet<Movie> Movies => Set<Movie>();

	/// <summary>Collections.</summary>
	public DbSet<Collection> Collections => Set<Collection>();

	/// <summary>Versions.</summary>
	public DbSet<MediaVersion> MediaVersions => Set<MediaVersion>();

	/// <summary>Episode files.</summary>
	public DbSet<EpisodeFile> EpisodeFiles => Set<EpisodeFile>();

	/// <summary>Movie files.</summary>
	public DbSet<MovieFile> MovieFiles => Set<MovieFile>();

	/// <summary>Alternative titles.</summary>
	public DbSet<AlternativeTitle> AlternativeTitles => Set<AlternativeTitle>();

	/// <summary>Quality profiles.</summary>
	public DbSet<QualityProfile> QualityProfiles => Set<QualityProfile>();

	/// <summary>Quality definitions.</summary>
	public DbSet<QualityDefinition> QualityDefinitions => Set<QualityDefinition>();

	/// <summary>Language profiles.</summary>
	public DbSet<LanguageProfile> LanguageProfiles => Set<LanguageProfile>();

	/// <summary>Delay profiles.</summary>
	public DbSet<DelayProfile> DelayProfiles => Set<DelayProfile>();

	/// <summary>Release profiles.</summary>
	public DbSet<ReleaseProfile> ReleaseProfiles => Set<ReleaseProfile>();

	/// <summary>Custom formats.</summary>
	public DbSet<CustomFormat> CustomFormats => Set<CustomFormat>();

	/// <summary>Release filters.</summary>
	public DbSet<ReleaseFilter> ReleaseFilters => Set<ReleaseFilter>();

	/// <summary>Release group quality overrides.</summary>
	public DbSet<ReleaseGroupQualityOverride> ReleaseGroupQualityOverrides => Set<ReleaseGroupQualityOverride>();

	/// <summary>Indexers.</summary>
	public DbSet<Indexer> Indexers => Set<Indexer>();

	/// <summary>Cardigann definitions.</summary>
	public DbSet<IndexerDefinition> IndexerDefinitions => Set<IndexerDefinition>();

	/// <summary>Indexer proxies.</summary>
	public DbSet<IndexerProxy> IndexerProxies => Set<IndexerProxy>();

	/// <summary>Indexer runtime statuses.</summary>
	public DbSet<IndexerStatus> IndexerStatuses => Set<IndexerStatus>();

	/// <summary>Indexer request history.</summary>
	public DbSet<IndexerHistory> IndexerHistories => Set<IndexerHistory>();

	/// <summary>Download clients.</summary>
	public DbSet<DownloadClient> DownloadClients => Set<DownloadClient>();

	/// <summary>Remote path mappings.</summary>
	public DbSet<RemotePathMapping> RemotePathMappings => Set<RemotePathMapping>();

	/// <summary>Tracked downloads.</summary>
	public DbSet<TrackedDownload> TrackedDownloads => Set<TrackedDownload>();

	/// <summary>History entries.</summary>
	public DbSet<HistoryEvent> HistoryEvents => Set<HistoryEvent>();

	/// <summary>Blocklist entries.</summary>
	public DbSet<BlocklistItem> BlocklistItems => Set<BlocklistItem>();

	/// <summary>Pending releases.</summary>
	public DbSet<PendingRelease> PendingReleases => Set<PendingRelease>();

	/// <summary>Import lists.</summary>
	public DbSet<ImportList> ImportLists => Set<ImportList>();

	/// <summary>Import list exclusions.</summary>
	public DbSet<ImportListExclusion> ImportListExclusions => Set<ImportListExclusion>();

	/// <summary>Notifications.</summary>
	public DbSet<Notification> Notifications => Set<Notification>();

	/// <summary>Command queue rows.</summary>
	public DbSet<Command> Commands => Set<Command>();

	/// <summary>Scheduled tasks.</summary>
	public DbSet<ScheduledTask> ScheduledTasks => Set<ScheduledTask>();

	/// <summary>Health issues.</summary>
	public DbSet<HealthIssue> HealthIssues => Set<HealthIssue>();

	/// <inheritdoc />
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(SubmarineDbContext).Assembly);

		foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entityType => entityType.GetProperties()))
		{
			var converterType = property.GetValueConverter()?.GetType();
			if (converterType is { IsGenericType: true } && converterType.GetGenericTypeDefinition() == typeof(JsonValueConverter<>))
			{
				var comparerType = typeof(JsonValueComparer<>).MakeGenericType(converterType.GetGenericArguments()[0]);
				property.SetValueComparer((ValueComparer)Activator.CreateInstance(comparerType)!);
			}
		}
	}

	/// <inheritdoc />
	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
		configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
	}

	/// <inheritdoc />
	public override int SaveChanges(bool acceptAllChangesOnSuccess)
	{
		TouchTimestamps();
		return base.SaveChanges(acceptAllChangesOnSuccess);
	}

	/// <inheritdoc />
	public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
	{
		TouchTimestamps();
		return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
	}

	private void TouchTimestamps()
	{
		var now = timeProvider.GetUtcNow().UtcDateTime;
		foreach (var entry in ChangeTracker.Entries<Entity>())
		{
			if (entry.State == EntityState.Added)
			{
				entry.Entity.CreatedAt = now;
			}

			if (entry.State is EntityState.Added or EntityState.Modified)
			{
				entry.Entity.UpdatedAt = now;
			}
		}

		foreach (var entry in ChangeTracker.Entries<SingletonEntity>())
		{
			if (entry.State is EntityState.Added or EntityState.Modified)
			{
				entry.Entity.UpdatedAt = now;
			}
		}
	}
}
