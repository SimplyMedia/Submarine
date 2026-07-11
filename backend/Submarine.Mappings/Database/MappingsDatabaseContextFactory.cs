using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Submarine.Mappings.Database;

public class MappingsDatabaseContextFactory : IDesignTimeDbContextFactory<MappingsDatabaseContext>
{
	public MappingsDatabaseContext CreateDbContext(string[] args)
	{
		var configuration = new ConfigurationBuilder()
			.SetBasePath(Directory.GetCurrentDirectory())
			.AddJsonFile("appsettings.json", true)
			.Build();

		var options = new DbContextOptionsBuilder<MappingsDatabaseContext>().Options;

		return new MappingsDatabaseContext(options, configuration);
	}
}
