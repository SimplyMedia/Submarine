using Submarine.Contracts.Metadata;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Library;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.IntegrationTests.Library;

/// <summary>
///     Shared fixtures for the library integration tests.
/// </summary>
public static class LibraryTestSupport
{
	public const int TvdbId = 107151;
	public const int SecondTvdbId = 73141;
	public const int TmdbId = 100;
	public const int SecondTmdbId = 200;
	public const int CollectionId = 999;

	/// <summary>Register the series fixture: 3 seasons (0 specials, 1, 2) and 6 episodes.</summary>
	public static SeriesResource SeriesFixture(int tvdbId, string title, DateTime now)
	{
		DateOnly Past(int days) => DateOnly.FromDateTime(now.AddDays(-days));
		DateOnly Future(int days) => DateOnly.FromDateTime(now.AddDays(days));
		return new SeriesResource(
			tvdbId,
			tvdbId + 100000,
			$"tt{tvdbId}",
			title,
			null,
			"An integration test series",
			Past(400),
			Submarine.Contracts.Metadata.SeriesStatus.CONTINUING,
			42,
			"Test Network",
			["Drama"],
			"TV-14",
			[new SeasonResource(0, "Specials", 1), new SeasonResource(1, "Season 1", 3), new SeasonResource(2, "Season 2", 2)],
			[
				Episode(0, 1, 1, Past(500)),
				Episode(1, 1, 1, Past(400)),
				Episode(1, 2, 2, Past(393)),
				Episode(1, 3, 3, Past(386)),
				Episode(2, 1, 4, Future(7)),
				Episode(2, 2, 5, Future(14))
			],
			"https://example.com/poster.jpg",
			"https://example.com/backdrop.jpg",
			Past(400).Year,
			[new AlternateTitleResource("Alternate Series Name", "eng")]);
	}

	private static EpisodeResource Episode(int season, int number, int absolute, DateOnly airDate)
		=> new(
			season * 10000 + number,
			null,
			$"Episode S{season}E{number}",
			$"Overview of S{season}E{number}",
			airDate,
			airDate,
			42,
			[new EpisodeNumber(EpisodeOrdering.AIRED, season, number, absolute)],
			null);

	/// <summary>Register the movie fixtures.</summary>
	public static void MovieFixtures(StubMetadataClient metadata, DateTime now)
	{
		metadata.Movies[TmdbId] = new MovieResource(
			TmdbId,
			"tt0100",
			"Test Movie",
			"Test Movie Original",
			null,
			"A test movie",
			DateOnly.FromDateTime(now.AddDays(-30)),
			DateOnly.FromDateTime(now.AddDays(-10)),
			DateOnly.FromDateTime(now.AddDays(-5)),
			Submarine.Contracts.Metadata.MovieStatus.RELEASED,
			now.Year,
			120,
			["Action"],
			"Studio",
			"PG-13",
			"https://example.com/movie.jpg",
			null,
			"abc123",
			CollectionId,
			"Test Collection",
			[]);
		metadata.Movies[SecondTmdbId] = new MovieResource(
			SecondTmdbId,
			"tt0200",
			"Second Movie",
			null,
			null,
			"Another movie in the collection",
			DateOnly.FromDateTime(now.AddDays(-20)),
			DateOnly.FromDateTime(now.AddDays(-2)),
			null,
			Submarine.Contracts.Metadata.MovieStatus.RELEASED,
			now.Year,
			100,
			[],
			"Studio",
			null,
			"https://example.com/movie2.jpg",
			null,
			null,
			CollectionId,
			"Test Collection",
			[]);
		metadata.Collections[CollectionId] = new CollectionResource(
			CollectionId,
			"Test Collection",
			"A collection",
			"https://example.com/collection.jpg",
			[metadata.Movies[TmdbId], metadata.Movies[SecondTmdbId]]);
	}

	/// <summary>Create a temporary root folder on disk.</summary>
	public static string CreateTempRoot()
	{
		var path = Path.Combine(Path.GetTempPath(), $"submarine-it-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}

	/// <summary>Create a series root folder via the API.</summary>
	public static async Task<int> CreateRootFolderAsync(HttpClient client, string path, string mediaKind = "SERIES")
	{
		var response = await client.PostAsJsonAsync("/api/v1/root-folders", new { path, mediaKind });
		response.EnsureSuccessStatusCode();
		var folder = await response.Content.ReadFromJsonAsync<RootFolderDto>();
		return folder!.Id;
	}

	/// <summary>Add the series fixture to the library.</summary>
	public static async Task<int> AddSeriesAsync(LibraryApiFactory factory, HttpClient client, int rootFolderId, int tvdbId = TvdbId, string title = "Test Series")
	{
		if (!factory.Metadata.Series.ContainsKey(tvdbId))
		{
			factory.Metadata.Series[tvdbId] = SeriesFixture(tvdbId, title, DateTime.UtcNow);
		}

		var response = await client.PostAsJsonAsync("/api/v1/series", new
		{
			tvdbId,
			metadataProvider = "TVDB",
			rootFolderId,
			monitorOption = "ALL",
			monitorSpecials = false,
			versions = new[] { new { name = "main", qualityProfileId = 1, languageProfileId = 1 } }
		});
		response.EnsureSuccessStatusCode();
		var detail = await response.Content.ReadFromJsonAsync<SeriesDetailDto>();
		return detail!.Series.Id;
	}

	/// <summary>Find the episode id of S01E01 of a series.</summary>
	public static async Task<int> EpisodeIdAsync(HttpClient client, int seriesId, int season, int episode)
	{
		var episodes = await client.GetFromJsonAsync<List<EpisodeDto>>(
			$"/api/v1/episodes?seriesId={seriesId}");
		return episodes!.Single(x => x.SeasonNumber == season && x.EpisodeNumber == episode).Id;
	}

	/// <summary>Fake command execution context for direct handler calls.</summary>
	public sealed class FakeCommandContext : ICommandContext
	{
		/// <inheritdoc />
		public int CommandId => 0;

		/// <inheritdoc />
		public Task ReportProgressAsync(int percent, string? message = null, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;
	}

	/// <summary>Run a command handler directly, since hosted services do not run under the test server.</summary>
	public static async Task RunHandlerAsync<TCommand>(LibraryApiFactory factory, TCommand command)
		where TCommand : ICommand
	{
		await using var scope = factory.Services.CreateAsyncScope();
		var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<TCommand>>();
		await handler.ExecuteAsync(command, new FakeCommandContext());
	}
}

/// <summary>Minimal root folder response for tests.</summary>
/// <param name="Id">Id.</param>
/// <param name="Path">Path.</param>
public sealed record RootFolderDto(int Id, string Path);

/// <summary>Minimal series detail response for tests.</summary>
/// <param name="Series">List item part.</param>
public sealed record SeriesDetailDto(SeriesListItemDto Series);

/// <summary>Minimal series list item response for tests.</summary>
/// <param name="Id">Id.</param>
/// <param name="TvdbId">TVDB id.</param>
/// <param name="Title">Title.</param>
/// <param name="Monitored">Monitored.</param>
/// <param name="TagIds">Tag ids.</param>
/// <param name="Versions">Versions.</param>
/// <param name="Statistics">Statistics.</param>
public sealed record SeriesListItemDto(
	int Id,
	int TvdbId,
	string Title,
	bool Monitored,
	List<int> TagIds,
	List<VersionDto> Versions,
	StatisticsDto Statistics);

/// <summary>Minimal version response for tests.</summary>
/// <param name="Id">Id.</param>
/// <param name="Name">Name.</param>
/// <param name="RootFolderId">Root folder id.</param>
/// <param name="Path">Path.</param>
public sealed record VersionDto(int Id, string Name, int RootFolderId, string Path);

/// <summary>Minimal statistics response for tests.</summary>
/// <param name="EpisodeCount">Monitored episodes.</param>
/// <param name="EpisodeFileCount">Monitored episodes with file.</param>
/// <param name="TotalEpisodeCount">All episodes.</param>
public sealed record StatisticsDto(int EpisodeCount, int EpisodeFileCount, int TotalEpisodeCount);

/// <summary>Minimal episode response for tests.</summary>
/// <param name="Id">Id.</param>
/// <param name="SeasonNumber">Season.</param>
/// <param name="EpisodeNumber">Episode.</param>
/// <param name="Title">Title.</param>
/// <param name="Monitored">Monitored.</param>
/// <param name="HasFile">Has file.</param>
public sealed record EpisodeDto(int Id, int SeasonNumber, int EpisodeNumber, string? Title, bool Monitored, bool HasFile);

/// <summary>Minimal wanted item response for tests.</summary>
/// <param name="Type">Episode or movie.</param>
/// <param name="Id">Item id.</param>
/// <param name="Title">Title.</param>
public sealed record WantedItemDto(string Type, int Id, string Title);

/// <summary>Minimal calendar event response for tests.</summary>
/// <param name="Type">Episode or movie.</param>
/// <param name="Id">Id.</param>
/// <param name="Title">Title.</param>
/// <param name="Kind">Kind.</param>
/// <param name="HasFile">Whether the item has a file on disk.</param>
/// <param name="Downloading">Whether a tracked download is in progress or pending import.</param>
public sealed record CalendarEventDto(string Type, int Id, string Title, string Kind, bool HasFile, bool Downloading);

/// <summary>Minimal tag detail response for tests.</summary>
/// <param name="Id">Tag id.</param>
/// <param name="SeriesIds">Series using the tag.</param>
public sealed record TagDetailDto(int Id, List<int> SeriesIds);

/// <summary>Minimal collection response for tests.</summary>
/// <param name="Id">Row id.</param>
/// <param name="TmdbCollectionId">TMDB id.</param>
/// <param name="Title">Title.</param>
/// <param name="Monitored">Whether monitored.</param>
/// <param name="RootFolderId">Root folder for new movies.</param>
/// <param name="QualityProfileId">Quality profile for new movies.</param>
/// <param name="LanguageProfileId">Language profile for new movies.</param>
/// <param name="MinimumAvailability">Earliest availability before grabbing.</param>
/// <param name="MovieCount">Movies in library.</param>
public sealed record CollectionDto(
	int? Id,
	int TmdbCollectionId,
	string Title,
	bool Monitored,
	int? RootFolderId,
	int? QualityProfileId,
	int? LanguageProfileId,
	string MinimumAvailability,
	int MovieCount);
