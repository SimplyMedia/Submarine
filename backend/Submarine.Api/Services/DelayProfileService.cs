using Microsoft.EntityFrameworkCore;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Request;
using Submarine.Api.Repository;
using Submarine.Core.Profile;
using Submarine.Core.Provider;

namespace Submarine.Api.Services;

public class DelayProfileService
{
	private readonly IDelayProfileRepository _repository;

	public DelayProfileService(IDelayProfileRepository repository)
		=> _repository = repository;

	public async Task<List<DelayProfile>> GetAllAsync()
	{
		await EnsureDefaultAsync();

		return await _repository.Query().OrderBy(p => p.Order).ToListAsync();
	}

	public async Task<DelayProfile> GetAsync(int id)
	{
		var profile = await _repository.FirstByConditionAsync(p => p.Id == id);

		if (profile == null)
			throw new NotFoundException();

		return profile;
	}

	public async Task<DelayProfile> CreateAsync(CreateDelayProfileRequest request)
	{
		var profile = new DelayProfile
		{
			Name = request.Name,
			PreferredProtocol = request.PreferredProtocol,
			UsenetDelayMinutes = request.UsenetDelayMinutes,
			TorrentDelayMinutes = request.TorrentDelayMinutes,
			BypassIfHighestQuality = request.BypassIfHighestQuality,
			Order = request.Order,
			Tags = request.Tags
		};

		await _repository.CreateAsync(profile);

		return profile;
	}

	public async Task<DelayProfile> UpdateAsync(int id, UpdateDelayProfileRequest request)
	{
		var profile = await GetAsync(id);

		profile.Name = request.Name;
		profile.PreferredProtocol = request.PreferredProtocol;
		profile.UsenetDelayMinutes = request.UsenetDelayMinutes;
		profile.TorrentDelayMinutes = request.TorrentDelayMinutes;
		profile.BypassIfHighestQuality = request.BypassIfHighestQuality;
		profile.Order = request.Order;
		profile.Tags = request.Tags;

		await _repository.UpdateAsync(profile);

		return profile;
	}

	public async Task<DelayProfile> DeleteAsync(int id)
	{
		var profile = await GetAsync(id);

		if (IsDefault(profile))
			throw new BadRequestException("The default delay profile cannot be deleted");

		await _repository.DeleteAsync(profile);

		return profile;
	}

	private async Task EnsureDefaultAsync()
	{
		var profiles = await _repository.FindAllAsync();

		if (profiles.Any(IsDefault))
			return;

		await _repository.CreateAsync(new DelayProfile
		{
			Name = "Default",
			PreferredProtocol = Protocol.USENET,
			UsenetDelayMinutes = 0,
			TorrentDelayMinutes = 0,
			BypassIfHighestQuality = false,
			Order = int.MaxValue,
			Tags = new List<string>()
		});
	}

	private static bool IsDefault(DelayProfile profile)
		=> profile.Order == int.MaxValue && profile.Tags.Count == 0;
}
