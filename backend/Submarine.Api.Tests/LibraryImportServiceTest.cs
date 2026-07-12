using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Models.Request;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.Library;
using Submarine.Metadata.Contracts;
using Xunit;
using MetadataProvider = Submarine.Core.Library.MetadataProvider;

namespace Submarine.Api.Tests;

public class LibraryImportServiceTest : DatabaseTestBase
{
	private static SeriesResource SeriesResource(int tvdbId, string title)
		=> new(tvdbId, null, title, title, null, null, Submarine.Metadata.Contracts.SeriesStatus.Continuing, null, null,
			Array.Empty<string>(),
			new[] { new SeasonResource(1, null, 1) },
			new[]
			{
				new EpisodeResource(11, null, "Pilot", null, null, null,
					new[] { new EpisodeNumber(EpisodeOrdering.Aired, 1, 1, null) })
			},
			null, 2020);

	private LibraryImportService BuildService(FakeMetadataClient metadata)
	{
		var seriesService = new SeriesService(new SeriesRepository(Context), new RootFolderRepository(Context),
			metadata, new FakeBackgroundTaskQueue());
		var movieService = new MovieService(new MovieRepository(Context), new RootFolderRepository(Context), metadata,
			new FakeBackgroundTaskQueue());

		return new LibraryImportService(Context, seriesService, movieService, metadata, ReleaseParser(),
			new HistoryService(Context), NullLogger<LibraryImportService>.Instance);
	}

	[Fact]
	public async Task ScanAsync_ShouldProposeMatchWithParsedEpisodes_WhenFolderHoldsEpisodes()
	{
		var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var folder = Path.Combine(root, "Show Name (2020)");
		Directory.CreateDirectory(folder);
		await File.WriteAllTextAsync(Path.Combine(folder, "Show.Name.S01E01.1080p.WEB-DL.x264-GROUP.mkv"), "video");

		try
		{
			var metadata = new FakeMetadataClient();
			metadata.SeriesSearchResults.Add(SeriesResource(42, "Show Name"));

			var service = BuildService(metadata);

			var proposals = await service.ScanAsync(new LibraryImportScanRequest
			{
				Path = root, MediaKind = MediaKind.SERIES
			});

			var proposal = Assert.Single(proposals);
			Assert.Equal(42, proposal.SuggestedTvdbId);
			Assert.Equal("Show Name", proposal.SuggestedTitle);

			var file = Assert.Single(proposal.Files);
			Assert.Equal(1, file.Season);
			Assert.Contains(1, file.Episodes);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportAsync_ShouldRegisterFilesInPlace_WhenAdoptingSeriesFolder()
	{
		var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var folder = Path.Combine(root, "Show");
		Directory.CreateDirectory(folder);
		var fileName = "Show.S01E01.1080p.WEB-DL.x264-GROUP.mkv";
		var filePath = Path.Combine(folder, fileName);
		await File.WriteAllTextAsync(filePath, "video");

		try
		{
			var metadata = new FakeMetadataClient { Series = SeriesResource(42, "Show") };
			var service = BuildService(metadata);

			var response = await service.ImportAsync(new LibraryImportRequest
			{
				MediaKind = MediaKind.SERIES,
				Items = new List<LibraryImportItem>
				{
					new()
					{
						Folder = folder, TvdbId = 42, QualityProfileId = 1, LanguageProfileId = 1
					}
				}
			});

			var result = Assert.Single(response.Results);
			Assert.True(result.Success);

			// file must not have moved
			Assert.True(File.Exists(filePath));

			var episodeFile = await Context.EpisodeFiles.SingleAsync();
			Assert.Equal(fileName, episodeFile.RelativePath);

			var episode = await Context.Episodes.Include(e => e.Files).SingleAsync();
			Assert.Contains(episode.Files, f => f.Id == episodeFile.Id);

			var version = await Context.Versions.SingleAsync();
			Assert.Equal(folder, version.Path);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}
}
