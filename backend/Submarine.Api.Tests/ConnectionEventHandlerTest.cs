using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Clients;
using Submarine.Api.Events;
using Submarine.Core.Library;
using Submarine.Core.Notification;
using Xunit;

namespace Submarine.Api.Tests;

/// <summary>
///     Records notified paths per connection instead of talking to a media server
/// </summary>
public sealed class FakeMediaServerClientFactory : IMediaServerClientFactory
{
	public List<(string Connection, string Path)> Notified { get; } = new();

	public IMediaServerClient Create(Connection connection)
		=> new FakeMediaServerClient(connection.Name, Notified);

	private sealed class FakeMediaServerClient : IMediaServerClient
	{
		private readonly string _connection;
		private readonly List<(string, string)> _notified;

		public FakeMediaServerClient(string connection, List<(string, string)> notified)
		{
			_connection = connection;
			_notified = notified;
		}

		public Task NotifyMediaUpdatedAsync(string path, CancellationToken cancellationToken = default)
		{
			_notified.Add((_connection, path));

			return Task.CompletedTask;
		}

		public Task TestAsync(CancellationToken cancellationToken = default)
			=> Task.CompletedTask;
	}
}

/// <summary>
///     Records requests and returns a fixed response instead of calling a real notification target
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
	private readonly HttpStatusCode _status;
	private readonly string _content;

	public List<HttpRequestMessage> Requests { get; } = new();

	public StubHttpMessageHandler(HttpStatusCode status, string content)
	{
		_status = status;
		_content = content;
	}

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		Requests.Add(request);

		return Task.FromResult(new HttpResponseMessage(_status)
		{
			Content = new StringContent(_content, Encoding.UTF8, "application/json")
		});
	}
}

/// <summary>
///     Hands out a fixed <see cref="HttpClient" /> wrapping a <see cref="StubHttpMessageHandler" />
/// </summary>
internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
	private readonly HttpClient _client;

	public FakeHttpClientFactory(HttpMessageHandler handler)
		=> _client = new HttpClient(handler);

	public HttpClient CreateClient(string name)
		=> _client;
}

public class ConnectionEventHandlerTest : DatabaseTestBase
{
	[Fact]
	public async Task HandleAsync_ShouldNotifyOnlyEnabledConnectionsWithToggle_WhenMediaImported()
	{
		Context.Connections.AddRange(
			NewConnection("notified", enable: true, onImport: true),
			NewConnection("disabled", enable: false, onImport: true),
			NewConnection("toggled-off", enable: true, onImport: false));
		await Context.SaveChangesAsync();

		var factory = new FakeMediaServerClientFactory();
		var handler = new ConnectionEventHandler(Context, factory, NewNotificationSenderFactory(out _),
			NullLogger<ConnectionEventHandler>.Instance);

		await handler.HandleAsync(new MediaImportedEvent(null, null, "/library/show", "Show"),
			CancellationToken.None);

		var notified = Assert.Single(factory.Notified);
		Assert.Equal(("notified", "/library/show"), notified);
	}

	[Fact]
	public async Task HandleAsync_ShouldFilterByTags_WhenConnectionAndMediaAreTagged()
	{
		Context.Movies.Add(new Movie
		{
			TmdbId = 1, Title = "Movie", Tags = new List<string> { "anime" }
		});
		Context.Connections.AddRange(
			NewConnection("matching-tag", enable: true, onImport: true, tags: new List<string> { "anime" }),
			NewConnection("other-tag", enable: true, onImport: true, tags: new List<string> { "kids" }),
			NewConnection("untagged", enable: true, onImport: true));
		await Context.SaveChangesAsync();

		var factory = new FakeMediaServerClientFactory();
		var handler = new ConnectionEventHandler(Context, factory, NewNotificationSenderFactory(out _),
			NullLogger<ConnectionEventHandler>.Instance);

		await handler.HandleAsync(new MediaImportedEvent(null, 1, "/movies/movie", "Movie"),
			CancellationToken.None);

		Assert.Equal(new[] { "matching-tag", "untagged" },
			factory.Notified.Select(n => n.Connection).OrderBy(n => n).ToArray());
	}

	[Fact]
	public async Task HandleAsync_ShouldPostDiscordEmbed_WhenMediaImported()
	{
		Context.Connections.Add(new DiscordConnection
		{
			Name = "discord", Enable = true, OnImport = true, WebhookUrl = "https://discord.example/webhook",
			Tags = new List<string>()
		});
		await Context.SaveChangesAsync();

		var handler = new ConnectionEventHandler(Context, new FakeMediaServerClientFactory(),
			NewNotificationSenderFactory(out var stub), NullLogger<ConnectionEventHandler>.Instance);

		await handler.HandleAsync(new MediaImportedEvent(null, null, "/movies/movie", "Movie Title"),
			CancellationToken.None);

		var request = Assert.Single(stub.Requests);
		Assert.Equal("https://discord.example/webhook", request.RequestUri!.ToString());

		var body = await request.Content!.ReadAsStringAsync();
		Assert.Contains("\"title\":\"Imported: Movie Title\"", body);
	}

	[Fact]
	public async Task HandleAsync_ShouldSendTelegramMessage_WhenMediaGrabbed()
	{
		Context.Connections.Add(new TelegramConnection
		{
			Name = "telegram", Enable = true, OnGrab = true, BotToken = "token123", ChatId = "chat-1",
			Tags = new List<string>()
		});
		await Context.SaveChangesAsync();

		var handler = new ConnectionEventHandler(Context, new FakeMediaServerClientFactory(),
			NewNotificationSenderFactory(out var stub), NullLogger<ConnectionEventHandler>.Instance);

		await handler.HandleAsync(new MediaGrabbedEvent(null, null, "/movies/movie", "Movie Title"),
			CancellationToken.None);

		var request = Assert.Single(stub.Requests);
		Assert.Equal("https://api.telegram.org/bottoken123/sendMessage", request.RequestUri!.ToString());

		var body = await request.Content!.ReadAsStringAsync();
		Assert.Contains("\"chat_id\":\"chat-1\"", body);
		Assert.Contains("\"text\":\"Grabbed: Movie Title\"", body);
	}

	[Fact]
	public async Task HandleAsync_ShouldSendBasicAuthHeader_WhenWebhookConnectionHasCredentials()
	{
		Context.Connections.Add(new WebhookConnection
		{
			Name = "webhook", Enable = true, OnRename = true, Url = "https://hooks.example/notify", Method = "PUT",
			Username = "user", Password = "pass", Tags = new List<string>()
		});
		await Context.SaveChangesAsync();

		var handler = new ConnectionEventHandler(Context, new FakeMediaServerClientFactory(),
			NewNotificationSenderFactory(out var stub), NullLogger<ConnectionEventHandler>.Instance);

		await handler.HandleAsync(new MediaRenamedEvent(null, null, "/movies/movie", "Movie Title"),
			CancellationToken.None);

		var request = Assert.Single(stub.Requests);
		Assert.Equal(HttpMethod.Put, request.Method);
		Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
		Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("user:pass")),
			request.Headers.Authorization.Parameter);
	}

	[Fact]
	public async Task HandleAsync_ShouldFilterNotificationConnectionsByTags_WhenMediaImported()
	{
		Context.Movies.Add(new Movie
		{
			TmdbId = 1, Title = "Movie", Tags = new List<string> { "anime" }
		});
		Context.Connections.AddRange(
			new DiscordConnection
			{
				Name = "matching", Enable = true, OnImport = true, WebhookUrl = "https://discord.example/a",
				Tags = new List<string> { "anime" }
			},
			new DiscordConnection
			{
				Name = "other", Enable = true, OnImport = true, WebhookUrl = "https://discord.example/b",
				Tags = new List<string> { "kids" }
			});
		await Context.SaveChangesAsync();

		var handler = new ConnectionEventHandler(Context, new FakeMediaServerClientFactory(),
			NewNotificationSenderFactory(out var stub), NullLogger<ConnectionEventHandler>.Instance);

		await handler.HandleAsync(new MediaImportedEvent(null, 1, "/movies/movie", "Movie"),
			CancellationToken.None);

		var request = Assert.Single(stub.Requests);
		Assert.Equal("https://discord.example/a", request.RequestUri!.ToString());
	}

	private static INotificationSenderFactory NewNotificationSenderFactory(out StubHttpMessageHandler stub)
	{
		stub = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");

		return new NotificationSenderFactory(new FakeHttpClientFactory(stub));
	}

	private static Connection NewConnection(string name, bool enable, bool onImport, List<string>? tags = null)
		=> new()
		{
			Name = name,
			Type = ConnectionType.JELLYFIN,
			Enable = enable,
			Host = "localhost",
			Port = 8096,
			ApiKey = "key",
			OnImport = onImport,
			Tags = tags ?? new List<string>()
		};
}
