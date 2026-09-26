using System.Data.Common;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Mappings.Data;

namespace Submarine.Mappings.Tests.TestKit;

/// <summary>
/// Hosts the real service against a temporary SQLite database.
/// Without an admin API key every write is rejected with 503, like an unconfigured production instance.
/// </summary>
public class MappingsTestFactory(string? adminApiKey = null) : WebApplicationFactory<Program>
{
	public const string AdminApiKey = "test-admin-key";

	private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"submarine-mappings-{Guid.NewGuid():N}.db");

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseSetting("ConnectionStrings:SqliteConnection", $"Data Source={_dbPath};Cache=Shared");
		if (adminApiKey is not null)
		{
			builder.UseSetting("Auth:AdminApiKey", adminApiKey);
		}
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		if (disposing)
		{
			SqliteConnection.ClearAllPools();
			TryDeleteDatabase();
		}
	}

	private void TryDeleteDatabase()
	{
		try
		{
			if (File.Exists(_dbPath))
			{
				File.Delete(_dbPath);
			}
		}
		catch (IOException)
		{
			// Best effort cleanup.
		}
	}
}

/// <summary>
/// Factory with a configured admin API key so CRUD endpoints accept writes.
/// </summary>
public sealed class AdminMappingsTestFactory() : MappingsTestFactory(MappingsTestFactory.AdminApiKey)
{
	public HttpClient CreateAuthorizedClient()
	{
		var client = CreateClient();
		client.DefaultRequestHeaders.Add("X-Api-Key", AdminApiKey);
		return client;
	}
}

/// <summary>
/// A real SQLite mappings database for unit level tests of services and resolvers.
/// </summary>
public abstract class SqliteTestBase : IDisposable
{
	private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"submarine-mappings-{Guid.NewGuid():N}.db");

	protected SqliteTestBase()
	{
		var options = new DbContextOptionsBuilder<SqliteMappingsDbContext>()
			.UseSqlite($"Data Source={_dbPath};Cache=Shared")
			.Options;
		Db = new SqliteMappingsDbContext(options);
		Db.Database.EnsureCreated();
	}

	protected SqliteMappingsDbContext Db { get; }

	public void Dispose()
	{
		Db.Dispose();
		SqliteConnection.ClearAllPools();
		if (File.Exists(_dbPath))
		{
			File.Delete(_dbPath);
		}
	}
}

public static class TestJson
{
	public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

	public static StringContent AsJson<T>(T value) => new(JsonSerializer.Serialize(value, Options), Encoding.UTF8, "application/json");

	public static async Task<T?> ReadAs<T>(HttpResponseMessage response) =>
		await response.Content.ReadFromJsonAsync<T>(Options);

	/// <summary>
	/// Serializes a response body with all id properties removed, to compare datasets across databases.
	/// </summary>
	public static async Task<string> ReadNormalizedAsync(HttpResponseMessage response)
	{
		var parsed = JsonNode.Parse(await response.Content.ReadAsStringAsync());
		var node = parsed.ShouldNotBeNull();
		RemoveIds(node);
		return node.ToJsonString(Options);
	}

	private static void RemoveIds(JsonNode node)
	{
		if (node is JsonObject obj)
		{
			obj.Remove("id");
			foreach (var (_, value) in obj)
			{
				if (value is not null)
				{
					RemoveIds(value);
				}
			}
		}
		else if (node is JsonArray array)
		{
			foreach (var item in array)
			{
				if (item is not null)
				{
					RemoveIds(item);
				}
			}
		}
	}
}
