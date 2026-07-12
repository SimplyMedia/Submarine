using Submarine.Api.Clients;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Request;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.Notification;
using Xunit;

namespace Submarine.Api.Tests;

public class ConnectionServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task CreateAsync_ShouldThrowBadRequest_WhenPlexConnectionHasNoHost()
	{
		var service = BuildService();

		await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(new CreateConnectionRequest
		{
			Name = "plex", Type = ConnectionType.PLEX, Port = 32400, ApiKey = "token"
		}));
	}

	[Fact]
	public async Task GetAsync_ShouldLoadDiscordConnection_WhenHostAndApiKeyAreNull()
	{
		Context.Connections.Add(new DiscordConnection
		{
			Name = "discord", Enable = true, WebhookUrl = "https://discord.example/webhook", Tags = new List<string>()
		});
		await Context.SaveChangesAsync();
		Context.ChangeTracker.Clear();

		var service = BuildService();

		var connection = await service.GetAsync(1);

		var discord = Assert.IsType<DiscordConnection>(connection);
		Assert.Null(discord.Host);
		Assert.Null(discord.ApiKey);
		Assert.Equal("https://discord.example/webhook", discord.WebhookUrl);
	}

	private ConnectionService BuildService()
		=> new(new ConnectionRepository(Context), new FakeMediaServerClientFactory(),
			new NotificationSenderFactory(new FakeHttpClientFactory(new StubHttpMessageHandler(
				System.Net.HttpStatusCode.OK, "{}"))));
}
