using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Submarine.Api.Models.Database;

public class SqliteDatabaseContext : SubmarineDatabaseContext
{
	private readonly IConfiguration _configuration;

	public SqliteDatabaseContext(DbContextOptions options, IConfiguration configuration) : base(options,
		configuration)
		=> _configuration = configuration;

	protected override void OnConfiguring(DbContextOptionsBuilder options)
	{
		var connectionString = _configuration.GetConnectionString("SqliteConnection") ?? "Data Source=data/submarine.db";

		var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
		var directory = Path.GetDirectoryName(dataSource);

		if (!string.IsNullOrEmpty(directory))
			Directory.CreateDirectory(directory);

		options.UseSqlite(connectionString);
	}
}
