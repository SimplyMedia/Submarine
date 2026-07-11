using System.Text.Json.Serialization;
using AspNetCore.ExceptionHandler;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Submarine.Api.Models.Database;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.Languages;
using Submarine.Core.Parser;
using Submarine.Core.Parser.Release;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Release.Usenet;
using Submarine.Core.Validator;

var builder = WebApplication.CreateBuilder(args);

const string logTemplate =
	"[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}\n{Exception}";

builder.Host.UseSerilog((_, _, loggerConfiguration) =>
{
	var loggerConfig = new ConfigurationBuilder()
		.SetBasePath(Directory.GetCurrentDirectory())
		.AddJsonFile("appsettings.json")
		.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json",
			true)
		.Build();

	loggerConfiguration
		.ReadFrom.Configuration(loggerConfig)
		.Enrich.WithThreadId()
		.Enrich.WithThreadName()
		.Enrich.WithProperty("ThreadName", "Main")
		.Enrich.FromLogContext()
		.WriteTo.Console(
			outputTemplate: logTemplate,
			theme: SystemConsoleTheme.Colored)
		.WriteTo.Async(a => a.File("logs/log.txt", rollingInterval: RollingInterval.Day, outputTemplate: logTemplate));
});

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(opts =>
{
	opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Parser
builder.Services.AddSingleton<IParser<BaseRelease>, ReleaseParserService>();
builder.Services.AddSingleton<IParser<TorrentRelease>, TorrentReleaseParserService>();
builder.Services.AddSingleton<IParser<UsenetRelease>, UsenetReleaseParserService>();
builder.Services.AddSingleton<IParser<string?>, ReleaseGroupParserService>();
builder.Services.AddSingleton<IParser<IReadOnlyList<Language>>, LanguageParserService>();
builder.Services.AddSingleton<IParser<QualityModel>, QualityParserService>();
builder.Services.AddSingleton<IParser<StreamingProvider?>, StreamingProviderParserService>();

// Validator
builder.Services.AddSingleton<UsenetReleaseValidatorService>();

// Repository
builder.Services.AddScoped<IProviderRepository, ProviderRepository>();

// Service
builder.Services.AddScoped<ProviderService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(x => x
		.AllowAnyOrigin()
		.AllowAnyMethod()
		.AllowAnyOrigin()
		.AllowAnyHeader()
	);
});

builder.Services.UseExceptionBasedErrorHandling();

builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "Submarine.Api",
		Version = "v1",
		Description = "Submarine Api powering the frontend!",
		Contact = new OpenApiContact
		{
			Name = "DevYukine",
			Email = "devyukine@gmx.de"
		}
	});

	var apiFilePath = Path.Combine(AppContext.BaseDirectory, "Submarine.Api.xml");
	var coreFilePath = Path.Combine(AppContext.BaseDirectory, "Submarine.Core.xml");
	c.IncludeXmlComments(apiFilePath, true);
	c.IncludeXmlComments(coreFilePath);
});

builder.Services.AddHealthChecks();

builder.Services.AddDbContext<SubmarineDatabaseContext, PostgresDatabaseContext>();

var app = builder.Build();

using (var scope = app.Services.GetService<IServiceScopeFactory>()?.CreateScope())
{
	var logger = app.Services.GetRequiredService<ILogger<PostgresDatabaseContext>>();

	try
	{
		scope?.ServiceProvider.GetRequiredService<PostgresDatabaseContext>().Database.Migrate();
	}
	catch (Exception ex)
	{
		logger.LogError(ex, "An error occurred while running migrations on the database");
		throw;
	}
}

if (app.Environment.IsDevelopment())
{
	app.UseHttpsRedirection();

	app.UseDeveloperExceptionPage();
	app.UseSwagger();
	app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Submarine.Api v1"));
}

app.UseSerilogRequestLogging(opt => opt.GetLevel = (_, _, _) => LogEventLevel.Debug);

app.UseCors();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/_status/healthz");
app.MapHealthChecks("/_status/ready");

app.MapControllers();

app.Run();
