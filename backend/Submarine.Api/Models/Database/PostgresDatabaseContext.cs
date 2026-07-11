using Microsoft.EntityFrameworkCore;

namespace Submarine.Api.Models.Database;

public class PostgresDatabaseContext : SubmarineDatabaseContext
{
	private readonly IConfiguration _configuration;

	public PostgresDatabaseContext(DbContextOptions options, IConfiguration configuration) : base(options,
		configuration)
		=> _configuration = configuration;

	protected override void OnConfiguring(DbContextOptionsBuilder options)
	{
		options.UseNpgsql(_configuration.GetConnectionString("PostgreSQLConnection"));
	}
}
