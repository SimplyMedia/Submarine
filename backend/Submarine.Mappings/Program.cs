using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Submarine.Mappings.Database;
using Submarine.Mappings.Services;

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

builder.Services.AddScoped<MappingResolver>();

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

builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "Submarine.Mappings",
		Version = "v1",
		Description = "Submarine Scene Mappings service powering the frontend!",
		Contact = new OpenApiContact
		{
			Name = "DevYukine",
			Email = "devyukine@gmx.de"
		}
	});

	var apiFilePath = Path.Combine(AppContext.BaseDirectory, "Submarine.Mappings.xml");
	var contractsFilePath = Path.Combine(AppContext.BaseDirectory, "Submarine.Mappings.Contracts.xml");
	c.IncludeXmlComments(apiFilePath, true);
	c.IncludeXmlComments(contractsFilePath);
});

builder.Services.AddHealthChecks();

builder.Services.AddDbContext<MappingsDatabaseContext>();

var app = builder.Build();

using (var scope = app.Services.GetService<IServiceScopeFactory>()?.CreateScope())
{
	var logger = app.Services.GetRequiredService<ILogger<MappingsDatabaseContext>>();

	try
	{
		scope?.ServiceProvider.GetRequiredService<MappingsDatabaseContext>().Database.Migrate();
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
	app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Submarine.Mappings v1"));
}

app.UseSerilogRequestLogging(opt => opt.GetLevel = (_, _, _) => LogEventLevel.Debug);

app.UseCors();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/_status/healthz");

app.Run();
