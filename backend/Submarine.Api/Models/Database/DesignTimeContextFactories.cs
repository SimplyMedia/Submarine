using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Submarine.Api.Models.Database;

public class SqliteDatabaseContextFactory : IDesignTimeDbContextFactory<SqliteDatabaseContext>
{
	public SqliteDatabaseContext CreateDbContext(string[] args)
	{
		var configuration = new ConfigurationBuilder()
			.SetBasePath(Directory.GetCurrentDirectory())
			.AddJsonFile("appsettings.json", true)
			.Build();

		var options = new DbContextOptionsBuilder<SqliteDatabaseContext>().Options;

		return new SqliteDatabaseContext(options, configuration);
	}
}

public class PostgresDatabaseContextFactory : IDesignTimeDbContextFactory<PostgresDatabaseContext>
{
	public PostgresDatabaseContext CreateDbContext(string[] args)
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:PostgreSQLConnection"] =
					"Host=localhost;Port=5432;Database=Submarine;Username=postgres;Password=dev;"
			})
			.Build();

		var options = new DbContextOptionsBuilder<PostgresDatabaseContext>().Options;

		return new PostgresDatabaseContext(options, configuration);
	}
}
