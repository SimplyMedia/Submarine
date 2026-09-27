using Submarine.Infrastructure.Library;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Library;

public sealed class VersionFolderMoveTests
{
	[Fact]
	public void IsCrossDeviceMove_ShouldRecognizeNativeCrossDeviceError()
	{
		var nativeErrorCode = OperatingSystem.IsWindows() ? 17 : 18;
		var crossDeviceException = new IOException("cross-device", unchecked((int)(0x80070000u | (uint)nativeErrorCode)));
		var otherMoveException = new IOException("different native error", unchecked((int)(0x80070000u | (uint)(nativeErrorCode == 17 ? 18 : 17))));

		VersionFolderMove.IsCrossDeviceMove(crossDeviceException).ShouldBeTrue();
		VersionFolderMove.IsCrossDeviceMove(otherMoveException).ShouldBeFalse();
		VersionFolderMove.IsCrossDeviceMove(new IOException("other I/O error")).ShouldBeFalse();
	}

	[Fact]
	public void MoveWithFallback_ShouldNotCopyAfterAnUnrelatedMoveFailure()
	{
		var tempDirectory = Directory.CreateTempSubdirectory("submarine-version-move-");
		try
		{
			var source = Path.Combine(tempDirectory.FullName, "source");
			var destination = Path.Combine(tempDirectory.FullName, "destination");
			Directory.CreateDirectory(source);
			File.WriteAllText(Path.Combine(source, "video.mkv"), "source");
			File.WriteAllText(destination, "destination file");

			Should.Throw<IOException>(() => VersionFolderMove.MoveWithFallback(source, destination));

			Directory.Exists(source).ShouldBeTrue();
			File.ReadAllText(Path.Combine(source, "video.mkv")).ShouldBe("source");
			File.ReadAllText(destination).ShouldBe("destination file");
		}
		finally
		{
			Directory.Delete(tempDirectory.FullName, recursive: true);
		}
	}

	[Fact]
	public void VerifyCopiedFiles_ShouldRejectDifferentFileLengths()
	{
		var tempDirectory = Directory.CreateTempSubdirectory("submarine-version-copy-");
		try
		{
			var source = Path.Combine(tempDirectory.FullName, "source");
			var destination = Path.Combine(tempDirectory.FullName, "destination");
			Directory.CreateDirectory(source);
			Directory.CreateDirectory(destination);
			File.WriteAllText(Path.Combine(source, "video.mkv"), "complete source");
			File.WriteAllText(Path.Combine(destination, "video.mkv"), "truncated");

			Should.Throw<IOException>(() => VersionFolderMove.VerifyCopiedFiles(source, destination));
			Directory.Exists(source).ShouldBeTrue();
		}
		finally
		{
			Directory.Delete(tempDirectory.FullName, recursive: true);
		}
	}
}
