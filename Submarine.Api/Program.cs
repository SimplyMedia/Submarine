using System.Text.Json.Serialization;
using AspNetCore.ExceptionHandler;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Enrichers;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Submarine.Core.Languages;
using Submarine.Core.Parser;
using Submarine.Core.Parser.Release;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Release.Usenet;
using Submarine.Core.Validator;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, loggerConfiguration) =>
{
	loggerConfiguration
		.MinimumLevel.Is(context.HostingEnvironment.EnvironmentName == "Development"
			? LogEventLevel.Debug
			: LogEventLevel.Information)
		.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
		.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
		.Enrich.WithThreadId()
		.Enrich.WithThreadName()
		.Enrich.WithProperty(ThreadNameEnricher.ThreadNamePropertyName, "Main")
		.Enrich.FromLogContext()
		.WriteTo.Console(
			outputTemplate:
			"[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{ThreadName}] {Message:lj}\n{Exception}",
			theme: SystemConsoleTheme.Colored);
});

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(opts =>
{
	opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<IParser<BaseRelease>, ReleaseParserService>();
builder.Services.AddSingleton<IParser<TorrentRelease>, TorrentReleaseParserService>();
builder.Services.AddSingleton<IParser<UsenetRelease>, UsenetReleaseParserService>();
builder.Services.AddSingleton<IParser<string?>, ReleaseGroupParserService>();
builder.Services.AddSingleton<IParser<IReadOnlyList<Language>>, LanguageParserService>();
builder.Services.AddSingleton<IParser<QualityModel>, QualityParserService>();
builder.Services.AddSingleton<IParser<StreamingProvider?>, StreamingProviderParserService>();
builder.Services.AddSingleton<UsenetReleaseValidatorService>();
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

	var filePath = Path.Combine(AppContext.BaseDirectory, "Submarine.Api.xml");
	c.IncludeXmlComments(filePath);
});

builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseHttpsRedirection();

	app.UseDeveloperExceptionPage();
	app.UseSwagger();
	app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Submarine.Api v1"));
}

app.UseSerilogRequestLogging();

app.UseCors();

app.UseRouting();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
	endpoints.MapControllers();
	endpoints.MapHealthChecks("/_status/healthz");
	endpoints.MapHealthChecks("/_status/ready");
});

app.MapControllers();

app.Run();
