using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Indexers;
using Submarine.Core.Provider;
using Xunit;

namespace Submarine.Api.IntegrationTests.Indexers;

public sealed class BrokenIndexerSearchTests
{
	[Fact]
	public async Task Search_ShouldSkipAnIndexerWhoseStoredSettingsAreInvalid()
	{
		await using var factory = new SubmarineApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		await factory.WithDbAsync(async db =>
		{
			// Settings that no longer validate, e.g. written by an older version or edited by hand.
			db.Indexers.Add(new Indexer { Name = "Broken", Implementation = IndexerImplementation.TORZNAB, Protocol = Protocol.BITTORRENT, SettingsJson = "{}" });
			return await db.SaveChangesAsync();
		});

		var response = await client.GetAsync("/api/v1/search?term=anything");

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		(await response.Content.ReadFromJsonAsync<List<JsonElement>>())!.ShouldBeEmpty();
	}
}
