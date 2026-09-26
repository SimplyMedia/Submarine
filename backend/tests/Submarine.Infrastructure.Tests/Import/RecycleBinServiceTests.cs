using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Submarine.Infrastructure.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Import;

public sealed class RecycleBinServiceTests : IDisposable
{
	private readonly string _tempDir = Directory.CreateTempSubdirectory("submarine-recyclebin-").FullName;
	private readonly RecycleBinService _service = new();

	public void Dispose() => Directory.Delete(_tempDir, recursive: true);

	[Fact]
	public void Recycle_ShouldMirrorRelativePath_UnderRecycleBin()
	{
		var root = Path.Combine(_tempDir, "library");
		var recycleBin = Path.Combine(_tempDir, "recycle");
		var file = Path.Combine(root, "Show", "Season 01", "episode.mkv");
		Directory.CreateDirectory(Path.GetDirectoryName(file)!);
		File.WriteAllText(file, "content");

		_service.Recycle(file, root, recycleBin, new FakeTimeProvider());

		var expected = Path.Combine(recycleBin, "Show", "Season 01", "episode.mkv");
		File.Exists(expected).ShouldBeTrue();
		File.Exists(file).ShouldBeFalse();
	}

	[Fact]
	public void Cleanup_ShouldKeepOldMedia_RecycledToday()
	{
		var root = Path.Combine(_tempDir, "library");
		var recycleBin = Path.Combine(_tempDir, "recycle");
		var file = Path.Combine(root, "old.mkv");
		Directory.CreateDirectory(root);
		File.WriteAllText(file, "content");
		File.SetLastWriteTimeUtc(file, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
		var now = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

		_service.Recycle(file, root, recycleBin, new FakeTimeProvider(now));
		var deleted = _service.CleanupOlderThan(recycleBin, now.AddDays(-7));

		deleted.ShouldBe(0);
		File.Exists(Path.Combine(recycleBin, "old.mkv")).ShouldBeTrue();
	}

	[Fact]
	public void Recycle_ShouldDeleteImmediately_WhenRecycleBinPathIsEmpty()
	{
		var file = Path.Combine(_tempDir, "episode.mkv");
		File.WriteAllText(file, "content");

		_service.Recycle(file, _tempDir, string.Empty, new FakeTimeProvider());

		File.Exists(file).ShouldBeFalse();
	}

	[Fact]
	public void Recycle_ShouldTimestampCollision_WhenDestinationAlreadyExists()
	{
		var root = Path.Combine(_tempDir, "library");
		var recycleBin = Path.Combine(_tempDir, "recycle");
		Directory.CreateDirectory(root);
		Directory.CreateDirectory(recycleBin);
		File.WriteAllText(Path.Combine(recycleBin, "episode.mkv"), "old");

		var file = Path.Combine(root, "episode.mkv");
		File.WriteAllText(file, "new");

		var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
		_service.Recycle(file, root, recycleBin, time);

		File.Exists(Path.Combine(recycleBin, "episode.mkv")).ShouldBeTrue();
		File.ReadAllText(Path.Combine(recycleBin, "episode.mkv")).ShouldBe("old");
		File.Exists(Path.Combine(recycleBin, "episode_20260102030405.mkv")).ShouldBeTrue();
	}

	[Fact]
	public void Recycle_ShouldNoOp_WhenSourceFileMissing()
	{
		var missing = Path.Combine(_tempDir, "missing.mkv");

		Should.NotThrow(() => _service.Recycle(missing, _tempDir, Path.Combine(_tempDir, "bin"), new FakeTimeProvider()));
	}

	[Fact]
	public void CleanupOlderThan_ShouldDeleteOldFiles_AndKeepRecentOnes()
	{
		var recycleBin = Path.Combine(_tempDir, "recycle");
		Directory.CreateDirectory(recycleBin);
		var oldFile = Path.Combine(recycleBin, "old.mkv");
		var newFile = Path.Combine(recycleBin, "new.mkv");
		File.WriteAllText(oldFile, "old");
		File.WriteAllText(newFile, "new");
		File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-30));
		File.SetLastWriteTimeUtc(newFile, DateTime.UtcNow);

		var deleted = _service.CleanupOlderThan(recycleBin, DateTime.UtcNow.AddDays(-7));

		deleted.ShouldBe(1);
		File.Exists(oldFile).ShouldBeFalse();
		File.Exists(newFile).ShouldBeTrue();
	}

	[Fact]
	public void CleanupOlderThan_ShouldRemoveEmptyDirectories_AfterDeletingFiles()
	{
		var recycleBin = Path.Combine(_tempDir, "recycle");
		var subDir = Path.Combine(recycleBin, "Show", "Season 01");
		Directory.CreateDirectory(subDir);
		var oldFile = Path.Combine(subDir, "old.mkv");
		File.WriteAllText(oldFile, "old");
		File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-30));

		_service.CleanupOlderThan(recycleBin, DateTime.UtcNow.AddDays(-7));

		Directory.Exists(subDir).ShouldBeFalse();
	}

	[Fact]
	public void CleanupOlderThan_ShouldReturnZero_WhenRecycleBinPathIsEmpty()
		=> _service.CleanupOlderThan(string.Empty, DateTime.UtcNow).ShouldBe(0);
}
