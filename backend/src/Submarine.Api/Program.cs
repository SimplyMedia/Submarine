using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.PeriodicBatching;
using Submarine.Api.Auth;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Api.Features.Commands;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Auth;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Events;
using Submarine.Infrastructure.Logging;
using Microsoft.OpenApi;
using Submarine.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
var contentRoot = builder.Environment.ContentRootPath;
// Directory holding the Sqlite database (or its default location even under Postgres), used for
// log files and DataProtection keys so they survive container restarts under a mounted volume.
var dataDirectory = Path.GetDirectoryName(
	new SqliteConnectionStringBuilder(
		SubmarineDatabase.ResolveSqlitePath(SubmarineDatabase.SqliteConnectionString(builder.Configuration), contentRoot)).DataSource)
	is { Length: > 0 } resolvedDataDirectory
		? resolvedDataDirectory
		: contentRoot;

// A backup restore stages a validated database at submarine.db.restore instead of overwriting the
// live file directly (open EF/log-sink connections could corrupt it); swap it in here, before
// anything (including migrations) opens the database.
SwapStagedSqliteRestore(dataDirectory);

builder.Host.UseSerilog((context, services, configuration) =>
{
	configuration.ReadFrom.Configuration(context.Configuration);
	if (SubmarineDesignTime.IsDocumentGeneration())
	{
		// No sinks: the document generator fails the build on any stderr output.
		return;
	}

	configuration
		.MinimumLevel.ControlledBy(services.GetRequiredService<LoggingLevelSwitch>())
		.Enrich.FromLogContext()
		.WriteTo.Console()
		.WriteTo.File(
			Path.Combine(dataDirectory, "logs", "submarine.txt"),
			rollingInterval: RollingInterval.Day,
			retainedFileCountLimit: 30)
		.WriteTo.Sink(new PeriodicBatchingSink(
			new LogDbSink(() => LogDbContextFactory.Create(context.Configuration, contentRoot)),
			new PeriodicBatchingSinkOptions
			{
				BatchSizeLimit = 100,
				Period = TimeSpan.FromSeconds(5),
				EagerlyEmitFirstEvent = true
			}));
});

// The document generator starts the server to enumerate endpoints; keep it off the real port.
if (SubmarineDesignTime.IsDocumentGeneration())
{
	builder.WebHost.UseUrls("http://127.0.0.1:0");
}

builder.Services.AddSubmarinePersistence(builder.Configuration, contentRoot);
builder.Services.AddSubmarineEvents(typeof(Program).Assembly, typeof(SubmarineDbContext).Assembly);
builder.Services.AddSubmarineCommands(typeof(Program).Assembly, typeof(SubmarineDbContext).Assembly);
builder.Services.AddSubmarineAuth();
builder.Services.AddSingleton(new DataDirectory(dataDirectory));
builder.Services.AddSingleton<IndexHtmlCache>();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "dataprotection-keys")));

builder.Services.AddSignalR().AddJsonProtocol(options =>
	options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services
	.AddAuthentication(AuthenticationSetup.Scheme)
	.AddPolicyScheme(AuthenticationSetup.Scheme, AuthenticationSetup.DisplayName, AuthenticationSetup.ConfigurePolicyScheme)
	.AddCookie(AuthenticationSetup.CookieScheme, AuthenticationSetup.ConfigureCookie)
	.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(AuthenticationSetup.ApiKeyScheme, _ => { })
	.AddScheme<AuthenticationSchemeOptions, AnonymousAuthenticationHandler>(AuthenticationSetup.AnonymousScheme, _ => { });
builder.Services.AddAuthorization(options =>
	options.FallbackPolicy = AuthenticationSetup.FallbackPolicy);

builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
		(context.Connection.RemoteIpAddress ?? IPAddress.Loopback).ToString(),
		_ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(5) }));
	options.OnRejected = async (context, cancellationToken) =>
	{
		context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
		var problemDetailsService = context.HttpContext.RequestServices.GetService<IProblemDetailsService>();
		if (problemDetailsService is not null)
		{
			await problemDetailsService.WriteAsync(new ProblemDetailsContext
			{
				HttpContext = context.HttpContext,
				ProblemDetails = new ProblemDetails
				{
					Status = StatusCodes.Status429TooManyRequests,
					Title = "Too many requests, try again later"
				}
			});
		}
	};
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SubmarineExceptionHandler>();
builder.Services.AddOpenApi("openapi", options =>
	options.AddSchemaTransformer((schema, context, _) =>
	{
		// Numbers may be read from strings at runtime, but the client types should stay numeric.
		if (schema.Type is { } type
			&& type.HasFlag(JsonSchemaType.String)
			&& (type.HasFlag(JsonSchemaType.Integer) || type.HasFlag(JsonSchemaType.Number)))
		{
			schema.Type = type & ~JsonSchemaType.String;
			schema.Pattern = null;
		}

		// Command-specific properties ride along via [JsonExtensionData]; the schema only
		// declares Name, so the client types must allow the rest through.
		if (context.JsonTypeInfo.Type == typeof(EnqueueCommandRequest))
		{
			schema.AdditionalPropertiesAllowed = true;
		}

		return Task.CompletedTask;
	}));
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
	options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddModules(
	builder.Configuration,
	typeof(Program).Assembly,
	typeof(SubmarineDbContext).Assembly);

var app = builder.Build();

// UrlBase lives in the database and can change at runtime, so the path base is applied per request.
app.Use(async (context, next) =>
{
	var snapshot = await context.RequestServices.GetRequiredService<IAuthConfigProvider>().GetSnapshotAsync(context.RequestAborted);
	if (!string.IsNullOrEmpty(snapshot.UrlBase)
		&& snapshot.UrlBase.StartsWith('/')
		&& context.Request.Path.StartsWithSegments(snapshot.UrlBase, out var remaining))
	{
		context.Request.PathBase = context.Request.PathBase.Add(snapshot.UrlBase);
		context.Request.Path = remaining;
	}

	await next(context);
});

// Endpoint matching must happen after the PathBase rewrite above, otherwise routes are matched
// against the un-stripped path (ASP.NET Core would otherwise insert routing implicitly before
// any earlier middleware, including this one).
app.UseRouting();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseRateLimiter();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapModules(typeof(Program).Assembly, typeof(SubmarineDbContext).Assembly);
app.MapOpenApi("openapi/{documentName}.json").AllowAnonymous();
app.MapScalarApiReference().AllowAnonymous();
// A single catch-all: MapFallback with a route-constrained pattern never matches an empty
// segment (the root "/"), which would otherwise fall through to the global FallbackPolicy and
// 401 instead of serving the SPA (and, for excluded prefixes, instead of a clean 404). Matching
// everything here and deciding inside the handler keeps every one of these paths on this single
// anonymous endpoint.
app.MapFallback(async (HttpContext context, IndexHtmlCache indexHtml, IAuthConfigProvider authConfigProvider) =>
{
	if (context.Request.Path.StartsWithSegments("/api")
		|| context.Request.Path.StartsWithSegments("/hubs")
		|| context.Request.Path.StartsWithSegments("/scalar")
		|| context.Request.Path.StartsWithSegments("/openapi"))
	{
		return Results.NotFound();
	}

	var snapshot = await authConfigProvider.GetSnapshotAsync(context.RequestAborted);
	var html = indexHtml.GetHtml(snapshot.UrlBase);
	if (html is null)
	{
		return Results.NotFound();
	}

	context.Response.Headers.CacheControl = "no-store";
	return Results.Text(html, "text/html", Encoding.UTF8);
}).AllowAnonymous();

// Applies the persisted log level once the host (including the migration hosted service) has
// finished starting, so startup logging uses the configured level rather than the Serilog default.
app.Lifetime.ApplicationStarted.Register(() =>
{
	_ = Task.Run(async () =>
	{
		try
		{
			await using var scope = app.Services.CreateAsyncScope();
			await scope.ServiceProvider.GetRequiredService<IAuthConfigProvider>().GetSnapshotAsync();
		}
		catch (Exception ex)
		{
			Serilog.Log.Warning(ex, "Failed to apply the configured log level at startup");
		}
	});
});

try
{
	app.Run();
}
finally
{
	await Serilog.Log.CloseAndFlushAsync();
}

static void SwapStagedSqliteRestore(string dataDirectory)
{
	var staged = Path.Combine(dataDirectory, "submarine.db.restore");
	if (!File.Exists(staged))
	{
		return;
	}

	var live = Path.Combine(dataDirectory, "submarine.db");
	foreach (var stale in new[] { live + "-wal", live + "-shm" })
	{
		if (File.Exists(stale))
		{
			File.Delete(stale);
		}
	}

	File.Move(staged, live, overwrite: true);
}

public partial class Program;
