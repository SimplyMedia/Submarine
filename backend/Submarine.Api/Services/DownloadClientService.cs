using Submarine.Api.Exceptions;
using Submarine.Api.Extensions;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;
using Submarine.Core.Download;

namespace Submarine.Api.Services;

public class DownloadClientService
{
	private readonly IDownloadClientRepository _repository;
	private readonly DownloadClientFactory _factory;

	public DownloadClientService(IDownloadClientRepository repository, DownloadClientFactory factory)
	{
		_repository = repository;
		_factory = factory;
	}

	public Task<PagedResult<DownloadClientConfig>> GetAllAsync(int page, int pageSize)
		=> _repository.Query().OrderBy(c => c.Priority).ThenBy(c => c.Id).ToPagedResultAsync(page, pageSize);

	public async Task<DownloadClientConfig> GetAsync(int id)
	{
		var config = await _repository.FirstByConditionAsync(c => c.Id == id);

		if (config == null)
			throw new NotFoundException();

		return config;
	}

	public async Task<DownloadClientConfig> CreateAsync(CreateDownloadClientRequest request)
	{
		_factory.ValidateSettings(request.Type, request.SettingsJson);

		var config = new DownloadClientConfig
		{
			Name = request.Name,
			Type = request.Type,
			Enable = request.Enable,
			Priority = request.Priority,
			SettingsJson = request.SettingsJson,
			Tags = request.Tags
		};

		await _repository.CreateAsync(config);

		return config;
	}

	public async Task<DownloadClientConfig> UpdateAsync(int id, UpdateDownloadClientRequest request)
	{
		var config = await GetAsync(id);

		if (request.Name != null)
			config.Name = request.Name;
		if (request.Enable != null)
			config.Enable = request.Enable.Value;
		if (request.Priority != null)
			config.Priority = request.Priority.Value;
		if (request.SettingsJson != null)
		{
			_factory.ValidateSettings(config.Type, request.SettingsJson);
			config.SettingsJson = request.SettingsJson;
		}

		if (request.Tags != null)
			config.Tags = request.Tags;

		await _repository.UpdateAsync(config);

		return config;
	}

	public async Task<DownloadClientConfig> DeleteAsync(int id)
	{
		var config = await GetAsync(id);

		await _repository.DeleteAsync(config);

		return config;
	}

	public async Task TestAsync(int id, CancellationToken cancellationToken)
	{
		var config = await GetAsync(id);

		await _factory.Create(config).TestAsync(cancellationToken);
	}
}
