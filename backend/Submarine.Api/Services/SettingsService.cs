using Microsoft.EntityFrameworkCore;
using Submarine.Api.Models.Database;
using Submarine.Api.Models.Request;
using Submarine.Core.Config;

namespace Submarine.Api.Services;

public class SettingsService
{
	private const int SingletonId = 1;

	private readonly SubmarineDatabaseContext _databaseContext;

	public SettingsService(SubmarineDatabaseContext databaseContext)
		=> _databaseContext = databaseContext;

	public async Task<NamingConfig> GetNamingConfigAsync()
	{
		var config = await _databaseContext.NamingConfigs.FirstOrDefaultAsync(c => c.Id == SingletonId);

		if (config != null)
			return config;

		config = new NamingConfig { Id = SingletonId };
		await _databaseContext.NamingConfigs.AddAsync(config);
		await _databaseContext.SaveChangesAsync();

		return config;
	}

	public async Task<NamingConfig> UpdateNamingConfigAsync(UpdateNamingConfigRequest request)
	{
		var config = await GetNamingConfigAsync();

		config.RenameEpisodes = request.RenameEpisodes;
		config.StandardEpisodeFormat = request.StandardEpisodeFormat;
		config.AnimeEpisodeFormat = request.AnimeEpisodeFormat;
		config.MovieFormat = request.MovieFormat;
		config.SeriesFolderFormat = request.SeriesFolderFormat;
		config.SeasonFolderFormat = request.SeasonFolderFormat;
		config.MovieFolderFormat = request.MovieFolderFormat;

		await _databaseContext.SaveChangesAsync();

		return config;
	}

	public async Task<MediaManagementConfig> GetMediaManagementConfigAsync()
	{
		var config = await _databaseContext.MediaManagementConfigs.FirstOrDefaultAsync(c => c.Id == SingletonId);

		if (config != null)
			return config;

		config = new MediaManagementConfig { Id = SingletonId };
		await _databaseContext.MediaManagementConfigs.AddAsync(config);
		await _databaseContext.SaveChangesAsync();

		return config;
	}

	public async Task<MediaManagementConfig> UpdateMediaManagementConfigAsync(UpdateMediaManagementConfigRequest request)
	{
		var config = await GetMediaManagementConfigAsync();

		config.UseHardlinks = request.UseHardlinks;
		config.ImportExtraFiles = request.ImportExtraFiles;
		config.MinimumFreeSpaceMb = request.MinimumFreeSpaceMb;

		await _databaseContext.SaveChangesAsync();

		return config;
	}
}
