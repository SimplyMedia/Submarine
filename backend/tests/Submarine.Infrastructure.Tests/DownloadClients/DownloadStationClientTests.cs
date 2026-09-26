using Shouldly;
using Xunit;
using System.Text.Json;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class DownloadStationClientTests
{
	private static DownloadStationSettings Settings()
		=> new() { Host = "ds.local", Port = 5000, Username = "admin", Password = "secret", Directory = "/video/tv" };

	private static HttpResponseMessage Success(object data)
		=> StubHttpHandler.Json(JsonSerializer.Serialize(new { success = true, data }));

	private static HttpResponseMessage Failure(int code)
		=> StubHttpHandler.Json($"{{\"success\":false,\"error\":{{\"code\":{code}}}}}");

	private static HttpResponseMessage Login()
		=> Success(new { sid = "sid-42" });

	[Fact]
	public async Task AddAsync_ShouldLoginCreateTaskAndResolveId()
	{
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.PathAndQuery.Contains("auth.cgi"))
				return Login();
			if (request.RequestUri!.PathAndQuery.Contains("task.cgi"))
			{
				if (request.RequestUri.PathAndQuery.Contains("method=create"))
					return Success(new { });
				return Success(new
				{
					tasks = new[]
					{
						new Dictionary<string, object>
						{
							["id"] = "dbid_1", ["title"] = "Some.Show.S01E01", ["size"] = 1000,
							["status"] = "downloading",
							["additional"] = new Dictionary<string, object>
							{
								["transfer"] = new Dictionary<string, object> { ["size_downloaded"] = 400 },
								["detail"] = new Dictionary<string, object> { ["destination"] = "/video/tv" }
							}
						}
					}
				});
			}

			return StubHttpHandler.Json("{}");
		});
		var client = new DownloadStationClient(Settings(), 9, "ds", new HttpClient(handler));

		var id = await client.AddAsync(new RemoteRelease
		{
			Title = "Some.Show.S01E01",
			DownloadUrl = "http://indexer/torrents/1",
			Size = 1000,
			Protocol = Protocol.BITTORRENT,
			Category = RemoteReleaseCategory.SERIES
		}, null, TestContext.Current.CancellationToken);

		id.ShouldBe("dbid_1");
		var create = handler.Requests.Single(request => request.Url.Contains("method=create"));
		create.Url.ShouldContain("uri=http%3A%2F%2Findexer%2Ftorrents%2F1");
		create.Url.ShouldContain("destination=%2Fvideo%2Ftv");
		handler.Requests.Where(request => request.Url.Contains("task.cgi")).All(request => request.Url.Contains("_sid=sid-42"))
			.ShouldBeTrue();
		handler.Requests.Where(request => request.Url.Contains("auth.cgi")).All(request => !request.Url.Contains("_sid"))
			.ShouldBeTrue();
	}

	[Fact]
	public async Task AddAsync_ShouldUseCategoryAsDestination_WhenDirectoryNotConfigured()
	{
		string? createUrl = null;
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.PathAndQuery.Contains("auth.cgi"))
				return Login();
			if (request.RequestUri!.PathAndQuery.Contains("method=create"))
			{
				createUrl = request.RequestUri.PathAndQuery;
				return Success(new { });
			}

			return Success(new { tasks = Array.Empty<object>() });
		});
		var client = new DownloadStationClient(
			new DownloadStationSettings { Host = "ds.local", Port = 5000, Username = "admin", Password = "secret", Category = "tv-sonarr" },
			9, "ds", new HttpClient(handler));

		await Should.ThrowAsync<DownloadClientException>(() => client.AddAsync(new RemoteRelease
		{
			Title = "Some.Show.S01E01",
			DownloadUrl = "http://indexer/torrents/1",
			Size = 1000,
			Protocol = Protocol.BITTORRENT,
			Category = RemoteReleaseCategory.SERIES
		}, null, TestContext.Current.CancellationToken));

		createUrl.ShouldNotBeNull();
		createUrl.ShouldContain("destination=tv-sonarr");
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapTasksStatusesAndIdentity()
	{
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.PathAndQuery.Contains("auth.cgi"))
				return Login();
			return Success(new
			{
				tasks = new object[]
				{
					new Dictionary<string, object>
					{
						["id"] = "1", ["title"] = "Downloading", ["size"] = 1000, ["status"] = "downloading",
						["additional"] = new Dictionary<string, object>
						{
							["transfer"] = new Dictionary<string, object> { ["size_downloaded"] = 400 },
							["detail"] = new Dictionary<string, object> { ["destination"] = "/video/tv" }
						}
					},
					new Dictionary<string, object>
					{
						["id"] = "2", ["title"] = "Finished", ["size"] = 2000, ["status"] = "finished",
						["additional"] = new Dictionary<string, object>
						{
							["transfer"] = new Dictionary<string, object> { ["size_downloaded"] = 2000 },
							["detail"] = new Dictionary<string, object> { ["destination"] = "/video/tv" }
						}
					},
					new Dictionary<string, object>
					{
						["id"] = "3", ["title"] = "Paused", ["size"] = 3000, ["status"] = "paused",
						["additional"] = new Dictionary<string, object>
						{
							["transfer"] = new Dictionary<string, object> { ["size_downloaded"] = 0 },
							["detail"] = new Dictionary<string, object> { ["destination"] = "/video/tv" }
						}
					},
					new Dictionary<string, object>
					{
						["id"] = "4", ["title"] = "Broken", ["size"] = 4000, ["status"] = "error",
						["additional"] = new Dictionary<string, object>
						{
							["transfer"] = new Dictionary<string, object> { ["size_downloaded"] = 0 },
							["detail"] = new Dictionary<string, object> { ["destination"] = "/video/tv" }
						}
					},
					new Dictionary<string, object>
					{
						["id"] = "5", ["title"] = "Foreign", ["size"] = 5000, ["status"] = "downloading",
						["additional"] = new Dictionary<string, object>
						{
							["transfer"] = new Dictionary<string, object> { ["size_downloaded"] = 0 },
							["detail"] = new Dictionary<string, object> { ["destination"] = "/video/movies" }
						}
					}
				}
			});
		});
		var client = new DownloadStationClient(Settings(), 9, "ds", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(5);
		items[0].Status.ShouldBe(DownloadItemStatus.DOWNLOADING);
		items[0].RemainingSize.ShouldBe(600);
		items[0].OutputPath.ShouldBe("/video/tv");
		items[0].IsReadOnly.ShouldBeFalse();
		items[0].Protocol.ShouldBe(Protocol.BITTORRENT);
		items[0].DownloadClientId.ShouldBe(9);
		items[0].DownloadClientName.ShouldBe("ds");
		items[1].Status.ShouldBe(DownloadItemStatus.COMPLETED);
		items[2].Status.ShouldBe(DownloadItemStatus.PAUSED);
		items[3].Status.ShouldBe(DownloadItemStatus.FAILED);
		items[4].IsReadOnly.ShouldBeTrue();
	}

	[Fact]
	public async Task RemoveAsync_ShouldDeleteTask()
	{
		var handler = new StubHttpHandler((request, _) =>
			request.RequestUri!.PathAndQuery.Contains("auth.cgi")
				? Login()
				: Success(new { }));
		var client = new DownloadStationClient(Settings(), 9, "ds", new HttpClient(handler));

		await client.RemoveAsync("dbid_1", true, TestContext.Current.CancellationToken);

		var delete = handler.Requests.Single(request => request.Url.Contains("method=delete"));
		delete.Url.ShouldContain("id=dbid_1");
		delete.Url.ShouldContain("_sid=sid-42");
	}

	[Fact]
	public async Task TestAsync_ShouldQueryApiInfo()
	{
		var handler = new StubHttpHandler((request, _) =>
			StubHttpHandler.Json("""{"success":true,"data":{"SYNO.API.Auth":{"path":"auth.cgi"},"SYNO.DownloadStation.Task":{"path":"DownloadStation/task.cgi"}}}"""));
		var client = new DownloadStationClient(Settings(), 9, "ds", new HttpClient(handler));

		await client.TestAsync(TestContext.Current.CancellationToken);

		handler.Requests[0].Url.ShouldContain("SYNO.API.Info");
		handler.Requests[0].Url.ShouldContain("query=SYNO.API.Auth%2CSYNO.DownloadStation.Task");
	}

	[Fact]
	public async Task AddAsync_ShouldThrow_WhenTaskCannotBeResolved()
	{
		var handler = new StubHttpHandler((request, _) =>
			request.RequestUri!.PathAndQuery.Contains("auth.cgi")
				? Login()
				: Success(new { tasks = Array.Empty<object>() }));
		var client = new DownloadStationClient(Settings(), 9, "ds", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() => client.AddAsync(new RemoteRelease
		{
			Title = "Some.Show.S01E01",
			DownloadUrl = "http://indexer/torrents/1",
			Protocol = Protocol.BITTORRENT,
			Category = RemoteReleaseCategory.SERIES
		}, null, TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("could not be resolved");
	}

	[Fact]
	public async Task Requests_ShouldReLogin_WhenSessionExpired()
	{
		var logins = 0;
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.PathAndQuery.Contains("auth.cgi"))
			{
				logins++;
				return Login();
			}

			return logins == 1 ? Failure(105) : Success(new { tasks = Array.Empty<object>() });
		});
		var client = new DownloadStationClient(Settings(), 9, "ds", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(0);
		logins.ShouldBe(2);
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenApiInfoFails()
	{
		var handler = new StubHttpHandler((_, _) => Failure(101));
		var client = new DownloadStationClient(Settings(), 9, "ds", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("101");
	}
}
