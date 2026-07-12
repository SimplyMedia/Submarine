using Microsoft.EntityFrameworkCore;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Database;
using Submarine.Api.Models.Request;
using Submarine.Core.Config;

namespace Submarine.Api.Services;

public class SettingsService
{
	private const int SingletonId = 1;

	private readonly SubmarineDatabaseContext _databaseContext;

	private readonly ILogger<SettingsService> _logger;

	private readonly SecurityConfigStore _securityConfigStore;

	public SettingsService(SubmarineDatabaseContext databaseContext, ILogger<SettingsService> logger,
		SecurityConfigStore securityConfigStore)
	{
		_databaseContext = databaseContext;
		_logger = logger;
		_securityConfigStore = securityConfigStore;
	}

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
		config.WriteNfo = request.WriteNfo;

		await _databaseContext.SaveChangesAsync();

		return config;
	}

	public async Task<IndexerConfig> GetIndexerConfigAsync()
	{
		var config = await _databaseContext.IndexerConfigs.FirstOrDefaultAsync(c => c.Id == SingletonId);

		if (config != null)
			return config;

		config = new IndexerConfig { Id = SingletonId };
		await _databaseContext.IndexerConfigs.AddAsync(config);
		await _databaseContext.SaveChangesAsync();

		return config;
	}

	public async Task<IndexerConfig> UpdateIndexerConfigAsync(UpdateIndexerConfigRequest request)
	{
		var config = await GetIndexerConfigAsync();

		config.RssSyncIntervalMinutes = request.RssSyncIntervalMinutes;
		config.MinimumAgeMinutes = request.MinimumAgeMinutes;
		config.RetentionDays = request.RetentionDays;
		config.MaximumSizeMb = request.MaximumSizeMb;

		await _databaseContext.SaveChangesAsync();

		return config;
	}

	public async Task<DownloadConfig> GetDownloadConfigAsync()
	{
		var config = await _databaseContext.DownloadConfigs.FirstOrDefaultAsync(c => c.Id == SingletonId);

		if (config != null)
			return config;

		config = new DownloadConfig { Id = SingletonId };
		await _databaseContext.DownloadConfigs.AddAsync(config);
		await _databaseContext.SaveChangesAsync();

		return config;
	}

	public async Task<DownloadConfig> UpdateDownloadConfigAsync(UpdateDownloadConfigRequest request)
	{
		var config = await GetDownloadConfigAsync();

		config.EnableFailedDownloadHandling = request.EnableFailedDownloadHandling;
		config.RedownloadFailedReleases = request.RedownloadFailedReleases;
		config.RemoveFailedFromClient = request.RemoveFailedFromClient;

		await _databaseContext.SaveChangesAsync();

		return config;
	}

	public async Task<SecurityConfig> GetSecurityConfigAsync()
	{
		var config = await _databaseContext.SecurityConfigs.FirstOrDefaultAsync(c => c.Id == SingletonId);

		if (config == null)
		{
			config = new SecurityConfig
			{
				Id = SingletonId, ApiKey = Guid.NewGuid().ToString("N"), FeedToken = Guid.NewGuid().ToString("N")
			};
			await _databaseContext.SecurityConfigs.AddAsync(config);
			await _databaseContext.SaveChangesAsync();

			_logger.LogInformation("Generated API key: {Key}", config.ApiKey);
		}
		else if (string.IsNullOrEmpty(config.FeedToken))
		{
			config.FeedToken = Guid.NewGuid().ToString("N");
			await _databaseContext.SaveChangesAsync();
		}

		return config;
	}

	public async Task<SecurityConfig> UpdateSecurityConfigAsync(UpdateSecurityConfigRequest request)
	{
		if (!Enum.IsDefined(request.Method))
			throw new BadRequestException($"Invalid authentication method '{request.Method}'");

		var config = await GetSecurityConfigAsync();

		config.Method = request.Method;

		if (!string.IsNullOrEmpty(request.NewApiKey))
			config.ApiKey = request.NewApiKey;
		else if (request.Regenerate)
			config.ApiKey = Guid.NewGuid().ToString("N");

		if (request.RegenerateFeedToken)
			config.FeedToken = Guid.NewGuid().ToString("N");

		await _databaseContext.SaveChangesAsync();

		_securityConfigStore.Set(config);

		return config;
	}
}
