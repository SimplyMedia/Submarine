using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Health;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Infrastructure.Tests.Health;

/// <summary>
///     Asserts the download client root folder, remote path mapping and import mechanism health checks.
/// </summary>
public sealed class DownloadHealthChecksTests : IAsyncLifetime
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
		Db.DownloadConfig.Add(new DownloadConfig());
		await Db.SaveChangesAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _provider.DisposeAsync();
		await _connection.DisposeAsync();
	}

	private SubmarineDbContext Db => _provider.GetRequiredService<SubmarineDbContext>();

	private static IDownloadClientFactory FactoryReturning(params string[] outputRootFolders)
	{
		var factory = Substitute.For<IDownloadClientFactory>();
		var client = Substitute.For<IDownloadClient>();
		client.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(new DownloadClientStatus(outputRootFolders));
		factory.Create(Arg.Any<DownloadClientType>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(client);
		return factory;
	}

	[Fact]
	public async Task RootFolderCheck_ShouldWarn_WhenClientOutputsDirectlyToRootFolder()
	{
		Db.RootFolders.Add(new RootFolder { Path = "/media/tv", MediaKind = MediaKind.SERIES });
		Db.DownloadClients.Add(new DownloadClient { Name = "QB", Enable = true });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new DownloadClientRootFolderHealthCheck(Db, FactoryReturning("/media/tv"))
			.CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.WARNING && x.Message.Contains("QB"));
	}

	[Fact]
	public async Task RootFolderCheck_ShouldBeEmpty_WhenClientOutputsElsewhere()
	{
		Db.RootFolders.Add(new RootFolder { Path = "/media/tv", MediaKind = MediaKind.SERIES });
		Db.DownloadClients.Add(new DownloadClient { Name = "QB", Enable = true });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new DownloadClientRootFolderHealthCheck(Db, FactoryReturning("/downloads"))
			.CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldBeEmpty();
	}

	[Fact]
	public async Task RemotePathMappingCheck_ShouldError_WhenOutputFolderMissingLocally()
	{
		Db.DownloadConfig.First().EnableCompletedDownloadHandling = true;
		Db.DownloadClients.Add(new DownloadClient { Name = "QB", Enable = true });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new RemotePathMappingHealthCheck(Db, FactoryReturning("/definitely/not/here"))
			.CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.ERROR && x.Message.Contains("QB"));
	}

	[Fact]
	public async Task RemotePathMappingCheck_ShouldBeEmpty_WhenFolderExistsLocally()
	{
		var temp = Path.Combine(Path.GetTempPath(), $"sub-health-{Guid.NewGuid():N}");
		Directory.CreateDirectory(temp);
		try
		{
			Db.DownloadConfig.First().EnableCompletedDownloadHandling = true;
			Db.DownloadClients.Add(new DownloadClient { Name = "QB", Enable = true });
			await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

			var issues = await new RemotePathMappingHealthCheck(Db, FactoryReturning(temp))
				.CheckAsync(TestContext.Current.CancellationToken);

			issues.ShouldBeEmpty();
		}
		finally
		{
			Directory.Delete(temp, recursive: true);
		}
	}

	[Fact]
	public async Task RemotePathMappingCheck_ShouldBeEmpty_WhenCompletedDownloadHandlingDisabled()
	{
		Db.DownloadConfig.First().EnableCompletedDownloadHandling = false;
		Db.DownloadClients.Add(new DownloadClient { Name = "QB", Enable = true });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new RemotePathMappingHealthCheck(Db, FactoryReturning("/definitely/not/here"))
			.CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldBeEmpty();
	}

	[Fact]
	public async Task ImportMechanismCheck_ShouldWarn_WhenCompletedDownloadHandlingDisabled()
	{
		Db.DownloadConfig.First().EnableCompletedDownloadHandling = false;
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new ImportMechanismHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.WARNING && x.Message.Contains("manually"));
	}

	[Fact]
	public async Task ImportMechanismCheck_ShouldBeEmpty_WhenCompletedDownloadHandlingEnabled()
	{
		Db.DownloadConfig.First().EnableCompletedDownloadHandling = true;
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new ImportMechanismHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldBeEmpty();
	}
}
