using Submarine.Api.Exceptions;
using Submarine.Api.Extensions;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;
using Submarine.Core.Download;

namespace Submarine.Api.Services;

public class BlocklistService
{
	private readonly IBlocklistRepository _repository;

	public BlocklistService(IBlocklistRepository repository)
		=> _repository = repository;

	public Task<PagedResult<BlocklistItem>> GetPagedAsync(int page, int pageSize)
		=> _repository.Query().OrderByDescending(b => b.Id).ToPagedResultAsync(page, pageSize);

	public Task<BlocklistItem> AddAsync(BlocklistItem item)
		=> _repository.CreateAsync(item);

	public async Task<bool> IsBlockedAsync(string? guid, string title)
		=> await _repository.FirstByConditionAsync(b =>
			(guid != null && b.Guid == guid) || b.ReleaseTitle == title) != null;

	public async Task<BlocklistItem> DeleteAsync(int id)
	{
		var item = await _repository.FirstByConditionAsync(b => b.Id == id);

		if (item == null)
			throw new NotFoundException();

		await _repository.DeleteAsync(item);

		return item;
	}

	public async Task DeleteAllAsync()
		=> await _repository.DeleteAsync(await _repository.FindAllAsync());
}
