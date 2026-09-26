using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Import;

/// <summary>
///     Applying the configured <see cref="FileDate" /> to a file's modified timestamp.
/// </summary>
public sealed class FileDateServiceTests : IDisposable
{
	private readonly string _tempDir = Directory.CreateTempSubdirectory("submarine-filedate-").FullName;
	private readonly FileDateService _service = new(NullLogger<FileDateService>.Instance);

	public void Dispose() => Directory.Delete(_tempDir, recursive: true);

	[Fact]
	public void ApplyEpisodeFileDate_ShouldSetUtcAirDate()
	{
		var path = Path.Combine(_tempDir, "episode.mkv");
		File.WriteAllText(path, "video");
		var episode = new Episode { AirDateUtc = new DateTime(2020, 5, 1, 20, 0, 0, DateTimeKind.Utc) };

		var changed = _service.ApplyEpisodeFileDate(path, FileDate.UTC_AIR_DATE, episode);

		changed.ShouldBeTrue();
		File.GetLastWriteTimeUtc(path).ShouldBe(episode.AirDateUtc.Value, TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void ApplyEpisodeFileDate_ShouldSetLocalAirDate_FromAirDateString()
	{
		var path = Path.Combine(_tempDir, "episode.mkv");
		File.WriteAllText(path, "video");
		var episode = new Episode { AirDate = "2019-03-04" };

		var changed = _service.ApplyEpisodeFileDate(path, FileDate.LOCAL_AIR_DATE, episode);

		changed.ShouldBeTrue();
		File.GetLastWriteTimeUtc(path).Date.ShouldBe(new DateTime(2019, 3, 4));
	}

	[Fact]
	public void ApplyEpisodeFileDate_ShouldNoOp_WhenNone()
	{
		var path = Path.Combine(_tempDir, "episode.mkv");
		File.WriteAllText(path, "video");
		var episode = new Episode { AirDate = "2019-03-04", AirDateUtc = DateTime.UtcNow };

		_service.ApplyEpisodeFileDate(path, FileDate.NONE, episode).ShouldBeFalse();
	}

	[Fact]
	public void ApplyEpisodeFileDate_ShouldNoOp_WhenMovieOnlySetting()
	{
		var path = Path.Combine(_tempDir, "episode.mkv");
		File.WriteAllText(path, "video");
		var episode = new Episode { AirDate = "2019-03-04" };

		_service.ApplyEpisodeFileDate(path, FileDate.IN_CINEMAS, episode).ShouldBeFalse();
	}

	[Fact]
	public void ApplyMovieFileDate_ShouldPreferPhysicalRelease_OverDigitalRelease()
	{
		var path = Path.Combine(_tempDir, "movie.mkv");
		File.WriteAllText(path, "video");
		var movie = new Movie
		{
			PhysicalReleaseDate = new DateTime(2021, 6, 15),
			DigitalReleaseDate = new DateTime(2021, 5, 1)
		};

		_service.ApplyMovieFileDate(path, FileDate.RELEASE, movie).ShouldBeTrue();
		File.GetLastWriteTimeUtc(path).ShouldBe(movie.PhysicalReleaseDate.Value, TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void ApplyMovieFileDate_ShouldFallBackToDigitalRelease_WhenNoPhysicalRelease()
	{
		var path = Path.Combine(_tempDir, "movie.mkv");
		File.WriteAllText(path, "video");
		var movie = new Movie { DigitalReleaseDate = new DateTime(2021, 5, 1) };

		_service.ApplyMovieFileDate(path, FileDate.RELEASE, movie).ShouldBeTrue();
		File.GetLastWriteTimeUtc(path).ShouldBe(movie.DigitalReleaseDate.Value, TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void ApplyMovieFileDate_ShouldSetInCinemasDate()
	{
		var path = Path.Combine(_tempDir, "movie.mkv");
		File.WriteAllText(path, "video");
		var movie = new Movie { InCinemasDate = new DateTime(2021, 1, 1) };

		_service.ApplyMovieFileDate(path, FileDate.IN_CINEMAS, movie).ShouldBeTrue();
		File.GetLastWriteTimeUtc(path).ShouldBe(movie.InCinemasDate.Value, TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void Apply_ShouldReturnFalse_WhenFileMissing()
	{
		var path = Path.Combine(_tempDir, "missing.mkv");
		var episode = new Episode { AirDateUtc = DateTime.UtcNow };

		_service.ApplyEpisodeFileDate(path, FileDate.UTC_AIR_DATE, episode).ShouldBeFalse();
	}
}
