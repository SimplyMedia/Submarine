using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Services;
using Submarine.Core.Config;
using Xunit;

namespace Submarine.Api.Tests;

public class PermissionApplierTest
{
	[Theory]
	[InlineData("755")]
	[InlineData("644")]
	[InlineData("0777")]
	public void TryParseMode_ShouldReturnTrue_WhenValidOctal(string octal)
	{
		Assert.True(PermissionApplier.TryParseMode(octal, out var mode));
		Assert.Equal((UnixFileMode)Convert.ToInt32(octal, 8), mode);
	}

	[Theory]
	[InlineData("8")]
	[InlineData("abc")]
	[InlineData("")]
	[InlineData(null)]
	public void TryParseMode_ShouldReturnFalse_WhenInvalidOctal(string? octal)
		=> Assert.False(PermissionApplier.TryParseMode(octal, out _));

	[Fact]
	public void ApplyToFile_ShouldApplyModeOnUnix_AndNoOpOnWindows()
	{
		var path = Path.GetTempFileName();

		try
		{
			var config = new MediaManagementConfig { ChmodFile = "640" };

			PermissionApplier.ApplyToFile(path, config, NullLogger.Instance);

			if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
				Assert.Equal((UnixFileMode)Convert.ToInt32("640", 8), File.GetUnixFileMode(path));

			// On Windows the call is a no-op: it neither throws nor disturbs the file.
			Assert.True(File.Exists(path));
		}
		finally
		{
			File.Delete(path);
		}
	}
}
