using Microsoft.AspNetCore.SignalR;

namespace Submarine.Api.Features.Compat.Shared.Realtime;

public sealed class SonarrCompatHub : Hub { }

public sealed class RadarrCompatHub : Hub { }

public interface ISonarrCompatRealtimeProjector
{
	Task<object?> ProjectSeriesAsync(int seriesId, CancellationToken cancellationToken);
	Task<object?> ProjectEpisodeAsync(int episodeId, CancellationToken cancellationToken);
}

public interface IRadarrCompatRealtimeProjector
{
	Task<object?> ProjectMovieAsync(int movieId, CancellationToken cancellationToken);
}

public interface ICompatRealtimePublisher
{
	Task PublishSonarrAsync(string resourceName, string action, object resource, CancellationToken cancellationToken = default);
	Task PublishRadarrAsync(string resourceName, string action, object resource, CancellationToken cancellationToken = default);
}

public sealed class CompatRealtimePublisher(
	IHubContext<SonarrCompatHub> sonarrHub,
	IHubContext<RadarrCompatHub> radarrHub) : ICompatRealtimePublisher
{
	public Task PublishSonarrAsync(string resourceName, string action, object resource, CancellationToken cancellationToken = default)
		=> SendAsync(sonarrHub, resourceName, action, resource, cancellationToken);

	public Task PublishRadarrAsync(string resourceName, string action, object resource, CancellationToken cancellationToken = default)
		=> SendAsync(radarrHub, resourceName, action, resource, cancellationToken);

	private static Task SendAsync<T>(IHubContext<T> hub, string resourceName, string action, object resource, CancellationToken cancellationToken)
		where T : Hub
	{
		if (resourceName is not ("series" or "episode" or "movie"))
			throw new ArgumentOutOfRangeException(nameof(resourceName));
		if (action is not ("updated" or "deleted"))
			throw new ArgumentOutOfRangeException(nameof(action));
		return hub.Clients.All.SendAsync("receiveMessage", new { name = resourceName, body = new { action, resource } }, cancellationToken);
	}
}
