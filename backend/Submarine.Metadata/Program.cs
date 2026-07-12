using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Submarine.Metadata.Clients;

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

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<TmdbClient>(client =>
{
	client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
});

builder.Services.AddHttpClient<TvdbClient>(client =>
{
	client.BaseAddress = new Uri("https://api4.thetvdb.com/v4/");
});

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
		Title = "Submarine.Metadata",
		Version = "v1",
		Description = "Submarine Metadata proxy powering the frontend!",
		Contact = new OpenApiContact
		{
			Name = "DevYukine",
			Email = "devyukine@gmx.de"
		}
	});

	var apiFilePath = Path.Combine(AppContext.BaseDirectory, "Submarine.Metadata.xml");
	var contractsFilePath = Path.Combine(AppContext.BaseDirectory, "Submarine.Metadata.Contracts.xml");
	c.IncludeXmlComments(apiFilePath, true);
	c.IncludeXmlComments(contractsFilePath);

	c.CustomOperationIds(apiDesc => $"{apiDesc.ActionDescriptor.RouteValues["controller"]}_{apiDesc.ActionDescriptor.RouteValues["action"]}");
});

builder.Services.AddHealthChecks();

var app = builder.Build();

var swaggerEnabled = builder.Configuration.GetValue<bool?>("Swagger:Enabled") ?? app.Environment.IsDevelopment();

if (app.Environment.IsDevelopment())
{
	app.UseHttpsRedirection();

	app.UseDeveloperExceptionPage();
}

if (swaggerEnabled)
{
	app.UseSwagger();
	app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Submarine.Metadata v1"));
}

app.UseSerilogRequestLogging(opt => opt.GetLevel = (_, _, _) => LogEventLevel.Debug);

app.UseCors();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/_status/healthz");

app.Run();
