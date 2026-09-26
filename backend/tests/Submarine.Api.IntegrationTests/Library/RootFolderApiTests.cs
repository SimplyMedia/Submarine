using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Xunit;

namespace Submarine.Api.IntegrationTests.Library;

public sealed class RootFolderApiTests
{
	[Fact]
	public async Task RootFolder_ShouldReportUnmappedFoldersAndDiskSpace_AndRejectDeletionWhileReferenced()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = Directory.CreateTempSubdirectory("submarine-root-api-").FullName;
		var unmapped = Directory.CreateDirectory(Path.Combine(root, "unmapped-child")).FullName;
		try
		{
			var created = await client.PostAsJsonAsync("/api/v1/root-folders", new { path = root, mediaKind = "SERIES" });
			created.StatusCode.ShouldBe(HttpStatusCode.Created);
			var folder = await created.Content.ReadFromJsonAsync<JsonElement>();
			var id = folder.GetProperty("id").GetInt32();
			folder.GetProperty("accessible").GetBoolean().ShouldBeTrue();
			folder.GetProperty("freeSpace").GetInt64().ShouldBeGreaterThan(0);
			folder.GetProperty("totalSpace").GetInt64().ShouldBeGreaterThan(0);
			folder.GetProperty("unmappedFolders").EnumerateArray().Select(item => item.GetString()).ShouldContain(unmapped);

			await factory.WithDbAsync(async db =>
			{
				db.ImportLists.Add(new ImportList { Name = "Root folder consumer", Type = ImportListType.CUSTOM, RootFolderId = id });
				await db.SaveChangesAsync(TestContext.Current.CancellationToken);
				return 0;
			});
			var conflict = await client.DeleteAsync($"/api/v1/root-folders/{id}");
			conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
			(await factory.WithDbAsync(db => db.RootFolders.AnyAsync(item => item.Id == id, TestContext.Current.CancellationToken))).ShouldBeTrue();
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}
}
