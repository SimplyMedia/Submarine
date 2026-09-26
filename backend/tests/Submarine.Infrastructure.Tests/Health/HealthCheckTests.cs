using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Commands;
using Submarine.Core.Download;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Health;
using Submarine.Infrastructure.Updates;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Infrastructure.Tests.Health;

/// <summary>
///     Asserts the individual health checks and the diffing command handler.
/// </summary>
public sealed class HealthCheckTests : IAsyncLifetime
{
	private SqliteConnection _connection = null!;
	private ServiceProvider _provider = null!;

	public async ValueTask InitializeAsync()
	{
		_connection = new SqliteConnection("DataSource=:memory:");
		_connection.Open();
		var services = new ServiceCollection();
		services.TryAddSingleton<TimeProvider>(new FakeTimeProvider());
		services.AddDbContext<SqliteSubmarineDbContext>(options => options.UseSqlite(_connection));
		services.AddScoped<SubmarineDbContext>(sp => sp.GetRequiredService<SqliteSubmarineDbContext>());
		_provider = services.BuildServiceProvider();
		var db = _provider.GetRequiredService<SubmarineDbContext>();
		await db.Database.EnsureCreatedAsync();
		db.MediaManagementConfig.Add(new MediaManagementConfig());
		db.IndexerConfig.Add(new IndexerConfig());
		db.GeneralConfig.Add(new GeneralConfig());
		await db.SaveChangesAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _provider.DisposeAsync();
		await _connection.DisposeAsync();
	}

	private SubmarineDbContext Db => _provider.GetRequiredService<SubmarineDbContext>();

	[Fact]
	public async Task IndexerCheck_ShouldWarn_WhenNoIndexersConfigured()
	{
		var issues = await new IndexerHealthCheck(Db, _provider.GetRequiredService<TimeProvider>())
			.CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.WARNING && x.Message == "No indexers are configured");
	}

	[Fact]
	public async Task IndexerCheck_ShouldWarn_WhenNoRssOrAutomaticSearch()
	{
		Db.Indexers.Add(new Indexer
		{
			Name = "Quiet",
			EnableRss = false,
			EnableAutomaticSearch = false
		});
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new IndexerHealthCheck(Db, _provider.GetRequiredService<TimeProvider>())
			.CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Message == "No indexers have RSS sync enabled");
		issues.ShouldContain(x => x.Message == "No indexers have automatic search enabled");
	}

	[Fact]
	public async Task IndexerCheck_ShouldWarn_WhenDisabledByFailures()
	{
		var indexer = new Indexer { Name = "Flaky" };
		Db.Indexers.Add(indexer);
		Db.IndexerStatuses.Add(new IndexerStatus
		{
			IndexerId = indexer.Id,
			Indexer = indexer,
			DisabledUntil = DateTime.UtcNow.AddHours(1)
		});
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new IndexerHealthCheck(Db, _provider.GetRequiredService<TimeProvider>())
			.CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Message.Contains("Flaky is disabled until"));
	}

	[Fact]
	public async Task DownloadClientCheck_ShouldWarn_WhenNoneEnabled()
	{
		var issues = await new DownloadClientHealthCheck(Db, Substitute.For<IDownloadClientFactory>())
			.CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.WARNING && x.Message.Contains("No download clients"));
	}

	[Fact]
	public async Task DownloadClientCheck_ShouldError_WhenUnreachable()
	{
		Db.DownloadClients.Add(new DownloadClient
		{
			Name = "LocalQB",
			Type = DownloadClientType.QBITTORRENT,
			Enable = true,
			SettingsJson = """{"host":"127.0.0.1","port":1}"""
		});
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var factory = Substitute.For<IDownloadClientFactory>();
		var client = Substitute.For<IDownloadClient>();
		client.TestAsync(Arg.Any<CancellationToken>())
			.Returns(Task.FromException(new DownloadClientException("connection refused")));
		factory.Create(Arg.Any<DownloadClientType>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>())
			.Returns(client);

		var issues = await new DownloadClientHealthCheck(Db, factory).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.ERROR && x.Message.Contains("LocalQB is unreachable"));
	}

	[Fact]
	public async Task RootFolderCheck_ShouldError_WhenMissing()
	{
		Db.RootFolders.Add(new RootFolder { Path = "/definitely/not/here", MediaKind = MediaKind.SERIES });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new RootFolderHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.ERROR && x.Message.Contains("is missing"));
	}

	[Fact]
	public async Task RootFolderCheck_ShouldWarn_WhenBelowMinimumFreeSpace()
	{
		var temp = Path.Combine(Path.GetTempPath(), $"sub-health-{Guid.NewGuid():N}");
		Directory.CreateDirectory(temp);
		try
		{
			Db.RootFolders.Add(new RootFolder { Path = temp, MediaKind = MediaKind.SERIES });
			Db.MediaManagementConfig.First().MinimumFreeSpaceMb = 1_000_000;
			await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

			var issues = await new RootFolderHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

			issues.ShouldContain(x => x.Type == HealthIssueType.WARNING && x.Message.Contains("free space"));
		}
		finally
		{
			Directory.Delete(temp, recursive: true);
		}
	}

	[Fact]
	public async Task RootFolderCheck_ShouldSkipSpace_WhenConfigured()
	{
		var temp = Path.Combine(Path.GetTempPath(), $"sub-health-{Guid.NewGuid():N}");
		Directory.CreateDirectory(temp);
		try
		{
			Db.RootFolders.Add(new RootFolder { Path = temp, MediaKind = MediaKind.MOVIES });
			Db.MediaManagementConfig.First().MinimumFreeSpaceMb = 1_000_000;
			Db.MediaManagementConfig.First().SkipFreeSpaceCheck = true;
			await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

			var issues = await new RootFolderHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

			issues.ShouldBeEmpty();
		}
		finally
		{
			Directory.Delete(temp, recursive: true);
		}
	}

	[Fact]
	public async Task SettingsCheck_ShouldReportDisabledRss_RecycleBinAndAuth()
	{
		Db.IndexerConfig.First().RssSyncIntervalMinutes = 0;
		Db.MediaManagementConfig.First().RecycleBinPath = "/definitely/not/here";
		Db.GeneralConfig.First().AuthMethod = AuthMethod.NONE;
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new SettingsHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Source == "RSS sync" && x.Type == HealthIssueType.WARNING);
		issues.ShouldContain(x => x.Source == "Recycle bin" && x.Type == HealthIssueType.WARNING);
		issues.ShouldContain(x => x.Source == "Authentication" && x.Type == HealthIssueType.NOTICE);
	}

	[Fact]
	public async Task UpdateCheck_ShouldNotice_WhenUpdateAvailable()
	{
		var checker = Substitute.For<IUpdateChecker>();
		checker.GetLatestAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(new UpdateInfo("1.0.0", "2.0.0", "https://example/r", true, false));

		var issues = await new UpdateHealthCheck(checker).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.NOTICE && x.Message.Contains("2.0.0"));
	}

	[Fact]
	public async Task ServiceCheck_ShouldWarnPerProvider_WhenMetadataReadyReportsMissingCredentials()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["Metadata:BaseUrl"] = "http://metadata" })
			.Build();
		var factory = Substitute.For<IHttpClientFactory>();
		factory.CreateClient("health").Returns(_ => new HttpClient(new MetadataReadinessHandler())
		{
			BaseAddress = new Uri("http://metadata")
		});

		var issues = await new ServiceHealthCheck(configuration, factory).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.WARNING && x.Message == "TMDB is not configured on the metadata service");
		issues.ShouldNotContain(x => x.Message.Contains("TVDB"));
	}

	private sealed class MetadataReadinessHandler : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (request.RequestUri!.AbsolutePath == "/_status/ready")
			{
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent("""{"tmdb":false,"tvdb":true}""", Encoding.UTF8, "application/json")
				});
			}

			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
		}
	}

	[Fact]
	public async Task Handler_ShouldDiffPersistAndPublish()
	{
		var eventBus = Substitute.For<IEventBus>();
		Db.HealthIssues.Add(new HealthIssue { Type = HealthIssueType.WARNING, Source = "Old", Message = "Gone" });
		Db.HealthIssues.Add(new HealthIssue { Type = HealthIssueType.ERROR, Source = "Kept", Message = "Same message" });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var failingCheck = Substitute.For<IHealthCheck>();
		failingCheck.CheckAsync(Arg.Any<CancellationToken>())
			.Returns(Task.FromException<IReadOnlyList<Core.Events.HealthIssueSnapshot>>(
				new InvalidOperationException("broken")));

		var handler = new HealthCheckCommandHandler(
			Db,
			[
				StaticCheck(new(HealthIssueType.ERROR, "Kept", "Same message", "https://wiki")),
				StaticCheck(new(HealthIssueType.WARNING, "New", "Brand new", null)),
				failingCheck
			],
			eventBus,
			_provider.GetRequiredService<TimeProvider>(),
			NullLogger<HealthCheckCommandHandler>.Instance);

		await handler.ExecuteAsync(new HealthCheckCommand(), NullContext.Instance, TestContext.Current.CancellationToken);

		var rows = await Db.HealthIssues.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
		rows.ShouldNotContain(x => x.Source == "Old");
		rows.ShouldContain(x => x.Source == "Kept" && x.WikiUrl == "https://wiki");
		rows.ShouldContain(x => x.Source == "New");

		await eventBus.Received(1).PublishAsync(
			Arg.Is<HealthIssuesChangedEvent>(e =>
				e.Added.Any(x => x.Source == "New") && e.Restored.Any(x => x.Source == "Old")),
			Arg.Any<CancellationToken>());
	}

	private static IHealthCheck StaticCheck(Core.Events.HealthIssueSnapshot snapshot)
	{
		var check = Substitute.For<IHealthCheck>();
		check.CheckAsync(Arg.Any<CancellationToken>())
			.Returns((IReadOnlyList<Core.Events.HealthIssueSnapshot>)[snapshot]);
		return check;
	}

	private sealed class NullContext : ICommandContext
	{
		public static readonly NullContext Instance = new();

		public int CommandId => 1;

		public Task ReportProgressAsync(int percent, string? message = null, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;
	}
}
