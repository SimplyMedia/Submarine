using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.MediaFiles;
using Xunit;

namespace Submarine.Infrastructure.Tests.MediaFiles;

public sealed class RemotePathResolverTests
{
	[Fact]
	public void ToLocal_ShouldTranslate_WhenHostAndPrefixMatch()
	{
		var mappings = new[] { new RemotePathMapping { Host = "qbt-host", RemotePath = "/downloads", LocalPath = "/mnt/downloads" } };

		var result = RemotePathResolver.ToLocal(mappings, "qbt-host", "/downloads/show/episode.mkv");

		result.ShouldBe("/mnt/downloads/show/episode.mkv");
	}

	[Fact]
	public void ToLocal_ShouldBeCaseInsensitive_ForHost()
	{
		var mappings = new[] { new RemotePathMapping { Host = "QBT-HOST", RemotePath = "/downloads", LocalPath = "/mnt/downloads" } };

		var result = RemotePathResolver.ToLocal(mappings, "qbt-host", "/downloads/movie.mkv");

		result.ShouldBe("/mnt/downloads/movie.mkv");
	}

	[Fact]
	public void ToLocal_ShouldPickLongestPrefix_WhenMultipleMatch()
	{
		var mappings = new[]
		{
			new RemotePathMapping { Host = "host", RemotePath = "/downloads", LocalPath = "/mnt/generic" },
			new RemotePathMapping { Host = "host", RemotePath = "/downloads/tv", LocalPath = "/mnt/tv" }
		};

		var result = RemotePathResolver.ToLocal(mappings, "host", "/downloads/tv/show/episode.mkv");

		result.ShouldBe("/mnt/tv/show/episode.mkv");
	}

	[Fact]
	public void ToLocal_ShouldBeSeparatorSafe_AndNotMatchPartialSegment()
	{
		var mappings = new[] { new RemotePathMapping { Host = "host", RemotePath = "/downloads", LocalPath = "/mnt/downloads" } };

		var result = RemotePathResolver.ToLocal(mappings, "host", "/downloads-archive/movie.mkv");

		result.ShouldBe("/downloads-archive/movie.mkv");
	}

	[Fact]
	public void ToLocal_ShouldReturnOriginal_WhenNoMappingMatches()
	{
		var mappings = new[] { new RemotePathMapping { Host = "other-host", RemotePath = "/downloads", LocalPath = "/mnt/downloads" } };

		var result = RemotePathResolver.ToLocal(mappings, "host", "/downloads/movie.mkv");

		result.ShouldBe("/downloads/movie.mkv");
	}

	[Fact]
	public void ToLocal_ShouldReturnOriginal_WhenHostIsNull()
	{
		var mappings = new[] { new RemotePathMapping { Host = "host", RemotePath = "/downloads", LocalPath = "/mnt/downloads" } };

		var result = RemotePathResolver.ToLocal(mappings, null, "/downloads/movie.mkv");

		result.ShouldBe("/downloads/movie.mkv");
	}

	[Fact]
	public void ToLocal_ShouldHandleWindowsStyleSeparators()
	{
		var mappings = new[] { new RemotePathMapping { Host = "host", RemotePath = @"C:\downloads", LocalPath = "/mnt/downloads" } };

		var result = RemotePathResolver.ToLocal(mappings, "host", @"C:\downloads\show\episode.mkv");

		result.ShouldBe("/mnt/downloads/show/episode.mkv");
	}
}
