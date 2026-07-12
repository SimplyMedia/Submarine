using Submarine.Api.Models.Request;
using Submarine.Core.MediaFile.Naming;
using Xunit;

namespace Submarine.Api.Tests;

public class SettingsServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task UpdateNamingConfigAsync_ShouldRoundtripMultiEpisodeStyle_WhenUpdated()
	{
		var service = Settings();

		var updated = await service.UpdateNamingConfigAsync(new UpdateNamingConfigRequest
		{
			RenameEpisodes = true,
			StandardEpisodeFormat = "{Series Title} - S{season:00}E{episode:00}",
			AnimeEpisodeFormat = "{Series Title} - {episode:00}",
			MovieFormat = "{Movie Title} ({Year})",
			SeriesFolderFormat = "{Series Title}",
			SeasonFolderFormat = "Season {Season:00}",
			MovieFolderFormat = "{Movie Title} ({Year})",
			MultiEpisodeStyle = MultiEpisodeStyle.RANGE
		});

		Assert.Equal(MultiEpisodeStyle.RANGE, updated.MultiEpisodeStyle);

		var reloaded = await service.GetNamingConfigAsync();

		Assert.Equal(MultiEpisodeStyle.RANGE, reloaded.MultiEpisodeStyle);
	}

	[Fact]
	public async Task UpdateMediaManagementConfigAsync_ShouldRoundtripPermissionFields_WhenUpdated()
	{
		var service = Settings();

		var updated = await service.UpdateMediaManagementConfigAsync(new UpdateMediaManagementConfigRequest
		{
			UseHardlinks = true,
			ImportExtraFiles = false,
			MinimumFreeSpaceMb = 100,
			WriteNfo = false,
			ChmodFolder = "755",
			ChmodFile = "644",
			ChownUser = "submarine",
			ChownGroup = "media"
		});

		Assert.Equal("755", updated.ChmodFolder);
		Assert.Equal("644", updated.ChmodFile);
		Assert.Equal("submarine", updated.ChownUser);
		Assert.Equal("media", updated.ChownGroup);

		var reloaded = await service.GetMediaManagementConfigAsync();

		Assert.Equal("755", reloaded.ChmodFolder);
		Assert.Equal("644", reloaded.ChmodFile);
		Assert.Equal("submarine", reloaded.ChownUser);
		Assert.Equal("media", reloaded.ChownGroup);
	}
}
