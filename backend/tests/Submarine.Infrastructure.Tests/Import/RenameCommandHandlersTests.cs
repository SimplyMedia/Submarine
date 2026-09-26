using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Naming;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Import;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Infrastructure.Tests.Import;

public sealed class RenameCommandHandlersTests : IDisposable
{
	private readonly string _tempDir = Directory.CreateTempSubdirectory("submarine-rename-").FullName;
	private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
	private readonly SqliteSubmarineDbContext _db;
	private readonly IEventBus _eventBus = Substitute.For<IEventBus>();
	private readonly ICommandContext _context = Substitute.For<ICommandContext>();

	public RenameCommandHandlersTests()
	{
		_db = TestDbFactory.Create(_time);
	}

	public void Dispose()
	{
		_db.Dispose();
		Directory.Delete(_tempDir, recursive: true);
	}

	[Fact]
	public async Task ExecuteAsync_ShouldNotOverwriteDifferentExistingFile_WhenTargetNameAlreadyTaken()
	{
		var root = new RootFolder { Path = Path.Combine(_tempDir, "library"), MediaKind = MediaKind.SERIES };
		var quality = new QualityProfile { Name = "Q" };
		var language = new LanguageProfile { Name = "L" };
		Directory.CreateDirectory(root.Path);
		_db.RootFolders.Add(root);
		_db.QualityProfiles.Add(quality);
		_db.LanguageProfiles.Add(language);
		_db.NamingConfig.Add(new NamingConfig());
		_db.SaveChanges();

		var series = new Series { TvdbId = 1, Title = "Test Show", CleanTitle = "testshow", SeasonFolder = true };
		series.Episodes.Add(new Episode { SeasonNumber = 1, EpisodeNumber = 1, Title = "Pilot" });
		_db.Series.Add(series);
		_db.SaveChanges();
		var episode = series.Episodes.Single();

		var version = new MediaVersion { SeriesId = series.Id, Name = "main", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "Test Show" };
		_db.MediaVersions.Add(version);
		_db.SaveChanges();

		var versionFolder = Path.Combine(root.Path, version.Path);
		var seasonFolder = Path.Combine(versionFolder, "Season 01");
		Directory.CreateDirectory(seasonFolder);

		var oldFileName = "old-name.mkv";
		File.WriteAllText(Path.Combine(versionFolder, oldFileName), "tracked-content");
		var episodeFile = new EpisodeFile
		{
			SeriesId = series.Id,
			MediaVersionId = version.Id,
			RelativePath = oldFileName,
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.BLURAY, QualityResolution.R1080_P), new Revision()),
			DateAdded = _time.GetUtcNow().UtcDateTime,
			NamedFromPlaceholder = true
		};
		episodeFile.Episodes.Add(episode);
		_db.EpisodeFiles.Add(episodeFile);
		_db.SaveChanges();

		// A different, untracked file already sits at the exact name the rename would produce.
		var collidingTargetPath = Path.Combine(seasonFolder, "Test Show - S01E01 - Pilot.mkv");
		File.WriteAllText(collidingTargetPath, "untracked-content");

		var handler = new RenameSeriesCommandHandler(_db, new NamingService(), Substitute.For<IMetadataConsumerWriter>(), _eventBus, _time);
		await handler.ExecuteAsync(new RenameSeriesCommand(series.Id), _context, TestContext.Current.CancellationToken);

		File.ReadAllText(collidingTargetPath).ShouldBe("untracked-content", "the untracked file must never be overwritten");
		File.Exists(Path.Combine(versionFolder, oldFileName)).ShouldBeTrue("the tracked file must remain since the rename was skipped");
		var reloaded = await _db.EpisodeFiles.SingleAsync(x => x.Id == episodeFile.Id, TestContext.Current.CancellationToken);
		reloaded.RelativePath.ShouldBe(oldFileName);
	}
}
