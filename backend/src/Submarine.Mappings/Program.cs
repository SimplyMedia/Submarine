using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Submarine.Mappings.Data;
using Submarine.Mappings.Endpoints;
using Submarine.Mappings.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
	.ReadFrom.Configuration(context.Configuration)
	.ReadFrom.Services(services)
	.Enrich.FromLogContext()
	.WriteTo.Console());

var isPostgres = string.Equals(builder.Configuration["Database:Provider"], "Postgres", StringComparison.OrdinalIgnoreCase);
var sqliteConnectionString = builder.Configuration.GetConnectionString("SqliteConnection") ?? "Data Source=data/mappings.db";
var postgresConnectionString = builder.Configuration.GetConnectionString("PostgresConnection")
	?? "Host=localhost;Database=submarine_mappings;Username=postgres;Password=postgres";

if (isPostgres)
{
	builder.Services.AddDbContext<MappingsDbContext, PostgresMappingsDbContext>(options =>
		options.UseNpgsql(postgresConnectionString));
}
else
{
	builder.Services.AddDbContext<MappingsDbContext, SqliteMappingsDbContext>(options =>
		options.UseSqlite(sqliteConnectionString).AddInterceptors(new SqlitePragmasConnectionInterceptor()));
}

builder.Services.AddScoped<MappingResolver>();
builder.Services.AddScoped<AniListMappingService>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	var adminApiKey = builder.Configuration["Auth:AdminApiKey"];
	options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
	{
		if (!string.IsNullOrEmpty(adminApiKey)
			&& httpContext.Request.Headers.TryGetValue("X-Api-Key", out var provided)
			&& provided.Count == 1
			&& CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided[0] ?? string.Empty), Encoding.UTF8.GetBytes(adminApiKey)))
		{
			return RateLimitPartition.GetNoLimiter("authenticated");
		}

		return RateLimitPartition.GetFixedWindowLimiter(
			(httpContext.Connection.RemoteIpAddress ?? IPAddress.Loopback).ToString(),
			_ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = 600,
				Window = TimeSpan.FromMinutes(1),
			});
	});
});

var swaggerEnabled = builder.Configuration.GetValue("Swagger:Enabled", builder.Environment.IsDevelopment());
if (swaggerEnabled)
{
	builder.Services.AddOpenApi();
}

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseRateLimiter();

if (swaggerEnabled)
{
	app.MapOpenApi();
	app.MapScalarApiReference();
}

app.MapSceneEndpoints();
app.MapAniListEndpoints();
app.MapDatasetEndpoints();

app.MapGet("/_status/healthz", () => Results.Ok(new { status = "Healthy" }));

using (var scope = app.Services.CreateScope())
{
	if (!isPostgres)
	{
		EnsureSqliteDirectory(sqliteConnectionString);
	}

	var db = scope.ServiceProvider.GetRequiredService<MappingsDbContext>();
	db.Database.Migrate();
}

app.Run();

static void EnsureSqliteDirectory(string connectionString)
{
	var dataSource = connectionString
		.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
		.Select(pair => pair.Split('=', 2))
		.FirstOrDefault(parts => parts.Length == 2 && parts[0].Equals("Data Source", StringComparison.OrdinalIgnoreCase)) is { } parts
			? parts[1]
			: null;
	if (string.IsNullOrEmpty(dataSource))
	{
		return;
	}

	var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
	if (!string.IsNullOrEmpty(directory))
	{
		Directory.CreateDirectory(directory);
	}
}

// Exposed for WebApplicationFactory in the test suite.
public partial class Program;
