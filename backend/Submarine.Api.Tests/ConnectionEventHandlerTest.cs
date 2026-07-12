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
		var handler = new ConnectionEventHandler(Context, factory, NullLogger<ConnectionEventHandler>.Instance);

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
		var handler = new ConnectionEventHandler(Context, factory, NullLogger<ConnectionEventHandler>.Instance);

		await handler.HandleAsync(new MediaImportedEvent(null, 1, "/movies/movie", "Movie"),
			CancellationToken.None);

		Assert.Equal(new[] { "matching-tag", "untagged" },
			factory.Notified.Select(n => n.Connection).OrderBy(n => n).ToArray());
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
