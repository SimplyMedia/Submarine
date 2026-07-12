using Submarine.Api.Services;
using Xunit;

namespace Submarine.Api.Tests;

public class FileLinkerTest
{
	[Fact]
	public void Place_ShouldThrow_WhenFreeSpaceBelowMinimum()
	{
		var source = Path.GetTempFileName();
		var destination = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mkv");

		try
		{
			var exception = Assert.Throws<InvalidOperationException>(
				() => FileLinker.Place(source, destination, false, int.MaxValue));

			Assert.Contains("insufficient free space", exception.Message);
			Assert.False(File.Exists(destination));
		}
		finally
		{
			if (File.Exists(source))
				File.Delete(source);
		}
	}

	[Fact]
	public void Place_ShouldSucceed_WhenFreeSpaceCheckDisabled()
	{
		var source = Path.GetTempFileName();
		var destination = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mkv");

		try
		{
			FileLinker.Place(source, destination, false, 0);

			Assert.True(File.Exists(destination));
		}
		finally
		{
			if (File.Exists(destination))
				File.Delete(destination);
		}
	}
}
