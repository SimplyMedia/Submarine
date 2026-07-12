using System.Text.Json.Serialization;
using AspNetCore.ExceptionHandler;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Submarine.Api.Clients;
using Submarine.Api.Events;
using Submarine.Api.Jobs;
using Submarine.Api.Middleware;
using Submarine.Api.Models.Database;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.DecisionEngine;
using Submarine.Core.DecisionEngine.CustomFormats;
using Submarine.Core.DecisionEngine.Filter;
using Submarine.Core.Indexer;
using Submarine.Core.Indexer.Torznab;
using Submarine.Core.Languages;
using Submarine.Core.MediaFile.Naming;
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
builder.Services.AddSingleton<QualityOverrideStore>();
builder.Services.AddSingleton<IQualityOverrideSource>(sp => sp.GetRequiredService<QualityOverrideStore>());
builder.Services.AddSingleton<IParser<BaseRelease>, ReleaseParserService>();
builder.Services.AddSingleton<IParser<TorrentRelease>, TorrentReleaseParserService>();
builder.Services.AddSingleton<IParser<UsenetRelease>, UsenetReleaseParserService>();
builder.Services.AddSingleton<IParser<string?>, ReleaseGroupParserService>();
builder.Services.AddSingleton<IParser<IReadOnlyList<Language>>, LanguageParserService>();
builder.Services.AddSingleton<IParser<QualityModel>, QualityParserService>();
builder.Services.AddSingleton<IParser<StreamingProvider?>, StreamingProviderParserService>();
builder.Services.AddSingleton<IParser<TorznabCapabilities>, TorznabCapabilitiesParser>();
builder.Services.AddSingleton<IParser<IReadOnlyList<ReleaseInfo>>, TorznabFeedParser>();

// Validator
builder.Services.AddSingleton<UsenetReleaseValidatorService>();

// Decision Engine
builder.Services.AddSingleton<FilterEvaluator>();
builder.Services.AddSingleton<CustomFormatEvaluator>();
builder.Services.AddSingleton<DownloadDecisionService>();

// Repository
builder.Services.AddScoped<IProviderRepository, ProviderRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IRootFolderRepository, RootFolderRepository>();
builder.Services.AddScoped<IQualityProfileRepository, QualityProfileRepository>();
builder.Services.AddScoped<ILanguageProfileRepository, LanguageProfileRepository>();
builder.Services.AddScoped<ISeriesRepository, SeriesRepository>();
builder.Services.AddScoped<IMovieRepository, MovieRepository>();
builder.Services.AddScoped<IDownloadClientRepository, DownloadClientRepository>();
builder.Services.AddScoped<IReleaseFilterRepository, ReleaseFilterRepository>();
builder.Services.AddScoped<ICustomFormatRepository, CustomFormatRepository>();
builder.Services.AddScoped<IImportListRepository, ImportListRepository>();
builder.Services.AddScoped<IConnectionRepository, ConnectionRepository>();
builder.Services.AddScoped<IBlocklistRepository, BlocklistRepository>();
builder.Services.AddScoped<IQualityOverrideRepository, QualityOverrideRepository>();

// Service
builder.Services.AddScoped<ProviderService>();
builder.Services.AddScoped<TagService>();
builder.Services.AddScoped<RootFolderService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<SeriesService>();
builder.Services.AddScoped<MovieService>();
builder.Services.AddScoped<VersionService>();
builder.Services.AddScoped<CalendarService>();
builder.Services.AddScoped<IndexerService>();
builder.Services.AddScoped<DownloadClientService>();
builder.Services.AddSingleton<DownloadClientFactory>();
builder.Services.AddScoped<DecisionConfigService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<HistoryService>();
builder.Services.AddScoped<GrabService>();
builder.Services.AddScoped<QueueService>();
builder.Services.AddScoped<SeriesRefreshService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<RenameService>();
builder.Services.AddScoped<ImportListService>();
builder.Services.AddScoped<ConnectionService>();
builder.Services.AddScoped<BlocklistService>();
builder.Services.AddScoped<QualityOverrideService>();
builder.Services.AddSingleton<SecurityConfigStore>();

builder.Services.AddSingleton<NamingTemplateRenderer>();
builder.Services.AddSingleton<MediaNamingService>();

// Clients
builder.Services.AddHttpClient<IMetadataClient, MetadataClient>((sp, client) =>
	{
		var baseUrl = sp.GetRequiredService<IConfiguration>().GetValue<string>("Metadata:BaseUrl")
		              ?? "http://localhost:5100";

		client.BaseAddress = new Uri(baseUrl);
	})
	.AddStandardResilienceHandler();

builder.Services.AddHttpClient<IMappingsClient, MappingsClient>((sp, client) =>
	{
		var baseUrl = sp.GetRequiredService<IConfiguration>().GetValue<string>("Mappings:BaseUrl")
		              ?? "http://localhost:5200";

		client.BaseAddress = new Uri(baseUrl);
	})
	.AddStandardResilienceHandler();

builder.Services.AddHttpClient("indexer", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("downloadclient", client => client.Timeout = TimeSpan.FromSeconds(100));

builder.Services.AddSingleton<TorznabHttpClient>();
builder.Services.AddSingleton<ITorznabSearchClient>(sp => sp.GetRequiredService<TorznabHttpClient>());

builder.Services.AddHttpClient<IImportListFetcher, ImportListFetcher>(client =>
	client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("mediaserver", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<IMediaServerClientFactory, MediaServerClientFactory>();

// Background jobs
builder.Services.AddSingleton<IBackgroundTaskQueue, ChannelBackgroundTaskQueue>();
builder.Services.AddHostedService<QueuedHostedService>();

builder.Services.AddSingleton<ScheduledJobRegistry>();
builder.Services.AddSingleton<IScheduledJobRegistry>(sp => sp.GetRequiredService<ScheduledJobRegistry>());
builder.Services.AddSingleton<ScheduledJobRunner>();
builder.Services.AddHostedService<SchedulerHostedService>();

builder.Services.AddSingleton<IScheduledJob, DownloadMonitorJob>();
builder.Services.AddSingleton<IScheduledJob, MetadataRefreshJob>();
builder.Services.AddSingleton<IScheduledJob, ImportListSyncJob>();

// Events
builder.Services.AddScoped<IEventPublisher, EventPublisher>();
builder.Services.AddScoped<EpisodeTitleChangedHandler>();
builder.Services.AddScoped<IEventHandler<EpisodeTitleChangedEvent>>(sp =>
	sp.GetRequiredService<EpisodeTitleChangedHandler>());
builder.Services.AddScoped<ConnectionEventHandler>();
builder.Services.AddScoped<IEventHandler<MediaGrabbedEvent>>(sp =>
	sp.GetRequiredService<ConnectionEventHandler>());
builder.Services.AddScoped<IEventHandler<MediaImportedEvent>>(sp =>
	sp.GetRequiredService<ConnectionEventHandler>());
builder.Services.AddScoped<IEventHandler<MediaRenamedEvent>>(sp =>
	sp.GetRequiredService<ConnectionEventHandler>());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(x => x
		.AllowAnyOrigin()
		.AllowAnyMethod()
		.AllowAnyHeader()
	);
});

builder.Services.UseExceptionBasedErrorHandling();
builder.Services.AddProblemDetails();

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

	c.CustomOperationIds(apiDesc => $"{apiDesc.ActionDescriptor.RouteValues["controller"]}_{apiDesc.ActionDescriptor.RouteValues["action"]}");

	c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
	{
		Type = SecuritySchemeType.ApiKey,
		In = ParameterLocation.Header,
		Name = "X-Api-Key",
		Description = "API key required when API key authentication is enabled"
	});

	c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
	{
		[new OpenApiSecuritySchemeReference("ApiKey", document)] = new List<string>()
	});

	// Default schemaId selector plus a "Metadata" prefix for Submarine.Metadata.Contracts types,
	// which otherwise collide with same-named Submarine.Core types (e.g. SeriesStatus).
	c.CustomSchemaIds(SchemaId);

	static string SchemaId(Type type)
	{
		if (type.IsConstructedGenericType)
			return string.Concat(type.GetGenericArguments().Select(SchemaId)) + type.Name.Split('`')[0];

		var name = type.Name.Replace("[]", "Array");

		return type.Namespace == "Submarine.Metadata.Contracts" ? $"Metadata{name}" : name;
	}
});

builder.Services.AddHealthChecks();

var databaseProvider = builder.Configuration.GetValue<string>("Database:Provider") ?? "Sqlite";

if (databaseProvider == "Postgres")
	builder.Services.AddDbContext<SubmarineDatabaseContext, PostgresDatabaseContext>();
else
	builder.Services.AddDbContext<SubmarineDatabaseContext, SqliteDatabaseContext>();

var app = builder.Build();

using (var scope = app.Services.GetService<IServiceScopeFactory>()?.CreateScope())
{
	var logger = app.Services.GetRequiredService<ILogger<SubmarineDatabaseContext>>();

	try
	{
		scope?.ServiceProvider.GetRequiredService<SubmarineDatabaseContext>().Database.Migrate();

		await (scope?.ServiceProvider.GetRequiredService<ProfileService>().SeedDefaultsAsync() ?? Task.CompletedTask);

		if (scope != null)
		{
			var overrides = await scope.ServiceProvider.GetRequiredService<SubmarineDatabaseContext>()
				.ReleaseGroupQualityOverrides.AsNoTracking().ToListAsync();

			app.Services.GetRequiredService<QualityOverrideStore>().Reload(overrides);
		}
	}
	catch (Exception ex)
	{
		logger.LogError(ex, "An error occurred while running migrations on the database");
		throw;
	}
}

var swaggerEnabled = builder.Configuration.GetValue<bool?>("Swagger:Enabled") ?? app.Environment.IsDevelopment();

if (app.Environment.IsDevelopment())
{
	app.UseHttpsRedirection();

	app.UseDeveloperExceptionPage();
}
else
{
	app.UseExceptionHandler();
}

if (swaggerEnabled)
{
	app.UseSwagger();
	app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Submarine.Api v1"));
}

app.UseStatusCodePages();

app.UseSerilogRequestLogging(opt => opt.GetLevel = (_, _, _) => LogEventLevel.Debug);

app.UseCors();

app.UseRouting();

app.UseMiddleware<ApiKeyMiddleware>();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/_status/healthz");
app.MapHealthChecks("/_status/ready");


app.Run();
