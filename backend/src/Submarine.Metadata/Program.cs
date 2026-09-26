using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;
using Submarine.Metadata;
using Submarine.Metadata.Endpoints;
using Submarine.Metadata.Options;
using Submarine.Metadata.Upstream;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, configuration) => configuration
	.ReadFrom.Configuration(context.Configuration)
	.Enrich.FromLogContext()
	.WriteTo.Console());

builder.Services.Configure<TmdbOptions>(builder.Configuration.GetSection(TmdbOptions.SectionName));
builder.Services.Configure<TvdbOptions>(builder.Configuration.GetSection(TvdbOptions.SectionName));
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection(CacheOptions.SectionName));
builder.Services.AddHybridCache();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<TmdbClient>();
builder.Services.AddSingleton<TvdbClient>();
builder.Services.AddSingleton<MetadataService>();

builder.Services.AddHttpClient(TmdbClient.ClientName, (provider, client) =>
{
	var options = provider.GetRequiredService<IOptions<TmdbOptions>>().Value;
	client.BaseAddress = new Uri(options.BaseUrl);
	if (!string.IsNullOrEmpty(options.AccessToken))
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
});
builder.Services.AddHttpClient(TvdbClient.ClientName, (provider, client) =>
{
	var options = provider.GetRequiredService<IOptions<TvdbOptions>>().Value;
	client.BaseAddress = new Uri(options.BaseUrl);
});

builder.Services.Configure<JsonOptions>(options =>
	options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var apiKey = builder.Configuration["Auth:ApiKey"];
builder.Services.AddRateLimiter(limiterOptions =>
{
	limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	limiterOptions.AddPolicy("api", context =>
	{
		if (!string.IsNullOrEmpty(apiKey)
			&& context.Request.Headers.TryGetValue("X-Api-Key", out var provided)
			&& provided.Count == 1
			&& CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided[0] ?? string.Empty), Encoding.UTF8.GetBytes(apiKey)))
		{
			return RateLimitPartition.GetNoLimiter("authenticated");
		}

		return RateLimitPartition.GetFixedWindowLimiter(
			context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
			_ => new FixedWindowRateLimiterOptions { PermitLimit = 600, Window = TimeSpan.FromMinutes(1) });
	});
});

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();

app.Use(async (context, next) =>
{
	if (!string.IsNullOrEmpty(apiKey)
		&& context.Request.Path.StartsWithSegments("/api")
		&& (!context.Request.Headers.TryGetValue("X-Api-Key", out var provided)
			|| provided.Count != 1
			|| !string.Equals(provided[0], apiKey, StringComparison.Ordinal)))
	{
		context.Response.StatusCode = StatusCodes.Status401Unauthorized;
		await context.Response.WriteAsJsonAsync(
			new Microsoft.AspNetCore.Mvc.ProblemDetails
			{
				Status = StatusCodes.Status401Unauthorized,
				Title = "Unauthorized"
			},
			context.RequestAborted);
		return;
	}

	await next(context);
});

app.UseRateLimiter();
app.MapMetadataEndpoints();
app.MapGet("/_status/healthz", () => Results.Json(new { status = "ok" }));
app.MapGet("/_status/ready", (IOptions<TmdbOptions> tmdbOptions, IOptions<TvdbOptions> tvdbOptions) =>
{
	var tmdb = tmdbOptions.Value;
	var tvdb = tvdbOptions.Value;
	return Results.Json(new
	{
		tmdb = !string.IsNullOrEmpty(tmdb.AccessToken) || !string.IsNullOrEmpty(tmdb.ApiKey),
		tvdb = !string.IsNullOrEmpty(tvdb.ApiKey)
	});
});

if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
	app.MapOpenApi();
	app.MapScalarApiReference();
}

app.Run();

public partial class Program;
