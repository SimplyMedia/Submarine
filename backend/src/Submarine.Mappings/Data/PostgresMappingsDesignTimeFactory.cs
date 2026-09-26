using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Submarine.Mappings.Data;

/// <summary>
/// Design time factory so dotnet-ef can generate the PostgreSQL migrations.
/// </summary>
public sealed class PostgresMappingsDesignTimeFactory : IDesignTimeDbContextFactory<PostgresMappingsDbContext>
{
	public PostgresMappingsDbContext CreateDbContext(string[] args)
	{
		var options = new DbContextOptionsBuilder<PostgresMappingsDbContext>()
			.UseNpgsql("Host=localhost;Database=submarine_mappings_design;Username=postgres;Password=postgres")
			.Options;
		return new PostgresMappingsDbContext(options);
	}
}
