using Submarine.Api.Exceptions;
using Submarine.Api.Extensions;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;
using Submarine.Core.Profile;

namespace Submarine.Api.Services;

public class ReleaseProfileService
{
	private readonly IReleaseProfileRepository _repository;

	public ReleaseProfileService(IReleaseProfileRepository repository)
		=> _repository = repository;

	public Task<PagedResult<ReleaseProfile>> GetAllAsync(int page, int pageSize)
		=> _repository.Query().ToPagedResultAsync(page, pageSize);

	public async Task<ReleaseProfile> GetAsync(int id)
	{
		var profile = await _repository.FirstByConditionAsync(p => p.Id == id);

		if (profile == null)
			throw new NotFoundException();

		return profile;
	}

	public async Task<ReleaseProfile> CreateAsync(CreateReleaseProfileRequest request)
	{
		var profile = new ReleaseProfile
		{
			Name = request.Name,
			Enabled = request.Enabled,
			Required = request.Required,
			Ignored = request.Ignored,
			Indexer = request.Indexer,
			Tags = request.Tags
		};

		await _repository.CreateAsync(profile);

		return profile;
	}

	public async Task<ReleaseProfile> UpdateAsync(int id, UpdateReleaseProfileRequest request)
	{
		var profile = await GetAsync(id);

		profile.Name = request.Name;
		profile.Enabled = request.Enabled;
		profile.Required = request.Required;
		profile.Ignored = request.Ignored;
		profile.Indexer = request.Indexer;
		profile.Tags = request.Tags;

		await _repository.UpdateAsync(profile);

		return profile;
	}

	public async Task<ReleaseProfile> DeleteAsync(int id)
	{
		var profile = await GetAsync(id);

		await _repository.DeleteAsync(profile);

		return profile;
	}
}
