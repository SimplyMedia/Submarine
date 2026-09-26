using Submarine.Core.Entities;

namespace Submarine.Infrastructure.MediaFiles;

/// <summary>
///     Extracts technical media info from a video file using ffprobe.
/// </summary>
public interface IMediaInfoService
{
	/// <summary>
	///     Probe a file. Returns null when ffprobe is not installed, the file could not be read, or probing failed.
	/// </summary>
	Task<MediaInfoModel?> ProbeAsync(string filePath, CancellationToken cancellationToken = default);
}
