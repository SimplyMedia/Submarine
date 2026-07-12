using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Submarine.Core.Config;
using Submarine.Core.Database;
using Submarine.Core.DecisionEngine.CustomFormats;
using Submarine.Core.DecisionEngine.Filter;
using Submarine.Core.Download;
using Submarine.Core.History;
using Submarine.Core.ImportList;
using Submarine.Core.Library;
using Submarine.Core.Notification;
using Submarine.Core.MediaFile;
using Submarine.Core.Profile;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Tag = Submarine.Core.Tag.Tag;

namespace Submarine.Api.Models.Database;

/// <summary>
///     Submarine Database Context
/// </summary>
public class SubmarineDatabaseContext : DbContext
{
	/// <summary>
	///     Configuration of this Database Context
	/// </summary>
	protected readonly IConfiguration Configuration;

	private static readonly ValueComparer<QualityModel> QualityValueComparer = new(
		(a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
		          == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
		q => JsonSerializer.Serialize(q, (JsonSerializerOptions?)null).GetHashCode(),
		q => JsonSerializer.Deserialize<QualityModel>(
			JsonSerializer.Serialize(q, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);

	public DbSet<Provider> Providers { get; set; }

	public DbSet<Tag> Tags { get; set; }

	public DbSet<RootFolder> RootFolders { get; set; }

	public DbSet<NamingConfig> NamingConfigs { get; set; }

	public DbSet<MediaManagementConfig> MediaManagementConfigs { get; set; }

	public DbSet<QualityProfile> QualityProfiles { get; set; }

	public DbSet<LanguageProfile> LanguageProfiles { get; set; }

	public DbSet<Series> Series { get; set; }

	public DbSet<Season> Seasons { get; set; }

	public DbSet<Episode> Episodes { get; set; }

	public DbSet<Movie> Movies { get; set; }

	public DbSet<MediaVersion> Versions { get; set; }

	public DbSet<EpisodeFile> EpisodeFiles { get; set; }

	public DbSet<MovieFile> MovieFiles { get; set; }

	public DbSet<DownloadClientConfig> DownloadClients { get; set; }

	public DbSet<ReleaseFilterConfig> ReleaseFilters { get; set; }

	public DbSet<CustomFormatConfig> CustomFormats { get; set; }

	public DbSet<TrackedDownload> TrackedDownloads { get; set; }

	public DbSet<HistoryEvent> History { get; set; }

	public DbSet<ImportList> ImportLists { get; set; }

	public DbSet<Connection> Connections { get; set; }

	/// <inheritdoc />
	public SubmarineDatabaseContext(DbContextOptions options, IConfiguration configuration) : base(options)
		=> Configuration = configuration;

	protected override void OnModelCreating(ModelBuilder builder)
	{
		builder.Entity<BittorrentTracker>();
		builder.Entity<UsenetIndexer>();
		builder.Entity<TorznabIndexer>();
		builder.Entity<NewznabIndexer>();

		builder.Entity<Tag>()
			.HasIndex(t => t.Label)
			.IsUnique();

		builder.Entity<RootFolder>()
			.HasIndex(r => r.Path)
			.IsUnique();

		builder.Entity<QualityProfile>()
			.OwnsMany(p => p.Items, items =>
			{
				items.ToJson();
				items.OwnsOne(i => i.Quality, q => q.Ignore(m => m.Name));
			});

		builder.Entity<QualityProfile>()
			.Property(p => p.FormatScores)
			.HasConversion(
				s => JsonSerializer.Serialize(s, (JsonSerializerOptions?)null),
				s => string.IsNullOrEmpty(s)
					? new Dictionary<int, int>()
					: JsonSerializer.Deserialize<Dictionary<int, int>>(s, (JsonSerializerOptions?)null)
					  ?? new Dictionary<int, int>(),
				new ValueComparer<Dictionary<int, int>>(
					(a, b) => a!.Count == b!.Count && a.All(pair => b.ContainsKey(pair.Key) && b[pair.Key] == pair.Value),
					d => d.Aggregate(0, (hash, pair) => hash ^ HashCode.Combine(pair.Key, pair.Value)),
					d => d.ToDictionary(pair => pair.Key, pair => pair.Value)));

		builder.Entity<CustomFormatConfig>()
			.Property(c => c.Conditions)
			.HasConversion(
				c => JsonSerializer.Serialize(c, (JsonSerializerOptions?)null),
				c => string.IsNullOrEmpty(c)
					? new List<CustomFormatCondition>()
					: JsonSerializer.Deserialize<List<CustomFormatCondition>>(c, (JsonSerializerOptions?)null)
					  ?? new List<CustomFormatCondition>(),
				new ValueComparer<List<CustomFormatCondition>>(
					(a, b) => a!.SequenceEqual(b!),
					c => c.Aggregate(0, (hash, condition) => HashCode.Combine(hash, condition.GetHashCode())),
					c => c.ToList()));

		builder.Entity<Series>()
			.HasIndex(s => s.TvdbId)
			.IsUnique();

		builder.Entity<Series>()
			.HasMany(s => s.Seasons)
			.WithOne()
			.HasForeignKey(s => s.SeriesId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Entity<Series>()
			.HasMany(s => s.Episodes)
			.WithOne()
			.HasForeignKey(e => e.SeriesId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Entity<Movie>()
			.HasIndex(m => m.TmdbId)
			.IsUnique();

		builder.Entity<MediaVersion>()
			.HasIndex(v => v.SeriesId);

		builder.Entity<MediaVersion>()
			.HasIndex(v => v.MovieId);

		builder.Entity<Series>()
			.HasMany(s => s.Versions)
			.WithOne()
			.HasForeignKey(v => v.SeriesId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Entity<Movie>()
			.HasMany(m => m.Versions)
			.WithOne()
			.HasForeignKey(v => v.MovieId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Entity<Episode>()
			.HasMany(e => e.Files)
			.WithMany(f => f.Episodes)
			.UsingEntity("EpisodeFileEpisodes");

		builder.Entity<EpisodeFile>()
			.HasOne<MediaVersion>()
			.WithMany()
			.HasForeignKey(f => f.MediaVersionId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Entity<Movie>()
			.HasMany(m => m.Files)
			.WithOne()
			.HasForeignKey(f => f.MovieId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Entity<MovieFile>()
			.HasOne<MediaVersion>()
			.WithMany()
			.HasForeignKey(f => f.MediaVersionId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Entity<EpisodeFile>()
			.Property(f => f.Quality)
			.HasConversion(
				q => JsonSerializer.Serialize(q, (JsonSerializerOptions?)null),
				q => JsonSerializer.Deserialize<QualityModel>(q, (JsonSerializerOptions?)null)!,
				QualityValueComparer);

		builder.Entity<MovieFile>()
			.Property(f => f.Quality)
			.HasConversion(
				q => JsonSerializer.Serialize(q, (JsonSerializerOptions?)null),
				q => JsonSerializer.Deserialize<QualityModel>(q, (JsonSerializerOptions?)null)!,
				QualityValueComparer);

		builder.Entity<TrackedDownload>()
			.Property(d => d.Quality)
			.HasConversion(
				q => JsonSerializer.Serialize(q, (JsonSerializerOptions?)null),
				q => JsonSerializer.Deserialize<QualityModel>(q, (JsonSerializerOptions?)null)!,
				QualityValueComparer);

		builder.Entity<TrackedDownload>()
			.HasOne<Series>()
			.WithMany()
			.HasForeignKey(d => d.SeriesId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.Entity<TrackedDownload>()
			.HasOne<Movie>()
			.WithMany()
			.HasForeignKey(d => d.MovieId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.Entity<TrackedDownload>()
			.HasOne<MediaVersion>()
			.WithMany()
			.HasForeignKey(d => d.MediaVersionId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.Entity<HistoryEvent>()
			.Property(h => h.Quality)
			.HasConversion(
				q => q == null ? null : JsonSerializer.Serialize(q, (JsonSerializerOptions?)null),
				q => string.IsNullOrEmpty(q)
					? null
					: JsonSerializer.Deserialize<QualityModel>(q, (JsonSerializerOptions?)null),
				QualityValueComparer);

		builder.Entity<HistoryEvent>()
			.Property(h => h.Data)
			.HasConversion(
				d => JsonSerializer.Serialize(d, (JsonSerializerOptions?)null),
				d => string.IsNullOrEmpty(d)
					? new Dictionary<string, string>()
					: JsonSerializer.Deserialize<Dictionary<string, string>>(d, (JsonSerializerOptions?)null)
					  ?? new Dictionary<string, string>(),
				new ValueComparer<Dictionary<string, string>>(
					(a, b) => a!.SequenceEqual(b!),
					d => d.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
					d => d.ToDictionary(pair => pair.Key, pair => pair.Value)));

		builder.Entity<HistoryEvent>()
			.HasOne<Series>()
			.WithMany()
			.HasForeignKey(h => h.SeriesId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.Entity<HistoryEvent>()
			.HasOne<Episode>()
			.WithMany()
			.HasForeignKey(h => h.EpisodeId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.Entity<HistoryEvent>()
			.HasOne<Movie>()
			.WithMany()
			.HasForeignKey(h => h.MovieId)
			.OnDelete(DeleteBehavior.SetNull);

		base.OnModelCreating(builder);
	}

	/// <inheritdoc />
	public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = new())
	{
		UpdateTimestamps();

		return base.SaveChangesAsync(cancellationToken);
	}

	/// <inheritdoc />
	public override int SaveChanges()
	{
		UpdateTimestamps();

		return base.SaveChanges();
	}

	private void UpdateTimestamps()
	{
		var changedEntries = ChangeTracker.Entries()
			.Where(e => e.State is EntityState.Added or EntityState.Modified)
			.ToList();

		var now = DateTimeOffset.UtcNow;

		foreach (var entry in changedEntries)
		{
			if (entry is { State: EntityState.Added, Entity: ICreatable creatable })
				creatable.CreatedAt = now;

			if (entry.Entity is IUpdatable updatable)
				updatable.UpdatedAt = now;
		}
	}
}
