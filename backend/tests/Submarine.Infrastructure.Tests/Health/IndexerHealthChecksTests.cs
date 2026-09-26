using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.Health;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Infrastructure.Tests.Health;

/// <summary>
///     Asserts the indexer status, RSS, search, download client and Jackett health checks.
/// </summary>
public sealed class IndexerHealthChecksTests : IAsyncLifetime
{
	private SqliteConnection _connection = null!;
	private ServiceProvider _provider = null!;

	public async ValueTask InitializeAsync()
	{
		_connection = new SqliteConnection("DataSource=:memory:");
		_connection.Open();
		var services = new ServiceCollection();
		services.AddSingleton<TimeProvider>(new FakeTimeProvider());
		services.AddDbContext<SqliteSubmarineDbContext>(options => options.UseSqlite(_connection));
		services.AddScoped<SubmarineDbContext>(sp => sp.GetRequiredService<SqliteSubmarineDbContext>());
		_provider = services.BuildServiceProvider();
		await Db.Database.EnsureCreatedAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _provider.DisposeAsync();
		await _connection.DisposeAsync();
	}

	private SubmarineDbContext Db => _provider.GetRequiredService<SubmarineDbContext>();
	private TimeProvider Time => _provider.GetRequiredService<TimeProvider>();

	[Fact]
	public async Task StatusCheck_ShouldWarn_WhenSomeIndexersRecentlyFailed()
	{
		var ok = new Indexer { Name = "Ok" };
		var flaky = new Indexer { Name = "Flaky" };
		Db.Indexers.AddRange(ok, flaky);
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var now = Time.GetUtcNow().UtcDateTime;
		Db.IndexerStatuses.Add(new IndexerStatus
		{
			IndexerId = flaky.Id,
			Indexer = flaky,
			InitialFailure = now.AddHours(-1),
			DisabledUntil = now.AddMinutes(10)
		});
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new IndexerStatusHealthCheck(Db, Time).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.WARNING && x.Message.Contains("Flaky"));
	}

	[Fact]
	public async Task StatusCheck_ShouldError_WhenAllIndexersRecentlyFailed()
	{
		var flaky = new Indexer { Name = "Flaky" };
		Db.Indexers.Add(flaky);
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var now = Time.GetUtcNow().UtcDateTime;
		Db.IndexerStatuses.Add(new IndexerStatus
		{
			IndexerId = flaky.Id,
			Indexer = flaky,
			InitialFailure = now.AddHours(-1),
			DisabledUntil = now.AddMinutes(10)
		});
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new IndexerStatusHealthCheck(Db, Time).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.ERROR);
	}

	[Fact]
	public async Task LongTermStatusCheck_ShouldWarn_WhenFailureOlderThanSixHours()
	{
		var ok = new Indexer { Name = "Ok" };
		var stale = new Indexer { Name = "Stale" };
		Db.Indexers.AddRange(ok, stale);
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var now = Time.GetUtcNow().UtcDateTime;
		Db.IndexerStatuses.Add(new IndexerStatus
		{
			IndexerId = stale.Id,
			Indexer = stale,
			InitialFailure = now.AddHours(-10),
			DisabledUntil = now.AddMinutes(10)
		});
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new IndexerLongTermStatusHealthCheck(Db, Time).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.WARNING && x.Message.Contains("Stale"));
	}

	[Fact]
	public async Task LongTermStatusCheck_ShouldBeEmpty_WhenFailureIsRecent()
	{
		var indexer = new Indexer { Name = "Flaky" };
		Db.Indexers.Add(indexer);
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var now = Time.GetUtcNow().UtcDateTime;
		Db.IndexerStatuses.Add(new IndexerStatus
		{
			IndexerId = indexer.Id,
			Indexer = indexer,
			InitialFailure = now.AddHours(-1),
			DisabledUntil = now.AddMinutes(10)
		});
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new IndexerLongTermStatusHealthCheck(Db, Time).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldBeEmpty();
	}

	[Fact]
	public async Task RssCheck_ShouldError_WhenNoIndexerHasRssEnabled()
	{
		Db.Indexers.Add(new Indexer { Name = "NoRss", EnableRss = false });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new IndexerRssHealthCheck(Db, Time).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.ERROR && x.Message.Contains("RSS sync"));
	}

	[Fact]
	public async Task RssCheck_ShouldBeEmpty_WhenAnIndexerIsActive()
	{
		Db.Indexers.Add(new Indexer { Name = "Rss", EnableRss = true });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new IndexerRssHealthCheck(Db, Time).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldBeEmpty();
	}

	[Fact]
	public async Task SearchCheck_ShouldWarn_WhenNoAutomaticOrInteractiveSearch()
	{
		Db.Indexers.Add(new Indexer { Name = "Quiet", EnableAutomaticSearch = false, EnableInteractiveSearch = false });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new IndexerSearchHealthCheck(Db, Time).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Message.Contains("automatic search"));
		issues.ShouldContain(x => x.Message.Contains("interactive search"));
	}

	[Fact]
	public async Task SearchCheck_ShouldBeEmpty_WhenIndexerSearchIsActive()
	{
		Db.Indexers.Add(new Indexer { Name = "Active", EnableAutomaticSearch = true, EnableInteractiveSearch = true });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new IndexerSearchHealthCheck(Db, Time).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldBeEmpty();
	}

	[Fact]
	public async Task DownloadClientCheck_ShouldWarn_WhenIndexerReferencesDisabledClient()
	{
		var client = new DownloadClient { Name = "Off", Enable = false };
		Db.DownloadClients.Add(client);
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
		Db.Indexers.Add(new Indexer { Name = "Bad", DownloadClientId = client.Id });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new IndexerDownloadClientHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.WARNING && x.Message.Contains("Bad"));
	}

	[Fact]
	public async Task DownloadClientCheck_ShouldBeEmpty_WhenClientIsEnabled()
	{
		var client = new DownloadClient { Name = "On", Enable = true };
		Db.DownloadClients.Add(client);
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
		Db.Indexers.Add(new Indexer { Name = "Good", DownloadClientId = client.Id });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new IndexerDownloadClientHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldBeEmpty();
	}

	[Fact]
	public async Task JackettAllCheck_ShouldWarn_WhenBaseUrlUsesAllEndpoint()
	{
		Db.Indexers.Add(new Indexer
		{
			Name = "Jackett",
			Implementation = IndexerImplementation.TORZNAB,
			BaseUrl = "http://localhost:9117/torznab/all/api"
		});
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new IndexerJackettAllHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.WARNING && x.Message.Contains("Jackett"));
	}

	[Fact]
	public async Task JackettAllCheck_ShouldBeEmpty_WhenBaseUrlIsSpecificIndexer()
	{
		Db.Indexers.Add(new Indexer
		{
			Name = "Jackett",
			Implementation = IndexerImplementation.TORZNAB,
			BaseUrl = "http://localhost:9117/torznab/rarbg/api"
		});
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new IndexerJackettAllHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldBeEmpty();
	}
}
