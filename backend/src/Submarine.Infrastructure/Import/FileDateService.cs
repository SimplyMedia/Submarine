using Microsoft.Extensions.Logging;
using Submarine.Core.Entities;
using Submarine.Core.Enums;

namespace Submarine.Infrastructure.Import;

/// <summary>
///     Applies the configured <see cref="FileDate" /> to a media file's last write timestamp, mirroring the file
///     date on disk to the episode air date or movie release date so media servers sort by it.
/// </summary>
public interface IFileDateService
{
	/// <summary>Applies the file date to an episode file, a no-op for movie only settings or missing dates.</summary>
	bool ApplyEpisodeFileDate(string fullPath, FileDate fileDate, Episode episode);

	/// <summary>Applies the file date to a movie file, a no-op for episode only settings or missing dates.</summary>
	bool ApplyMovieFileDate(string fullPath, FileDate fileDate, Movie movie);
}

/// <inheritdoc />
public sealed class FileDateService(ILogger<FileDateService> logger) : IFileDateService
{
	/// <inheritdoc />
	public bool ApplyEpisodeFileDate(string fullPath, FileDate fileDate, Episode episode)
	{
		DateTime? date = fileDate switch
		{
			FileDate.LOCAL_AIR_DATE => DateTime.TryParse(episode.AirDate, out var airDate) ? airDate : null,
			FileDate.UTC_AIR_DATE => episode.AirDateUtc,
			_ => null
		};

		return date is { } value && TrySetLastWriteTime(fullPath, value);
	}

	/// <inheritdoc />
	public bool ApplyMovieFileDate(string fullPath, FileDate fileDate, Movie movie)
	{
		DateTime? date = fileDate switch
		{
			FileDate.IN_CINEMAS => movie.InCinemasDate,
			FileDate.RELEASE => movie.PhysicalReleaseDate ?? movie.DigitalReleaseDate,
			_ => null
		};

		return date is { } value && TrySetLastWriteTime(fullPath, value);
	}

	private bool TrySetLastWriteTime(string fullPath, DateTime date)
	{
		try
		{
			if (!File.Exists(fullPath))
			{
				return false;
			}

			if (File.GetLastWriteTimeUtc(fullPath) == date)
			{
				return false;
			}

			File.SetLastWriteTimeUtc(fullPath, date);
			return true;
		}
		catch (IOException exception)
		{
			logger.LogWarning(exception, "Unable to set file date of {Path}", fullPath);
			return false;
		}
	}
}
