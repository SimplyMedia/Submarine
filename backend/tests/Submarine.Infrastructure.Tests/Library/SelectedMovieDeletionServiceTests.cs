using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Infrastructure.Import;
using Submarine.Infrastructure.Library;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Library;

public sealed class SelectedMovieDeletionServiceTests : IDisposable
{
	private readonly string _tempDir = Directory.CreateTempSubdirectory("submarine-selected-movie-delete-").FullName;
	private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
	private readonly SqliteSubmarineDbContext _db;
	private readonly IEventBus _eventBus = Substitute.For<IEventBus>();

	public SelectedMovieDeletionServiceTests()
	{
		_db = TestDbFactory.Create(_time);
	}

	public void Dispose()
	{
		_db.Dispose();
		Directory.Delete(_tempDir, recursive: true);
	}

	[Fact]
	public async Task DeleteAsync_ShouldRecycleVersionFiles()
	{
		var (movie, version, root, movieFile) = await CreateMovieWithSiblingVersionAsync();
		var sourceFolder = Path.Combine(root.Path, version.Path);
		Directory.CreateDirectory(sourceFolder);
		var sourceFile = Path.Combine(sourceFolder, "movie.mkv");
		File.WriteAllText(sourceFile, "video");
		var recycleBin = Path.Combine(_tempDir, "recycle");
		_db.MediaManagementConfig.Add(new MediaManagementConfig { RecycleBinPath = recycleBin });
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var service = new SelectedMovieDeletionService(_db, CreateMutator(), new RecycleBinService(), _eventBus, _time);

		await service.DeleteAsync(movie.Id, version.Id, deleteFiles: true, addImportExclusion: false, TestContext.Current.CancellationToken);

		File.Exists(sourceFile).ShouldBeFalse();
		File.Exists(Path.Combine(recycleBin, version.Path, "movie.mkv")).ShouldBeTrue();
		(await _db.MovieFiles.FindAsync([movieFile.Id], TestContext.Current.CancellationToken)).ShouldBeNull();
		(await _db.MediaVersions.FindAsync([version.Id], TestContext.Current.CancellationToken)).ShouldBeNull();
	}

	[Fact]
	public async Task DeleteAsync_ShouldReportRecycleFailureAfterDatabaseCommit()
	{
		var (movie, version, root, _) = await CreateMovieWithSiblingVersionAsync();
		var sourceFolder = Path.Combine(root.Path, version.Path);
		Directory.CreateDirectory(sourceFolder);
		var sourceFile = Path.Combine(sourceFolder, "movie.mkv");
		File.WriteAllText(sourceFile, "video");
		var recycleBin = Path.Combine(_tempDir, "recycle");
		_db.MediaManagementConfig.Add(new MediaManagementConfig { RecycleBinPath = recycleBin });
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var recycle = Substitute.For<IRecycleBinService>();
		recycle.When(x => x.Recycle(sourceFile, root.Path, recycleBin, _time))
			.Do(_ =>
			{
				_db.MediaVersions.AsNoTracking().SingleOrDefault(x => x.Id == version.Id).ShouldBeNull("recycling starts only after the database commit");
				throw new IOException("recycle failed");
			});
		var service = new SelectedMovieDeletionService(_db, CreateMutator(), recycle, _eventBus, _time);

		await Should.ThrowAsync<IOException>(() => service.DeleteAsync(movie.Id, version.Id, deleteFiles: true, addImportExclusion: false, TestContext.Current.CancellationToken));

		(await _db.MediaVersions.FindAsync([version.Id], TestContext.Current.CancellationToken)).ShouldBeNull();
		File.Exists(sourceFile).ShouldBeTrue();
	}

	[Fact]
	public async Task DeleteAsync_ShouldRecycleFilesAfterDeletingWholeMovie()
	{
		var (movie, version, root, _) = await CreateMovieWithSiblingVersionAsync();
		await _db.MediaVersions.Where(x => x.MovieId == movie.Id && x.Id != version.Id)
			.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
		_db.ChangeTracker.Clear();
		var sourceFolder = Path.Combine(root.Path, version.Path);
		Directory.CreateDirectory(sourceFolder);
		var sourceFile = Path.Combine(sourceFolder, "movie.mkv");
		File.WriteAllText(sourceFile, "video");
		var recycleBin = Path.Combine(_tempDir, "recycle");
		_db.MediaManagementConfig.Add(new MediaManagementConfig { RecycleBinPath = recycleBin });
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
		var service = new SelectedMovieDeletionService(_db, CreateMutator(), new RecycleBinService(), _eventBus, _time);

		await service.DeleteAsync(movie.Id, version.Id, deleteFiles: true, addImportExclusion: false, TestContext.Current.CancellationToken);

		(await _db.Movies.FindAsync([movie.Id], TestContext.Current.CancellationToken)).ShouldBeNull();
		File.Exists(sourceFile).ShouldBeFalse();
		File.Exists(Path.Combine(recycleBin, version.Path, "movie.mkv")).ShouldBeTrue();
	}

	private async Task<(Movie Movie, MediaVersion Version, RootFolder Root, MovieFile File)> CreateMovieWithSiblingVersionAsync()
	{
		var quality = new QualityProfile { Name = "Q" };
		var language = new LanguageProfile { Name = "L" };
		var root = new RootFolder { Path = Path.Combine(_tempDir, "library"), MediaKind = MediaKind.MOVIES };
		Directory.CreateDirectory(root.Path);
		_db.QualityProfiles.Add(quality);
		_db.LanguageProfiles.Add(language);
		_db.RootFolders.Add(root);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var movie = new Movie { TmdbId = 100, Title = "Movie", CleanTitle = "movie" };
		_db.Movies.Add(movie);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var version = new MediaVersion { MovieId = movie.Id, Name = "selected", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "selected" };
		var sibling = new MediaVersion { MovieId = movie.Id, Name = "sibling", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "sibling" };
		_db.MediaVersions.AddRange(version, sibling);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var file = new MovieFile { MovieId = movie.Id, MediaVersionId = version.Id, RelativePath = "movie.mkv", Size = 5, DateAdded = _time.GetUtcNow().UtcDateTime };
		_db.MovieFiles.Add(file);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
		return (movie, version, root, file);
	}

	private LibraryMutator CreateMutator() => new(_db, Substitute.For<Submarine.Infrastructure.Commands.ICommandQueue>(), _eventBus, _time);
}

