using Microsoft.Extensions.Logging.Abstractions;
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

	[Theory]
	[MemberData(nameof(NewConnectionTypeCreateRequests))]
	public async Task CreateAsync_ShouldCreateConnection_WhenRequiredFieldsProvided(CreateConnectionRequest request,
		Type expectedType)
	{
		var service = BuildService();

		var connection = await service.CreateAsync(request);

		Assert.IsType(expectedType, connection);
	}

	[Theory]
	[MemberData(nameof(NewConnectionTypeInvalidRequests))]
	public async Task CreateAsync_ShouldThrowBadRequest_WhenRequiredFieldsMissing(CreateConnectionRequest request)
	{
		var service = BuildService();

		await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(request));
	}

	public static IEnumerable<object[]> NewConnectionTypeCreateRequests()
	{
		var scriptPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

		yield return new object[]
		{
			new CreateConnectionRequest { Name = "slack", Type = ConnectionType.SLACK, WebhookUrl = "https://hooks.slack.com/x" },
			typeof(SlackConnection)
		};
		yield return new object[]
		{
			new CreateConnectionRequest { Name = "pushover", Type = ConnectionType.PUSHOVER, AppToken = "app", UserKey = "user" },
			typeof(PushoverConnection)
		};
		yield return new object[]
		{
			new CreateConnectionRequest { Name = "pushbullet", Type = ConnectionType.PUSHBULLET, AccessToken = "token" },
			typeof(PushbulletConnection)
		};
		yield return new object[]
		{
			new CreateConnectionRequest
			{
				Name = "gotify", Type = ConnectionType.GOTIFY, ServerUrl = "https://gotify.example", AppToken = "token"
			},
			typeof(GotifyConnection)
		};
		yield return new object[]
		{
			new CreateConnectionRequest { Name = "kodi", Type = ConnectionType.KODI, Host = "localhost", Port = 8080 },
			typeof(KodiConnection)
		};
		yield return new object[]
		{
			new CreateConnectionRequest { Name = "script", Type = ConnectionType.CUSTOM_SCRIPT, ScriptPath = scriptPath },
			typeof(CustomScriptConnection)
		};
	}

	public static IEnumerable<object[]> NewConnectionTypeInvalidRequests()
	{
		yield return new object[] { new CreateConnectionRequest { Name = "slack", Type = ConnectionType.SLACK } };
		yield return new object[]
			{ new CreateConnectionRequest { Name = "pushover", Type = ConnectionType.PUSHOVER, AppToken = "app" } };
		yield return new object[] { new CreateConnectionRequest { Name = "pushbullet", Type = ConnectionType.PUSHBULLET } };
		yield return new object[]
			{ new CreateConnectionRequest { Name = "gotify", Type = ConnectionType.GOTIFY, AppToken = "token" } };
		yield return new object[] { new CreateConnectionRequest { Name = "kodi", Type = ConnectionType.KODI } };
		yield return new object[]
		{
			new CreateConnectionRequest
			{
				Name = "script", Type = ConnectionType.CUSTOM_SCRIPT, ScriptPath = "does-not-exist.sh"
			}
		};
	}

	private ConnectionService BuildService()
		=> new(new ConnectionRepository(Context), new FakeMediaServerClientFactory(),
			new NotificationSenderFactory(new FakeHttpClientFactory(new StubHttpMessageHandler(
				System.Net.HttpStatusCode.OK, "{}")), NullLoggerFactory.Instance));
}
