using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Submarine.Infrastructure.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Import;

public sealed class FileLinkerTests : IDisposable
{
	private readonly string _tempDir = Directory.CreateTempSubdirectory("submarine-filelinker-").FullName;
	private readonly FileLinker _linker = new(NullLogger<FileLinker>.Instance);

	public void Dispose() => Directory.Delete(_tempDir, recursive: true);

	[Fact]
	public void LinkOrMove_ShouldHardlink_WhenUseHardlinksTrue_OnSameVolume()
	{
		var source = Path.Combine(_tempDir, "source.mkv");
		var destination = Path.Combine(_tempDir, "sub", "destination.mkv");
		File.WriteAllText(source, "content");

		var hardlinked = _linker.LinkOrMove(source, destination, useHardlinks: true);

		hardlinked.ShouldBeTrue();
		File.Exists(source).ShouldBeTrue("hardlinked source must still exist");
		File.Exists(destination).ShouldBeTrue();
		File.ReadAllText(destination).ShouldBe("content");
	}

	[Fact]
	public void LinkOrMove_ShouldMove_WhenUseHardlinksFalse()
	{
		var source = Path.Combine(_tempDir, "source.mkv");
		var destination = Path.Combine(_tempDir, "sub", "destination.mkv");
		File.WriteAllText(source, "content");

		var hardlinked = _linker.LinkOrMove(source, destination, useHardlinks: false);

		hardlinked.ShouldBeFalse();
		File.Exists(source).ShouldBeFalse("moved source must no longer exist");
		File.Exists(destination).ShouldBeTrue();
		File.ReadAllText(destination).ShouldBe("content");
	}

	[Fact]
	public void LinkOrMove_ShouldOverwriteExistingDestination()
	{
		var source = Path.Combine(_tempDir, "source.mkv");
		var destination = Path.Combine(_tempDir, "destination.mkv");
		File.WriteAllText(source, "new-content");
		File.WriteAllText(destination, "old-content");

		_linker.LinkOrMove(source, destination, useHardlinks: false);

		File.ReadAllText(destination).ShouldBe("new-content");
	}

	[Fact]
	public void LinkOrMove_ShouldCreateDestinationDirectory_WhenMissing()
	{
		var source = Path.Combine(_tempDir, "source.mkv");
		var destination = Path.Combine(_tempDir, "a", "b", "c", "destination.mkv");
		File.WriteAllText(source, "content");

		_linker.LinkOrMove(source, destination, useHardlinks: false);

		File.Exists(destination).ShouldBeTrue();
	}

	[Fact]
	[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
	public void ApplyPermissions_ShouldSetUnixFileMode_OnUnix()
	{
		if (OperatingSystem.IsWindows())
		{
			Assert.Skip("Unix file permissions are only meaningful on Linux or macOS");
			return;
		}

		var file = Path.Combine(_tempDir, "perm.mkv");
		File.WriteAllText(file, "content");

		_linker.ApplyPermissions(file, "644", isDirectory: false);

		var mode = File.GetUnixFileMode(file);
		mode.HasFlag(UnixFileMode.UserRead).ShouldBeTrue();
		mode.HasFlag(UnixFileMode.UserWrite).ShouldBeTrue();
		mode.HasFlag(UnixFileMode.OtherWrite).ShouldBeFalse();
	}
}
