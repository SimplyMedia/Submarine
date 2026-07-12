using Submarine.Api.Exceptions;
using Submarine.Api.Extensions;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;
using Submarine.Core.ImportList;

namespace Submarine.Api.Services;

public class ImportListService
{
	private readonly IImportListRepository _repository;

	public ImportListService(IImportListRepository repository)
		=> _repository = repository;

	public Task<PagedResult<ImportList>> GetAllAsync(int page, int pageSize)
		=> _repository.Query().OrderBy(l => l.Id).ToPagedResultAsync(page, pageSize);

	public async Task<ImportList> GetAsync(int id)
	{
		var list = await _repository.FirstByConditionAsync(l => l.Id == id);

		if (list == null)
			throw new NotFoundException();

		return list;
	}

	public async Task<ImportList> CreateAsync(CreateImportListRequest request)
	{
		var list = new ImportList
		{
			Name = request.Name,
			Type = request.Type,
			Enable = request.Enable,
			SettingsJson = request.SettingsJson,
			MediaKind = request.MediaKind,
			QualityProfileId = request.QualityProfileId,
			LanguageProfileId = request.LanguageProfileId,
			RootFolderId = request.RootFolderId,
			Monitored = request.Monitored,
			Tags = request.Tags
		};

		await _repository.CreateAsync(list);

		return list;
	}

	public async Task<ImportList> UpdateAsync(int id, UpdateImportListRequest request)
	{
		var list = await GetAsync(id);

		if (request.Name != null)
			list.Name = request.Name;
		if (request.Enable != null)
			list.Enable = request.Enable.Value;
		if (request.SettingsJson != null)
			list.SettingsJson = request.SettingsJson;
		if (request.MediaKind != null)
			list.MediaKind = request.MediaKind.Value;
		if (request.QualityProfileId != null)
			list.QualityProfileId = request.QualityProfileId.Value;
		if (request.LanguageProfileId != null)
			list.LanguageProfileId = request.LanguageProfileId.Value;
		if (request.RootFolderId != null)
			list.RootFolderId = request.RootFolderId.Value;
		if (request.Monitored != null)
			list.Monitored = request.Monitored.Value;
		if (request.Tags != null)
			list.Tags = request.Tags;

		await _repository.UpdateAsync(list);

		return list;
	}

	public async Task<ImportList> DeleteAsync(int id)
	{
		var list = await GetAsync(id);

		await _repository.DeleteAsync(list);

		return list;
	}
}
