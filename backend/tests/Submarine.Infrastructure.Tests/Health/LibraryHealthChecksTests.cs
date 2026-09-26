using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Health;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Infrastructure.Tests.Health;

/// <summary>
///     Asserts the mount, recycle bin, import list and collection root folder health checks.
/// </summary>
public sealed class LibraryHealthChecksTests : IAsyncLifetime
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
		Db.MediaManagementConfig.Add(new MediaManagementConfig());
		await Db.SaveChangesAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _provider.DisposeAsync();
		await _connection.DisposeAsync();
	}

	private SubmarineDbContext Db => _provider.GetRequiredService<SubmarineDbContext>();

	[Fact]
	[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
	public async Task MountCheck_ShouldError_WhenRootFolderIsReadOnly()
	{
		if (OperatingSystem.IsWindows())
		{
			Assert.Skip("Unix file permissions are only meaningful on Linux or macOS");
			return;
		}

		var temp = Path.Combine(Path.GetTempPath(), $"sub-health-{Guid.NewGuid():N}");
		Directory.CreateDirectory(temp);
		try
		{
			File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserExecute);
			Db.RootFolders.Add(new RootFolder { Path = temp, MediaKind = MediaKind.SERIES });
			await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

			var issues = await new MountHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

			issues.ShouldContain(x => x.Type == HealthIssueType.ERROR && x.Message.Contains("read-only"));
		}
		finally
		{
			File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
			Directory.Delete(temp, recursive: true);
		}
	}

	[Fact]
	public async Task MountCheck_ShouldBeEmpty_WhenRootFolderIsWritable()
	{
		var temp = Path.Combine(Path.GetTempPath(), $"sub-health-{Guid.NewGuid():N}");
		Directory.CreateDirectory(temp);
		try
		{
			Db.RootFolders.Add(new RootFolder { Path = temp, MediaKind = MediaKind.SERIES });
			await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

			var issues = await new MountHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

			issues.ShouldBeEmpty();
		}
		finally
		{
			Directory.Delete(temp, recursive: true);
		}
	}

	[Fact]
	[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
	public async Task RecyclingBinCheck_ShouldError_WhenNotWritable()
	{
		if (OperatingSystem.IsWindows())
		{
			Assert.Skip("Unix file permissions are only meaningful on Linux or macOS");
			return;
		}

		var readOnlyParent = Path.Combine(Path.GetTempPath(), $"sub-health-{Guid.NewGuid():N}");
		Directory.CreateDirectory(readOnlyParent);
		try
		{
			File.SetUnixFileMode(readOnlyParent, UnixFileMode.UserRead | UnixFileMode.UserExecute);
			Db.MediaManagementConfig.First().RecycleBinPath = Path.Combine(readOnlyParent, "bin");
			await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

			var issues = await new RecyclingBinHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

			issues.ShouldContain(x => x.Type == HealthIssueType.ERROR && x.Message.Contains("recycle bin"));
		}
		finally
		{
			File.SetUnixFileMode(readOnlyParent, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
			Directory.Delete(readOnlyParent, recursive: true);
		}
	}

	[Fact]
	public async Task RecyclingBinCheck_ShouldBeEmpty_WhenWritable()
	{
		var temp = Path.Combine(Path.GetTempPath(), $"sub-health-{Guid.NewGuid():N}");
		try
		{
			Db.MediaManagementConfig.First().RecycleBinPath = temp;
			await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

			var issues = await new RecyclingBinHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

			issues.ShouldBeEmpty();
		}
		finally
		{
			if (Directory.Exists(temp))
			{
				Directory.Delete(temp, recursive: true);
			}
		}
	}

	[Fact]
	public async Task RecyclingBinCheck_ShouldBeEmpty_WhenPathNotConfigured()
	{
		Db.MediaManagementConfig.First().RecycleBinPath = string.Empty;
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new RecyclingBinHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldBeEmpty();
	}

	[Fact]
	public async Task ImportListRootFolderCheck_ShouldError_WhenAutoAddListHasNoRootFolder()
	{
		Db.ImportLists.Add(new ImportList { Name = "Trakt", Enable = true, EnableAutomaticAdd = true, RootFolderId = null });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new ImportListRootFolderHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.ERROR && x.Message.Contains("Trakt"));
	}

	[Fact]
	public async Task ImportListRootFolderCheck_ShouldBeEmpty_WhenRootFolderConfigured()
	{
		var root = new RootFolder { Path = "/media/tv", MediaKind = MediaKind.SERIES };
		Db.RootFolders.Add(root);
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
		Db.ImportLists.Add(new ImportList { Name = "Trakt", Enable = true, EnableAutomaticAdd = true, RootFolderId = root.Id });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new ImportListRootFolderHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldBeEmpty();
	}

	[Fact]
	public async Task ImportListRootFolderCheck_ShouldBeEmpty_WhenAutomaticAddDisabled()
	{
		Db.ImportLists.Add(new ImportList { Name = "Trakt", Enable = true, EnableAutomaticAdd = false, RootFolderId = null });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new ImportListRootFolderHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldBeEmpty();
	}

	[Fact]
	public async Task MovieCollectionRootFolderCheck_ShouldError_WhenMonitoredCollectionHasNoRootFolder()
	{
		Db.Collections.Add(new Collection { TmdbCollectionId = 1, Title = "Marvel", Monitored = true, RootFolderId = null });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new MovieCollectionRootFolderHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.ERROR && x.Message.Contains("Marvel"));
	}

	[Fact]
	public async Task MovieCollectionRootFolderCheck_ShouldBeEmpty_WhenNotMonitored()
	{
		Db.Collections.Add(new Collection { TmdbCollectionId = 1, Title = "Marvel", Monitored = false, RootFolderId = null });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new MovieCollectionRootFolderHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldBeEmpty();
	}
}
