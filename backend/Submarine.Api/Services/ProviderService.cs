using Submarine.Api.Exceptions;
using Submarine.Api.Models.Request;
using Submarine.Api.Repository;
using Submarine.Core.Provider;

namespace Submarine.Api.Services;

public class ProviderService
{
	private readonly ILogger<ProviderService> _logger;
	private readonly IProviderRepository _repository;

	public ProviderService(ILogger<ProviderService> logger, IProviderRepository repository)
	{
		_logger = logger;
		_repository = repository;
	}

	public async Task<Provider> CreateAsync(CreateProviderRequest request)
	{
		var provider = request.ToProvider();
		await _repository.CreateAsync(provider);

		return provider;
	}

	public Task<List<Provider>> GetAllAsync()
		=> _repository.FindAllAsync();

	public async Task<Provider> GetAsync(int id)
	{
		var provider = await _repository.FirstByConditionAsync(p => p.Id == id);

		if (provider == null)
			throw new NotFoundException();

		return provider;
	}

	public async Task<Provider> UpdateAsync(int id, UpdateProviderRequest update)
	{
		var provider = await _repository.FirstByConditionAsync(p => p.Id == id);

		if (provider == null)
			throw new NotFoundException();

		if (update.Mode != null)
			provider.Mode = (ProviderMode)update.Mode;
		if (update.Name != null)
			provider.Name = update.Name;
		if (update.Priority != null)
			provider.Priority = (short)update.Priority;
		if (update.Url != null)
			provider.Url = update.Url;
		if (update.ApiKey != null)
			provider.ApiKey = update.ApiKey;

		await _repository.UpdateAsync(provider);

		return provider;
	}

	public async Task<Provider> DeleteAsync(int id)
	{
		var provider = await _repository.FirstByConditionAsync(p => p.Id == id);

		if (provider == null)
			throw new NotFoundException();

		await _repository.DeleteAsync(provider);

		return provider;
	}
}
