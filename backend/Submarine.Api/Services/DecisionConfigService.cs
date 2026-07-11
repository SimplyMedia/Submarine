using Submarine.Api.Exceptions;
using Submarine.Api.Extensions;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;
using Submarine.Core.DecisionEngine.CustomFormats;
using Submarine.Core.DecisionEngine.Filter;

namespace Submarine.Api.Services;

public class DecisionConfigService
{
	private readonly IReleaseFilterRepository _filterRepository;
	private readonly ICustomFormatRepository _formatRepository;

	public DecisionConfigService(IReleaseFilterRepository filterRepository, ICustomFormatRepository formatRepository)
	{
		_filterRepository = filterRepository;
		_formatRepository = formatRepository;
	}

	public Task<PagedResult<ReleaseFilterConfig>> GetAllFiltersAsync(int page, int pageSize)
		=> _filterRepository.Query().OrderBy(f => f.Id).ToPagedResultAsync(page, pageSize);

	public async Task<ReleaseFilterConfig> GetFilterAsync(int id)
	{
		var filter = await _filterRepository.FirstByConditionAsync(f => f.Id == id);

		if (filter == null)
			throw new NotFoundException();

		return filter;
	}

	public async Task<ReleaseFilterConfig> CreateFilterAsync(CreateReleaseFilterRequest request)
	{
		var filter = new ReleaseFilterConfig
		{
			Field = request.Field,
			Values = request.Values,
			Mode = request.Mode,
			Tier = request.Tier
		};

		await _filterRepository.CreateAsync(filter);

		return filter;
	}

	public async Task<ReleaseFilterConfig> UpdateFilterAsync(int id, UpdateReleaseFilterRequest request)
	{
		var filter = await GetFilterAsync(id);

		filter.Field = request.Field;
		filter.Values = request.Values;
		filter.Mode = request.Mode;
		filter.Tier = request.Tier;

		await _filterRepository.UpdateAsync(filter);

		return filter;
	}

	public async Task<ReleaseFilterConfig> DeleteFilterAsync(int id)
	{
		var filter = await GetFilterAsync(id);

		await _filterRepository.DeleteAsync(filter);

		return filter;
	}

	public Task<PagedResult<CustomFormatConfig>> GetAllFormatsAsync(int page, int pageSize)
		=> _formatRepository.Query().OrderBy(f => f.Id).ToPagedResultAsync(page, pageSize);

	public async Task<CustomFormatConfig> GetFormatAsync(int id)
	{
		var format = await _formatRepository.FirstByConditionAsync(f => f.Id == id);

		if (format == null)
			throw new NotFoundException();

		return format;
	}

	public async Task<CustomFormatConfig> CreateFormatAsync(CreateCustomFormatRequest request)
	{
		var format = new CustomFormatConfig
		{
			Name = request.Name,
			Conditions = request.Conditions
		};

		await _formatRepository.CreateAsync(format);

		return format;
	}

	public async Task<CustomFormatConfig> UpdateFormatAsync(int id, UpdateCustomFormatRequest request)
	{
		var format = await GetFormatAsync(id);

		format.Name = request.Name;
		format.Conditions = request.Conditions;

		await _formatRepository.UpdateAsync(format);

		return format;
	}

	public async Task<CustomFormatConfig> DeleteFormatAsync(int id)
	{
		var format = await GetFormatAsync(id);

		await _formatRepository.DeleteAsync(format);

		return format;
	}
}
