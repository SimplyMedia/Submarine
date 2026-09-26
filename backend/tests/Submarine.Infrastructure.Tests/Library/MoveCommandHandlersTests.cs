using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Library;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Library;

public sealed class MoveCommandHandlersTests : IDisposable
{
	private readonly string _tempDir = Directory.CreateTempSubdirectory("submarine-movehandlers-").FullName;
	private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
	private readonly SqliteSubmarineDbContext _db;
	private readonly IEventBus _eventBus = Substitute.For<IEventBus>();
	private readonly ICommandContext _context = Substitute.For<ICommandContext>();

	public MoveCommandHandlersTests()
	{
		_db = TestDbFactory.Create(_time);
	}

	public void Dispose()
	{
		_db.Dispose();
		Directory.Delete(_tempDir, recursive: true);
	}

	[Fact]
	public async Task ExecuteAsync_ShouldPersistEarlierVersionMove_WhenALaterVersionFailsToMove()
	{
		var quality = new QualityProfile { Name = "Q" };
		var language = new LanguageProfile { Name = "L" };
		var oldRoot1 = new RootFolder { Path = Path.Combine(_tempDir, "old1"), MediaKind = MediaKind.SERIES };
		var oldRoot2 = new RootFolder { Path = Path.Combine(_tempDir, "old2"), MediaKind = MediaKind.SERIES };
		var targetRoot = new RootFolder { Path = Path.Combine(_tempDir, "target"), MediaKind = MediaKind.SERIES };
		Directory.CreateDirectory(oldRoot1.Path);
		Directory.CreateDirectory(oldRoot2.Path);
		Directory.CreateDirectory(targetRoot.Path);
		_db.QualityProfiles.Add(quality);
		_db.LanguageProfiles.Add(language);
		_db.RootFolders.AddRange(oldRoot1, oldRoot2, targetRoot);
		_db.SaveChanges();

		var series = new Series { TvdbId = 1, Title = "Show", CleanTitle = "show" };
		_db.Series.Add(series);
		_db.SaveChanges();

		var version1 = new MediaVersion { SeriesId = series.Id, Name = "v1", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = oldRoot1.Id, Path = "v1" };
		var version2 = new MediaVersion { SeriesId = series.Id, Name = "v2", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = oldRoot2.Id, Path = "v2" };
		_db.MediaVersions.AddRange(version1, version2);
		_db.SaveChanges();

		Directory.CreateDirectory(Path.Combine(oldRoot1.Path, "v1"));
		Directory.CreateDirectory(Path.Combine(oldRoot2.Path, "v2"));
		// Pre-create the target for v2 so the handler refuses to move onto it (simulates a later-version failure).
		Directory.CreateDirectory(Path.Combine(targetRoot.Path, "v2"));

		var handler = new MoveSeriesCommandHandler(_db, _eventBus);

		await Should.ThrowAsync<Core.Common.ConflictException>(() =>
			handler.ExecuteAsync(new MoveSeriesCommand(series.Id, targetRoot.Id), _context, TestContext.Current.CancellationToken));

		var reloadedVersion1 = await _db.MediaVersions.FindAsync([version1.Id], TestContext.Current.CancellationToken);
		var reloadedVersion2 = await _db.MediaVersions.FindAsync([version2.Id], TestContext.Current.CancellationToken);

		reloadedVersion1!.RootFolderId.ShouldBe(targetRoot.Id, "the earlier version's move must persist even though a later version failed");
		Directory.Exists(Path.Combine(targetRoot.Path, "v1")).ShouldBeTrue();
		reloadedVersion2!.RootFolderId.ShouldBe(oldRoot2.Id, "the failed version must keep its original root");
	}
}
