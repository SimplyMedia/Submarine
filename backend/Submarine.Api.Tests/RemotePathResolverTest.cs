using Submarine.Api.Services;
using Submarine.Core.Download;
using Xunit;

namespace Submarine.Api.Tests;

public class RemotePathResolverTest
{
	[Fact]
	public void Resolve_ShouldReplacePrefix_WhenMappingMatches()
	{
		var mappings = new List<RemotePathMapping>
		{
			new() { Host = "qbit", RemotePath = "/data/downloads", LocalPath = @"D:\downloads" }
		};

		var result = RemotePathResolver.Resolve("qbit", "/data/downloads/show/episode.mkv", mappings);

		Assert.Equal(@"D:\downloads\show\episode.mkv", result);
	}

	[Fact]
	public void Resolve_ShouldNormalizeSeparators_WhenLocalPathUsesBackslashes()
	{
		var mappings = new List<RemotePathMapping>
		{
			new() { Host = "qbit", RemotePath = "/data/downloads", LocalPath = @"D:\downloads" }
		};

		var result = RemotePathResolver.Resolve("qbit", "/data/downloads", mappings);

		Assert.Equal(@"D:\downloads", result);
	}

	[Fact]
	public void Resolve_ShouldUseLongestMatch_WhenMultipleMappingsMatch()
	{
		var mappings = new List<RemotePathMapping>
		{
			new() { Host = "qbit", RemotePath = "/data", LocalPath = @"D:\wrong" },
			new() { Host = "qbit", RemotePath = "/data/downloads", LocalPath = @"D:\downloads" }
		};

		var result = RemotePathResolver.Resolve("qbit", "/data/downloads/movie.mkv", mappings);

		Assert.Equal(@"D:\downloads\movie.mkv", result);
	}

	[Fact]
	public void Resolve_ShouldIsolateByHost_WhenMappingsExistForOtherHosts()
	{
		var mappings = new List<RemotePathMapping>
		{
			new() { Host = "other-host", RemotePath = "/data/downloads", LocalPath = @"D:\downloads" }
		};

		var result = RemotePathResolver.Resolve("qbit", "/data/downloads/movie.mkv", mappings);

		Assert.Equal("/data/downloads/movie.mkv", result);
	}

	[Fact]
	public void Resolve_ShouldReturnUnchanged_WhenNoMappingMatches()
	{
		var mappings = new List<RemotePathMapping>
		{
			new() { Host = "qbit", RemotePath = "/other", LocalPath = @"D:\other" }
		};

		var result = RemotePathResolver.Resolve("qbit", "/data/downloads/movie.mkv", mappings);

		Assert.Equal("/data/downloads/movie.mkv", result);
	}

	[Fact]
	public void ExtractHost_ShouldReturnHost_WhenSettingsJsonHasHostProperty()
	{
		var host = RemotePathResolver.ExtractHost("""{"host":"qbit.local","port":8080}""");

		Assert.Equal("qbit.local", host);
	}

	[Fact]
	public void ExtractHost_ShouldReturnNull_WhenSettingsJsonHasNoHostProperty()
	{
		var host = RemotePathResolver.ExtractHost("""{"folder":"/downloads"}""");

		Assert.Null(host);
	}
}
