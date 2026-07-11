using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Submarine.Mappings.Models;

namespace Submarine.Mappings.Database;

/// <summary>
///     Mappings Database Context
/// </summary>
public class MappingsDatabaseContext : DbContext
{
	private readonly IConfiguration _configuration;

	public DbSet<SceneMapping> SceneMappings { get; set; }

	public DbSet<SceneEpisodeMapping> SceneEpisodeMappings { get; set; }

	public DbSet<AniListMapping> AniListMappings { get; set; }

	/// <summary>
	///     Creates a new instance of <see cref="MappingsDatabaseContext" />
	/// </summary>
	/// <param name="options">database context options</param>
	/// <param name="configuration">configuration to read the connection string from</param>
	public MappingsDatabaseContext(DbContextOptions options, IConfiguration configuration) : base(options)
		=> _configuration = configuration;

	/// <inheritdoc />
	protected override void OnConfiguring(DbContextOptionsBuilder options)
	{
		var connectionString =
			_configuration.GetConnectionString("SqliteConnection") ?? "Data Source=data/mappings.db";

		var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
		var directory = Path.GetDirectoryName(dataSource);

		if (!string.IsNullOrEmpty(directory))
			Directory.CreateDirectory(directory);

		options.UseSqlite(connectionString);
	}

	/// <inheritdoc />
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<AniListMapping>(entity =>
		{
			entity.HasIndex(m => m.AniListId).IsUnique();
			entity.HasIndex(m => m.TvdbId);
		});
	}
}
