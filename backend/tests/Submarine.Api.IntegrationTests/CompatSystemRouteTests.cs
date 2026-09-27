using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class CompatSystemRouteTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public CompatSystemRouteTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task SystemRoutes_ShouldRequireApiKeyAndReturnCompatibilityShapes()
	{
		using var anonymous = _factory.CreateClient();
		foreach (var path in new[]
		{
			"/compat/sonarr/api/v3/health",
			"/compat/sonarr/api/v3/diskspace",
			"/compat/sonarr/api/v3/remotepathmapping",
			"/compat/sonarr/api/v3/filesystem",
			"/compat/radarr/api/v3/health",
			"/compat/radarr/api/v3/diskspace",
			"/compat/radarr/api/v3/remotepathmapping",
			"/compat/radarr/api/v3/filesystem",
			"/compat/prowlarr/api/v1/health"
		})
		{
			(await anonymous.GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		}

		var browsePath = Path.Combine(Path.GetTempPath(), "compat-filesystem-" + Guid.NewGuid().ToString("N"));
		var childPath = Path.Combine(browsePath, "child");
		Directory.CreateDirectory(childPath);
		var filePath = Path.Combine(browsePath, "sample.txt");
		await File.WriteAllTextAsync(filePath, "sample");
		try
		{
			await _factory.WithDbAsync(async db =>
			{
				db.HealthIssues.Add(new HealthIssue
				{
					Type = HealthIssueType.WARNING,
					Source = "Compat test",
					Message = "Actual health issue",
					WikiUrl = "https://example.test/health"
				});
				db.RemotePathMappings.Add(new RemotePathMapping
				{
					Host = "download-host",
					RemotePath = "/remote/downloads",
					LocalPath = "/local/downloads"
				});
				db.RootFolders.Add(new RootFolder { Path = browsePath, MediaKind = MediaKind.SERIES });
				await db.SaveChangesAsync();
				return true;
			});

			using var client = await _factory.CreateAuthorizedClientAsync();
			foreach (var (facade, version) in new[] { ("sonarr", "v3"), ("radarr", "v3"), ("prowlarr", "v1") })
			{
				var health = await GetArrayAsync(client, $"/compat/{facade}/api/{version}/health");
				var issue = health.EnumerateArray().Single(x => x.GetProperty("source").GetString() == "Compat test");
				issue.GetProperty("type").GetString().ShouldBe("warning");
				issue.GetProperty("message").GetString().ShouldBe("Actual health issue");
				issue.GetProperty("wikiUrl").GetString().ShouldBe("https://example.test/health");
			}

			var expectedParent = Directory.GetParent(browsePath)!.FullName + Path.DirectorySeparatorChar;
			foreach (var facade in new[] { "sonarr", "radarr" })
			{
				var prefix = $"/compat/{facade}/api/v3";
				var diskSpace = await GetArrayAsync(client, prefix + "/diskspace");
				var rootSpace = diskSpace.EnumerateArray().Single(x => x.GetProperty("path").GetString() == browsePath);
				rootSpace.GetProperty("label").GetString().ShouldBe(browsePath);
				rootSpace.GetProperty("freeSpace").ValueKind.ShouldBe(JsonValueKind.Number);
				rootSpace.GetProperty("totalSpace").ValueKind.ShouldBe(JsonValueKind.Number);

				var mappings = await GetArrayAsync(client, prefix + "/remotepathmapping");
				var mapping = mappings.EnumerateArray().Single(x => x.GetProperty("host").GetString() == "download-host");
				mapping.GetProperty("remotePath").GetString().ShouldBe("/remote/downloads");
				mapping.GetProperty("localPath").GetString().ShouldBe("/local/downloads");

				// Root ("/") stays browsable, matching native's fallback when no path is supplied.
				var root = await GetObjectAsync(client, prefix + "/filesystem");
				root.GetProperty("directories").ValueKind.ShouldBe(JsonValueKind.Array);

				// Trailing slash browses the folder's own contents; entries carry the native
				// type/extension/size/lastModified metadata and directories always end in a
				// separator regardless of allowFoldersWithoutTrailingSlashes.
				var self = await GetObjectAsync(client,
					$"{prefix}/filesystem?path={Uri.EscapeDataString(browsePath + Path.DirectorySeparatorChar)}&includeFiles=true");
				self.GetProperty("parent").GetString().ShouldBe(expectedParent);
				var directory = self.GetProperty("directories").EnumerateArray().Single();
				directory.GetProperty("type").GetString().ShouldBe("folder");
				directory.GetProperty("name").GetString().ShouldBe("child");
				directory.GetProperty("path").GetString().ShouldBe(childPath + Path.DirectorySeparatorChar);
				directory.GetProperty("extension").ValueKind.ShouldBe(JsonValueKind.Null);
				directory.GetProperty("size").GetInt64().ShouldBe(0);
				directory.GetProperty("lastModified").ValueKind.ShouldBe(JsonValueKind.String);
				var file = self.GetProperty("files").EnumerateArray().Single();
				file.GetProperty("type").GetString().ShouldBe("file");
				file.GetProperty("name").GetString().ShouldBe("sample.txt");
				file.GetProperty("path").GetString().ShouldBe(filePath);
				file.GetProperty("extension").GetString().ShouldBe(".txt");
				file.GetProperty("size").GetInt64().ShouldBe(new FileInfo(filePath).Length);
				file.GetProperty("lastModified").ValueKind.ShouldBe(JsonValueKind.String);

				// No trailing slash and the default flag: the query is treated as a typed-in
				// prefix and native browses the parent directory instead of the folder itself.
				var parentBrowse = await GetObjectAsync(client, $"{prefix}/filesystem?path={Uri.EscapeDataString(browsePath)}");
				parentBrowse.GetProperty("directories").EnumerateArray()
					.Single(x => x.GetProperty("name").GetString() == Path.GetFileName(browsePath))
					.GetProperty("path").GetString().ShouldBe(browsePath + Path.DirectorySeparatorChar);

				// allowFoldersWithoutTrailingSlashes lets an existing folder be browsed directly
				// even without a trailing separator.
				var directBrowse = await GetObjectAsync(client,
					$"{prefix}/filesystem?path={Uri.EscapeDataString(browsePath)}&allowFoldersWithoutTrailingSlashes=true");
				directBrowse.GetProperty("parent").GetString().ShouldBe(expectedParent);
				directBrowse.GetProperty("directories").EnumerateArray().Single().GetProperty("path").GetString()
					.ShouldBe(childPath + Path.DirectorySeparatorChar);
				directBrowse.GetProperty("files").GetArrayLength().ShouldBe(0);

				// A missing directory degrades to an empty listing instead of an error response,
				// matching native's DirectoryNotFoundException handling.
				var missingPath = Path.Combine(browsePath, "missing-child") + Path.DirectorySeparatorChar;
				var missing = await GetObjectAsync(client, $"{prefix}/filesystem?path={Uri.EscapeDataString(missingPath)}");
				missing.GetProperty("parent").GetString().ShouldBe(browsePath + Path.DirectorySeparatorChar);
				missing.GetProperty("directories").GetArrayLength().ShouldBe(0);
				missing.GetProperty("files").GetArrayLength().ShouldBe(0);
			}
		}
		finally
		{
			Directory.Delete(browsePath, recursive: true);
		}
	}

	private static async Task<JsonElement> GetArrayAsync(HttpClient client, string path)
	{
		var response = await client.GetAsync(path);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var json = await response.Content.ReadFromJsonAsync<JsonElement>();
		json.ValueKind.ShouldBe(JsonValueKind.Array);
		return json;
	}

	private static async Task<JsonElement> GetObjectAsync(HttpClient client, string path)
	{
		var response = await client.GetAsync(path);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var json = await response.Content.ReadFromJsonAsync<JsonElement>();
		json.ValueKind.ShouldBe(JsonValueKind.Object);
		return json;
	}
}
