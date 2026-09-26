using Microsoft.Extensions.Logging;

namespace Submarine.Infrastructure.Metadata;

/// <summary>
///     Downloads companion images returned by metadata consumers to disk.
/// </summary>
public interface IMetadataConsumerImageDownloader
{
	/// <summary>Downloads every image, relative to the version folder, best effort.</summary>
	Task DownloadAsync(string versionFolder, IReadOnlyList<MetadataImageResult> images, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class MetadataConsumerImageDownloader(IHttpClientFactory httpClientFactory, ILogger<MetadataConsumerImageDownloader> logger) : IMetadataConsumerImageDownloader
{
	/// <summary>Name of the shared http client used to download metadata consumer images.</summary>
	public const string HttpClientName = "metadata-consumer-images";

	/// <inheritdoc />
	public async Task DownloadAsync(string versionFolder, IReadOnlyList<MetadataImageResult> images, CancellationToken cancellationToken)
	{
		if (images.Count == 0)
		{
			return;
		}

		var http = httpClientFactory.CreateClient(HttpClientName);
		foreach (var image in images)
		{
			try
			{
				var bytes = await http.GetByteArrayAsync(image.SourceUrl, cancellationToken);
				var path = Path.Combine(versionFolder, image.RelativePath);
				var directory = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(directory))
				{
					Directory.CreateDirectory(directory);
				}

				await File.WriteAllBytesAsync(path, bytes, cancellationToken);
			}
			catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
			{
				logger.LogWarning(exception, "Unable to download metadata image {RelativePath} from {SourceUrl}", image.RelativePath, image.SourceUrl);
			}
		}
	}
}
