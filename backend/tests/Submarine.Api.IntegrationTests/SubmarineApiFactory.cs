using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.IntegrationTests;

/// <summary>
///     Hosts the real API against a temporary Sqlite database, or Postgres when configured.
/// </summary>
public class SubmarineApiFactory : WebApplicationFactory<Program>
{
	// One directory per factory: logs.db lives next to the main database, so sharing a directory
	// between parallel hosts makes them fight over the same log file.
	private readonly string _directory = Path.Combine(Path.GetTempPath(), "submarine-it", Guid.NewGuid().ToString("N"));
	private readonly string _dbPath;

	public SubmarineApiFactory()
	{
		Directory.CreateDirectory(_directory);
		_dbPath = Path.Combine(_directory, "submarine.db");
	}

	/// <summary>
	///     Optional fixed Sqlite connection string. When unset a temporary file is used.
	/// </summary>
	public string? SqliteConnectionString { get; init; }

	/// <summary>
	///     Set to run against Postgres instead of Sqlite.
	/// </summary>
	public string? PostgresConnectionString { get; init; }

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseSetting("Database:Provider", PostgresConnectionString is null ? "Sqlite" : "Postgres");
		builder.UseSetting("ConnectionStrings:Sqlite", SqliteConnectionString ?? $"Data Source={_dbPath}");
		if (PostgresConnectionString is not null)
		{
			builder.UseSetting("ConnectionStrings:Postgres", PostgresConnectionString);
		}

		// Seeded tasks are due on first start (definition sync, health checks). Tests queue the
		// commands they need, so the scheduler stays off to keep them off the network.
		builder.ConfigureTestServices(services => services.Remove(services.Single(descriptor =>
			descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(SchedulerHostedService))));
		builder.ConfigureTestServices(ConfigureTestServices);
	}

	/// <summary>
	///     Hook for derived factories to replace services with test doubles.
	/// </summary>
	protected virtual void ConfigureTestServices(IServiceCollection services)
	{
	}

	/// <summary>
	///     Create a client that sends the seeded API key.
	/// </summary>
	public async Task<HttpClient> CreateAuthorizedClientAsync()
	{
		var client = CreateClient();
		var apiKey = await WithDbAsync(async db => (await db.GeneralConfig.AsNoTracking().SingleAsync()).ApiKey);
		client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
		return client;
	}

	/// <summary>
	///     The seeded API key, read through the host's own connection settings.
	/// </summary>
	public string ReadApiKey()
	{
		CreateClient().Dispose();
		return WithDbAsync(db => db.GeneralConfig.AsNoTracking().Select(x => x.ApiKey).FirstAsync()).GetAwaiter().GetResult();
	}

	/// <summary>
	///     Run a query against the host's database.
	/// </summary>
	public async Task<T> WithDbAsync<T>(Func<SubmarineDbContext, Task<T>> action)
	{
		await using var scope = Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
		return await action(db);
	}

	/// <summary>
	///     Path of the Sqlite database file, empty for Postgres runs.
	/// </summary>
	public string DbPath => SqliteConnectionString is null ? _dbPath : string.Empty;

	private bool _migrated;

	/// <summary>
	///     Migrates and seeds the target database. WebApplicationFactory applies host
	///     overrides only after Program's pre-build bootstrap runs, so the tests own this step.
	/// </summary>
	private void MigrateOnce()
	{
		if (_migrated)
		{
			return;
		}

		_migrated = true;
		var settings = new Dictionary<string, string?>
		{
			["Database:Provider"] = PostgresConnectionString is null ? "Sqlite" : "Postgres",
			["ConnectionStrings:Sqlite"] = SqliteConnectionString ?? $"Data Source={_dbPath}",
			["ConnectionStrings:Postgres"] = PostgresConnectionString ?? "Host=localhost"
		};
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
		var services = new ServiceCollection().AddSubmarinePersistence(configuration);
		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		SubmarineInitializer.InitializeAsync(scope.ServiceProvider).GetAwaiter().GetResult();
	}

	/// <summary>
	///     Create a client keeping cookies across requests, no redirects.
	/// </summary>
	public new HttpClient CreateClient()
	{
		MigrateOnce();
		return base.CreateClient();
	}

	/// <summary>
	///     Create a client keeping cookies across requests, no redirects.
	/// </summary>
	public HttpClient CreateCookieClient()
	{
		MigrateOnce();
		return CreateDefaultClient(new CookieJarHandler());
	}

	private sealed class CookieJarHandler : DelegatingHandler
	{
		private readonly CookieContainer _container = new();

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var uri = request.RequestUri!;
			var cookieHeader = _container.GetCookieHeader(uri);
			if (!string.IsNullOrEmpty(cookieHeader))
			{
				request.Headers.Add("Cookie", cookieHeader);
			}

			var response = await base.SendAsync(request, cancellationToken);
			if (response.Headers.TryGetValues("Set-Cookie", out var values))
			{
				foreach (var value in values)
				{
					_container.SetCookies(uri, value);
				}
			}

			return response;
		}
	}

	/// <inheritdoc />
	public override async ValueTask DisposeAsync()
	{
		await base.DisposeAsync();
		try
		{
			Directory.Delete(_directory, recursive: true);
		}
		catch (IOException)
		{
			// Best effort cleanup.
		}
	}
}
