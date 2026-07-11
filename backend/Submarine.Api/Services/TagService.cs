using Submarine.Api.Exceptions;
using Submarine.Api.Extensions;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;

namespace Submarine.Api.Services;

public class TagService
{
	private readonly ITagRepository _repository;

	public TagService(ITagRepository repository)
		=> _repository = repository;

	public Task<PagedResult<Core.Tag.Tag>> GetAllAsync(int page, int pageSize)
		=> _repository.Query().ToPagedResultAsync(page, pageSize);

	public async Task<Core.Tag.Tag> GetAsync(int id)
	{
		var tag = await _repository.FirstByConditionAsync(t => t.Id == id);

		if (tag == null)
			throw new NotFoundException();

		return tag;
	}

	public async Task<Core.Tag.Tag> CreateAsync(CreateTagRequest request)
	{
		var existing = await _repository.FirstByConditionAsync(t => t.Label == request.Label);

		if (existing != null)
			throw new ConflictException($"Tag with label '{request.Label}' already exists");

		var tag = new Core.Tag.Tag { Label = request.Label };

		await _repository.CreateAsync(tag);

		return tag;
	}

	public async Task<Core.Tag.Tag> DeleteAsync(int id)
	{
		var tag = await _repository.FirstByConditionAsync(t => t.Id == id);

		if (tag == null)
			throw new NotFoundException();

		await _repository.DeleteAsync(tag);

		return tag;
	}
}
