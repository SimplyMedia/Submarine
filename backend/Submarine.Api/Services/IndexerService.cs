using Submarine.Api.Clients;
using Submarine.Api.Exceptions;
using Submarine.Api.Extensions;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;
using Submarine.Core.Indexer.Torznab;
using Submarine.Core.Provider;

namespace Submarine.Api.Services;

public class IndexerService
{
	private readonly IProviderRepository _repository;
	private readonly TorznabHttpClient _torznabHttpClient;

	public IndexerService(IProviderRepository repository, TorznabHttpClient torznabHttpClient)
	{
		_repository = repository;
		_torznabHttpClient = torznabHttpClient;
	}

	public Task<PagedResult<Provider>> GetPagedAsync(int page, int pageSize)
		=> _repository.Query()
			.Where(p => p is TorznabIndexer || p is NewznabIndexer)
			.OrderBy(p => p.Priority)
			.ThenBy(p => p.Id)
			.ToPagedResultAsync(page, pageSize);

	public async Task<TorznabCapabilities> TestAsync(int id, CancellationToken cancellationToken)
	{
		var provider = await _repository.FirstByConditionAsync(p => p.Id == id);

		if (provider is not (TorznabIndexer or NewznabIndexer))
			throw new NotFoundException();

		return await _torznabHttpClient.GetCapabilitiesAsync(provider, cancellationToken);
	}
}
