using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Submarine.Mappings.Data;

/// <summary>
/// Design time factory so dotnet-ef can generate the SQLite migrations.
/// </summary>
public sealed class SqliteMappingsDesignTimeFactory : IDesignTimeDbContextFactory<SqliteMappingsDbContext>
{
	public SqliteMappingsDbContext CreateDbContext(string[] args)
	{
		var options = new DbContextOptionsBuilder<SqliteMappingsDbContext>()
			.UseSqlite("Data Source=data/mappings.db")
			.Options;
		return new SqliteMappingsDbContext(options);
	}
}
