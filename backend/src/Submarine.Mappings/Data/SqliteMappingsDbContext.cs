using Microsoft.EntityFrameworkCore;

namespace Submarine.Mappings.Data;

/// <summary>
/// SQLite backed mapping database. Its migrations live under Migrations/Sqlite.
/// </summary>
public sealed class SqliteMappingsDbContext(DbContextOptions<SqliteMappingsDbContext> options)
	: MappingsDbContext(options);
