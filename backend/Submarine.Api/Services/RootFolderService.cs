using Submarine.Api.Exceptions;
using Submarine.Api.Extensions;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;
using Submarine.Core.Library;

namespace Submarine.Api.Services;

public class RootFolderService
{
	private readonly IRootFolderRepository _repository;

	public RootFolderService(IRootFolderRepository repository)
		=> _repository = repository;

	public async Task<PagedResult<RootFolderResponse>> GetAllAsync(int page, int pageSize)
	{
		var result = await _repository.Query().ToPagedResultAsync(page, pageSize);

		var items = result.Items.Select(RootFolderResponse.FromRootFolder).ToList();

		return new PagedResult<RootFolderResponse>(items, result.Page, result.PageSize, result.TotalItems);
	}

	public async Task<RootFolder> GetAsync(int id)
	{
		var rootFolder = await _repository.FirstByConditionAsync(r => r.Id == id);

		if (rootFolder == null)
			throw new NotFoundException();

		return rootFolder;
	}

	public async Task<RootFolder> CreateAsync(CreateRootFolderRequest request)
	{
		if (!Directory.Exists(request.Path))
			throw new BadRequestException($"Directory '{request.Path}' does not exist");

		var existing = await _repository.FirstByConditionAsync(r => r.Path == request.Path);

		if (existing != null)
			throw new ConflictException($"Root folder with path '{request.Path}' already exists");

		var rootFolder = new RootFolder { Path = request.Path, MediaKind = request.MediaKind };

		await _repository.CreateAsync(rootFolder);

		return rootFolder;
	}

	public async Task<RootFolder> DeleteAsync(int id)
	{
		var rootFolder = await _repository.FirstByConditionAsync(r => r.Id == id);

		if (rootFolder == null)
			throw new NotFoundException();

		await _repository.DeleteAsync(rootFolder);

		return rootFolder;
	}
}
