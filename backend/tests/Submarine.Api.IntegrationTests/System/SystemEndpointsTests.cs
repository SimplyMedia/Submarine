using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Api.IntegrationTests;

/// <summary>
///     Endpoints of the notifications, health, logs, stats, updates and disk space features.
/// </summary>
public sealed class SystemEndpointsTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public SystemEndpointsTests(SubmarineApiFactory factory) => _factory = factory;

	private HttpClient Client()
	{
		_factory.CreateClient().Dispose();
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Api-Key", ReadApiKey());
		return client;
	}

	private string ReadApiKey()
	{
		return _factory.ReadApiKey();
	}

	[Fact]
	public async Task Notification_Crud_ShouldRoundTrip()
	{
		var client = Client();

		var created = await client.PostAsync("/api/v1/notifications", new StringContent(
			"""{"name":"Discord","type":"DISCORD","settingsJson":"{\"webhookUrl\":\"https://discord/api/webhooks/1\"}","onGrab":true,"onImport":true,"tags":["anime"]}""",
			Encoding.UTF8,
			"application/json"));
		created.StatusCode.ShouldBe(HttpStatusCode.Created);
		var body = await created.Content.ReadFromJsonAsync<NotificationResponse>();
		body!.Name.ShouldBe("Discord");
		body.Tags.ShouldContain("anime");

		var updated = await client.PutAsJsonAsync($"/api/v1/notifications/{body.Id}", new
		{
			name = "Discord renamed",
			type = "DISCORD",
			enable = false,
			settingsJson = """{"webhookUrl":"https://discord/api/webhooks/2"}""",
			onImport = true
		});
		updated.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updatedBody = await updated.Content.ReadFromJsonAsync<NotificationResponse>();
		updatedBody!.Name.ShouldBe("Discord renamed");
		updatedBody.Enable.ShouldBeFalse();

		var list = await client.GetFromJsonAsync<List<NotificationResponse>>("/api/v1/notifications");
		list!.ShouldContain(x => x.Id == body.Id);

		var deleted = await client.DeleteAsync($"/api/v1/notifications/{body.Id}");
		deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task Notification_Create_ShouldRejectInvalidSettings()
	{
		var client = Client();

		var response = await client.PostAsync("/api/v1/notifications", new StringContent(
			"""{"name":"Bad","type":"DISCORD","settingsJson":"{}"}""",
			Encoding.UTF8,
			"application/json"));

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
		problem!.Errors.ShouldContainKey("webhookUrl");
	}

	[Fact]
	public async Task Notification_Test_ShouldReportFailure()
	{
		var client = Client();

		var response = await client.PostAsync("/api/v1/notifications/test", new StringContent(
			"""{"type":"DISCORD","settingsJson":"{\"webhookUrl\":\"http://127.0.0.1:1/discord\"}"}""",
			Encoding.UTF8,
			"application/json"));

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Notification_Schema_ShouldDescribeAllTypes()
	{
		var client = Client();

		var schema = await client.GetFromJsonAsync<List<SchemaType>>("/api/v1/notifications/schema");

		schema!.ShouldContain(t => t.Type == "DISCORD" && t.Fields.Any(f => f.Name == "webhookUrl" && f.Required));
		schema!.ShouldContain(t => t.Type == "NTFY" && t.Fields.Any(f => f.Name == "topics" && f.Required));
	}

	[Fact]
	public async Task Health_ShouldReturnIssues()
	{
		var client = Client();

		// Hosted services do not run under the test factory, so trigger the check directly.
		using var scope = _factory.Services.CreateScope();
		var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<HealthCheckCommand>>();
		await handler.ExecuteAsync(new HealthCheckCommand(), new NoopContext(), TestContext.Current.CancellationToken);

		var response = await client.GetAsync("/api/v1/health");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var issues = await response.Content.ReadFromJsonAsync<List<HealthRow>>();
		// No indexers or download clients are configured in the fresh database.
		issues!.ShouldContain(x => x.Type == "WARNING" && x.Source == "Indexers");
	}

	private sealed class NoopContext : ICommandContext
	{
		public int CommandId => 0;

		public Task ReportProgressAsync(int percent, string? message = null, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;
	}

	[Fact]
	public async Task Logs_ShouldFilterPageAndClear()
	{
		var client = Client();
		var scope = _factory.Services.CreateScope();
		var logs = scope.ServiceProvider.GetRequiredService<LogDbContext>();
		logs.Logs.Add(new Log
		{
			Time = DateTime.UtcNow,
			Level = "Warning",
			Logger = "Test.Logger",
			Message = "something odd happened"
		});
		await logs.SaveChangesAsync();

		var page = await client.GetFromJsonAsync<LogPage>("/api/v1/logs?level=Warning&q=odd");
		page!.Items.ShouldContain(x => x.Message == "something odd happened");
		page.TotalCount.ShouldBeGreaterThanOrEqualTo(1);

		var cleared = await client.DeleteAsync("/api/v1/logs");
		cleared.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		var after = await client.GetFromJsonAsync<LogPage>("/api/v1/logs?level=Warning&q=odd");
		after!.TotalCount.ShouldBe(0);
	}

	[Fact]
	public async Task Stats_ShouldCountSeededLibrary()
	{
		var client = Client();
		var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
		var now = DateTime.UtcNow;
		var rootFolder = new RootFolder { Path = Path.GetTempPath(), MediaKind = MediaKind.SERIES };
		var qualityProfile = new QualityProfile { Name = "StatsQP" };
		var languageProfile = new LanguageProfile { Name = "StatsLP", Languages = [Language.ENGLISH] };
		db.AddRange(rootFolder, qualityProfile, languageProfile);
		await db.SaveChangesAsync();

		var series = new Series { TvdbId = 42, Title = "Stat Show", Status = SeriesStatus.CONTINUING };
		series.Episodes.Add(new Episode { SeasonNumber = 1, EpisodeNumber = 1, Monitored = true });
		series.Episodes.Add(new Episode { SeasonNumber = 1, EpisodeNumber = 2, Monitored = true });
		db.Series.Add(series);
		var version = new MediaVersion
		{
			Name = "1080p",
			Series = series,
			QualityProfileId = qualityProfile.Id,
			LanguageProfileId = languageProfile.Id,
			RootFolderId = rootFolder.Id,
			Path = "Stat Show"
		};
		db.MediaVersions.Add(version);
		await db.SaveChangesAsync();

		var file = new EpisodeFile
		{
			SeriesId = series.Id,
			MediaVersionId = version.Id,
			RelativePath = "S01E01.mkv",
			Size = 1000,
			DateAdded = now
		};
		file.Episodes.Add(series.Episodes.First());
		db.EpisodeFiles.Add(file);
		db.HistoryEvents.Add(new HistoryEvent { Type = HistoryEventType.GRABBED, SourceTitle = "x", Date = now });
		await db.SaveChangesAsync();

		var stats = await client.GetFromJsonAsync<StatsResponse>("/api/v1/stats");

		stats!.SeriesCount.ShouldBeGreaterThanOrEqualTo(1);
		stats.ContinuingCount.ShouldBeGreaterThanOrEqualTo(1);
		stats.EpisodeFileCount.ShouldBeGreaterThanOrEqualTo(1);
		stats.TotalSizeBytes.ShouldBeGreaterThanOrEqualTo(1000);
		stats.MissingEpisodes.ShouldBeGreaterThanOrEqualTo(1);
		stats.History30Days.ShouldContain(h => h.Type == "GRABBED" && h.Count >= 1);
	}

	[Fact]
	public async Task DiskSpace_ShouldReportAppData()
	{
		var client = Client();

		var response = await client.GetAsync("/api/v1/system/disk-space");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var report = await response.Content.ReadFromJsonAsync<DiskSpaceReport>();
		report!.AppData.FreeBytes.ShouldNotBeNull();
		report.AppData.FreeBytes!.Value.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task Updates_ShouldReturnCurrentVersion()
	{
		var client = Client();

		var response = await client.GetAsync("/api/v1/updates");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var update = await response.Content.ReadFromJsonAsync<UpdateResponse>();
		update!.Current.ShouldNotBeEmpty();
	}

	private sealed record NotificationResponse(
		int Id,
		string Name,
		string Type,
		bool Enable,
		IReadOnlyList<string> Tags);

	private sealed record SchemaType(string Type, IReadOnlyList<Field> Fields);

	private sealed record Field(string Name, string Type, bool Required);

	private sealed record HealthRow(int Id, string Type, string Source, string Message);

	private sealed record ProblemBody(IReadOnlyDictionary<string, string[]> Errors);

	private sealed record LogPage(IReadOnlyList<LogRow> Items, int TotalCount);

	private sealed record LogRow(int Id, string Message);

	private sealed record StatsResponse(
		int SeriesCount,
		int ContinuingCount,
		int EndedCount,
		int MovieCount,
		int EpisodeCount,
		int EpisodeFileCount,
		int MovieFileCount,
		int MissingEpisodes,
		long TotalSizeBytes,
		IReadOnlyList<HistoryCount> History30Days,
		int QueueCount);

	private sealed record HistoryCount(string Type, int Count);

	private sealed record DiskSpaceReport(RootSpace AppData, IReadOnlyList<RootSpace> RootFolders);

	private sealed record RootSpace(string Path, long? FreeBytes, long? TotalBytes);

	private sealed record UpdateResponse(string Current, string? Latest, string? ReleaseNotesUrl, bool UpdateAvailable, bool IsDocker);
}
