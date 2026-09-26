using Microsoft.EntityFrameworkCore;
using Submarine.Mappings.Entities;

namespace Submarine.Mappings.Data;

/// <summary>
/// Shared mapping database model. The concrete provider context is selected at startup.
/// </summary>
public abstract class MappingsDbContext(DbContextOptions options) : DbContext(options)
{
	public DbSet<SceneMapping> SceneMappings => Set<SceneMapping>();

	public DbSet<SceneEpisodeMapping> SceneEpisodeMappings => Set<SceneEpisodeMapping>();

	public DbSet<AniListMapping> AniListMappings => Set<AniListMapping>();

	public DbSet<SceneNameEntry> SceneNames => Set<SceneNameEntry>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<SceneMapping>(entity =>
		{
			entity.ToTable("SceneMappings");
			entity.HasIndex(m => new { m.TvdbId, m.SeasonNumber }).IsUnique();
		});

		modelBuilder.Entity<SceneEpisodeMapping>(entity =>
		{
			entity.ToTable("SceneEpisodeMappings");
			entity.HasIndex(m => new { m.TvdbId, m.SeasonNumber, m.EpisodeNumber }).IsUnique();
		});

		modelBuilder.Entity<AniListMapping>(entity =>
		{
			entity.ToTable("AniListMappings");
			entity.HasIndex(m => m.AniListId).IsUnique();
			entity.HasIndex(m => new { m.TvdbId, m.TvdbSeason });
		});

		modelBuilder.Entity<SceneNameEntry>(entity =>
		{
			entity.ToTable("SceneNames");
			entity.HasIndex(m => new { m.TvdbId, m.SceneName }).IsUnique();
		});
	}
}
