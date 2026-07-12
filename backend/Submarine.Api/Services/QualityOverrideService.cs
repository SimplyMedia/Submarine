using Submarine.Api.Exceptions;
using Submarine.Api.Models.Request;
using Submarine.Api.Repository;
using Submarine.Core.Quality;

namespace Submarine.Api.Services;

public class QualityOverrideService
{
	private readonly IQualityOverrideRepository _repository;

	private readonly QualityOverrideStore _store;

	public QualityOverrideService(IQualityOverrideRepository repository, QualityOverrideStore store)
	{
		_repository = repository;
		_store = store;
	}

	public Task<List<ReleaseGroupQualityOverride>> GetAllAsync()
		=> _repository.FindAllAsync();

	public async Task<ReleaseGroupQualityOverride> CreateAsync(CreateQualityOverrideRequest request)
	{
		var existing = await _repository.FirstByConditionAsync(o =>
			o.ReleaseGroup.ToLower() == request.ReleaseGroup.ToLower());

		if (existing != null)
			throw new ConflictException($"Override for release group '{request.ReleaseGroup}' already exists");

		var qualityOverride = new ReleaseGroupQualityOverride
		{
			ReleaseGroup = request.ReleaseGroup,
			Source = request.Source
		};

		await _repository.CreateAsync(qualityOverride);
		await ReloadStoreAsync();

		return qualityOverride;
	}

	public async Task<ReleaseGroupQualityOverride> UpdateAsync(int id, UpdateQualityOverrideRequest request)
	{
		var qualityOverride = await _repository.FirstByConditionAsync(o => o.Id == id);

		if (qualityOverride == null)
			throw new NotFoundException();

		var duplicate = await _repository.FirstByConditionAsync(o =>
			o.Id != id && o.ReleaseGroup.ToLower() == request.ReleaseGroup.ToLower());

		if (duplicate != null)
			throw new ConflictException($"Override for release group '{request.ReleaseGroup}' already exists");

		qualityOverride.ReleaseGroup = request.ReleaseGroup;
		qualityOverride.Source = request.Source;

		await _repository.UpdateAsync(qualityOverride);
		await ReloadStoreAsync();

		return qualityOverride;
	}

	public async Task<ReleaseGroupQualityOverride> DeleteAsync(int id)
	{
		var qualityOverride = await _repository.FirstByConditionAsync(o => o.Id == id);

		if (qualityOverride == null)
			throw new NotFoundException();

		await _repository.DeleteAsync(qualityOverride);
		await ReloadStoreAsync();

		return qualityOverride;
	}

	private async Task ReloadStoreAsync()
		=> _store.Reload(await _repository.FindAllAsync());
}
