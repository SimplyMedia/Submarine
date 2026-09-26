using Microsoft.Extensions.Logging;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.Metadata;

/// <summary>
///     Resolves episode screenshot urls from the Submarine.Metadata service. Series and movies already carry their
///     own poster/backdrop urls; episodes do not, so metadata consumers that need an episode thumbnail look it up
///     live through this resolver.
/// </summary>
public interface IMetadataClientImageResolver
{
	/// <summary>The screenshot url of the first episode in the file, or null when unavailable.</summary>
	Task<string?> ResolveEpisodeImageUrlAsync(Series series, IReadOnlyList<Episode> episodes, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class MetadataClientImageResolver(IMetadataClient metadataClient, ILogger<MetadataClientImageResolver> logger) : IMetadataClientImageResolver
{
	/// <inheritdoc />
	public async Task<string?> ResolveEpisodeImageUrlAsync(Series series, IReadOnlyList<Episode> episodes, CancellationToken cancellationToken)
	{
		if (episodes.Count == 0)
		{
			return null;
		}

		try
		{
			var resource = await metadataClient.GetSeriesByTvdbAsync(series.TvdbId, cancellationToken);
			if (resource is null)
			{
				return null;
			}

			var episode = episodes[0];
			return resource.Episodes.FirstOrDefault(x => episode.TvdbId is { } tvdbId && x.TvdbId == tvdbId)?.ImageUrl;
		}
		catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
		{
			logger.LogWarning(exception, "Unable to resolve episode image for series {TvdbId}", series.TvdbId);
			return null;
		}
	}
}
