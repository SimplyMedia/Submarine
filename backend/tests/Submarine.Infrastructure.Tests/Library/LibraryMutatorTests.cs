using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Library;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Library;

public sealed class LibraryMutatorTests : IDisposable
{
	private readonly string _tempDir = Directory.CreateTempSubdirectory("submarine-librarymutator-").FullName;
	private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
	private readonly SqliteSubmarineDbContext _db;
	private readonly ICommandQueue _commandQueue = Substitute.For<ICommandQueue>();
	private readonly IEventBus _eventBus = Substitute.For<IEventBus>();
	private readonly LibraryMutator _mutator;

	public LibraryMutatorTests()
	{
		_db = TestDbFactory.Create(_time);
		_mutator = new LibraryMutator(_db, _commandQueue, _eventBus, _time);
	}

	public void Dispose()
	{
		_db.Dispose();
		Directory.Delete(_tempDir, recursive: true);
	}

	[Fact]
	public async Task UpdateSeriesAsync_ShouldNotEditVersionBelongingToAnotherSeries()
	{
		var quality = new QualityProfile { Name = "Q" };
		var language = new LanguageProfile { Name = "L" };
		var root = new RootFolder { Path = Path.Combine(_tempDir, "library"), MediaKind = MediaKind.SERIES };
		_db.QualityProfiles.Add(quality);
		_db.LanguageProfiles.Add(language);
		_db.RootFolders.Add(root);
		_db.SaveChanges();

		var seriesA = new Series { TvdbId = 1, Title = "A", CleanTitle = "a" };
		var seriesB = new Series { TvdbId = 2, Title = "B", CleanTitle = "b" };
		_db.Series.AddRange(seriesA, seriesB);
		_db.SaveChanges();

		var versionB = new MediaVersion { SeriesId = seriesB.Id, Name = "main", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "B" };
		_db.MediaVersions.Add(versionB);
		_db.SaveChanges();

		var options = new UpdateSeriesOptions(null, null, null, null, null, null, null, false,
			[new UpdateVersionOptions(versionB.Id, "hijacked", null, null, null)]);

		await Should.ThrowAsync<KeyNotFoundException>(() => _mutator.UpdateSeriesAsync(seriesA.Id, options, TestContext.Current.CancellationToken));

		var reloaded = await _db.MediaVersions.FindAsync([versionB.Id], TestContext.Current.CancellationToken);
		reloaded!.Name.ShouldBe("main");
	}

	[Fact]
	public async Task DeleteSeriesAsync_ShouldNotDeleteOutsideRoot_WhenVersionPathEscapesRoot()
	{
		var quality = new QualityProfile { Name = "Q" };
		var language = new LanguageProfile { Name = "L" };
		var root = new RootFolder { Path = Path.Combine(_tempDir, "library"), MediaKind = MediaKind.SERIES };
		Directory.CreateDirectory(root.Path);
		_db.QualityProfiles.Add(quality);
		_db.LanguageProfiles.Add(language);
		_db.RootFolders.Add(root);
		_db.SaveChanges();

		var series = new Series { Title = "Escaper", CleanTitle = "escaper" };
		_db.Series.Add(series);
		_db.SaveChanges();

		// Simulates legacy/corrupt data: a version path that escapes the root folder.
		var version = new MediaVersion { SeriesId = series.Id, Name = "main", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "../victim" };
		_db.MediaVersions.Add(version);
		_db.SaveChanges();

		var victim = Path.Combine(_tempDir, "victim");
		Directory.CreateDirectory(victim);
		File.WriteAllText(Path.Combine(victim, "keep-me.txt"), "important");

		await _mutator.DeleteSeriesAsync(series.Id, deleteFiles: true, addImportListExclusion: false, TestContext.Current.CancellationToken);

		Directory.Exists(victim).ShouldBeTrue("the guard must refuse to delete a path outside the root folder");
		File.Exists(Path.Combine(victim, "keep-me.txt")).ShouldBeTrue();
		(await _db.Series.FindAsync([series.Id], TestContext.Current.CancellationToken)).ShouldBeNull();
	}
	[Fact]
	public async Task UpdateSeriesAsync_ShouldRejectVersionPathUsedByAnotherVersionInSameRoot()
	{
		var quality = new QualityProfile { Name = "Q" };
		var language = new LanguageProfile { Name = "L" };
		var root = new RootFolder { Path = Path.Combine(_tempDir, "library"), MediaKind = MediaKind.SERIES };
		_db.QualityProfiles.Add(quality);
		_db.LanguageProfiles.Add(language);
		_db.RootFolders.Add(root);
		_db.SaveChanges();

		var series = new Series { TvdbId = 3, Title = "Show", CleanTitle = "show" };
		_db.Series.Add(series);
		_db.SaveChanges();

		var first = new MediaVersion { SeriesId = series.Id, Name = "main", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "main" };
		var second = new MediaVersion { SeriesId = series.Id, Name = "alternate", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "alternate" };
		_db.MediaVersions.AddRange(first, second);
		_db.SaveChanges();

		var options = new UpdateSeriesOptions(null, null, null, null, null, null, null, false,
			[new UpdateVersionOptions(second.Id, null, null, null, first.Path)]);

		await Should.ThrowAsync<FluentValidation.ValidationException>(() =>
			_mutator.UpdateSeriesAsync(series.Id, options, TestContext.Current.CancellationToken));

		var reloaded = await _db.MediaVersions.FindAsync([second.Id], TestContext.Current.CancellationToken);
		reloaded!.Path.ShouldBe("alternate");
	}
}
