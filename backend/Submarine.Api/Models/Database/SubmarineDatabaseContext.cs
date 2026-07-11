using Microsoft.EntityFrameworkCore;
using Submarine.Core.Database;
using Submarine.Core.Provider;

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

	public DbSet<Provider> Providers { get; set; }

	/// <inheritdoc />
	public SubmarineDatabaseContext(DbContextOptions options, IConfiguration configuration) : base(options)
		=> Configuration = configuration;

	protected override void OnModelCreating(ModelBuilder builder)
	{
		builder.Entity<BittorrentTracker>();
		builder.Entity<UsenetIndexer>();

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
