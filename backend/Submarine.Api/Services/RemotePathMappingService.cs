using Submarine.Api.Exceptions;
using Submarine.Api.Extensions;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;
using Submarine.Core.Download;

namespace Submarine.Api.Services;

public class RemotePathMappingService
{
	private readonly IRemotePathMappingRepository _repository;

	public RemotePathMappingService(IRemotePathMappingRepository repository)
		=> _repository = repository;

	public Task<PagedResult<RemotePathMapping>> GetAllAsync(int page, int pageSize)
		=> _repository.Query().ToPagedResultAsync(page, pageSize);

	public async Task<RemotePathMapping> GetAsync(int id)
	{
		var mapping = await _repository.FirstByConditionAsync(m => m.Id == id);

		if (mapping == null)
			throw new NotFoundException();

		return mapping;
	}

	public async Task<RemotePathMapping> CreateAsync(CreateRemotePathMappingRequest request)
	{
		if (!Directory.Exists(request.LocalPath))
			throw new BadRequestException($"Directory '{request.LocalPath}' does not exist");

		var mapping = new RemotePathMapping
		{
			Host = request.Host,
			RemotePath = request.RemotePath,
			LocalPath = request.LocalPath
		};

		await _repository.CreateAsync(mapping);

		return mapping;
	}

	public async Task<RemotePathMapping> UpdateAsync(int id, UpdateRemotePathMappingRequest request)
	{
		var mapping = await GetAsync(id);

		mapping.Host = request.Host;
		mapping.RemotePath = request.RemotePath;
		mapping.LocalPath = request.LocalPath;

		await _repository.UpdateAsync(mapping);

		return mapping;
	}

	public async Task<RemotePathMapping> DeleteAsync(int id)
	{
		var mapping = await GetAsync(id);

		await _repository.DeleteAsync(mapping);

		return mapping;
	}
}
