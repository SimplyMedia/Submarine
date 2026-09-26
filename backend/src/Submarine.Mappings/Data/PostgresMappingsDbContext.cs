using Microsoft.EntityFrameworkCore;

namespace Submarine.Mappings.Data;

/// <summary>
/// PostgreSQL backed mapping database. Its migrations live under Migrations/Postgres.
/// </summary>
public sealed class PostgresMappingsDbContext(DbContextOptions<PostgresMappingsDbContext> options)
	: MappingsDbContext(options);
