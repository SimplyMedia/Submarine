using Submarine.Api.Clients;
using Submarine.Api.Exceptions;
using Submarine.Api.Extensions;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;
using Submarine.Core.Notification;

namespace Submarine.Api.Services;

public class ConnectionService
{
	private readonly IConnectionRepository _repository;
	private readonly IMediaServerClientFactory _clientFactory;

	public ConnectionService(IConnectionRepository repository, IMediaServerClientFactory clientFactory)
	{
		_repository = repository;
		_clientFactory = clientFactory;
	}

	public Task<PagedResult<Connection>> GetAllAsync(int page, int pageSize)
		=> _repository.Query().OrderBy(c => c.Id).ToPagedResultAsync(page, pageSize);

	public async Task<Connection> GetAsync(int id)
	{
		var connection = await _repository.FirstByConditionAsync(c => c.Id == id);

		if (connection == null)
			throw new NotFoundException();

		return connection;
	}

	public async Task<Connection> CreateAsync(CreateConnectionRequest request)
	{
		var connection = new Connection
		{
			Name = request.Name,
			Type = request.Type,
			Enable = request.Enable,
			Host = request.Host,
			Port = request.Port,
			UseSsl = request.UseSsl,
			ApiKey = request.ApiKey,
			OnGrab = request.OnGrab,
			OnImport = request.OnImport,
			OnRename = request.OnRename,
			Tags = request.Tags
		};

		await _repository.CreateAsync(connection);

		return connection;
	}

	public async Task<Connection> UpdateAsync(int id, UpdateConnectionRequest request)
	{
		var connection = await GetAsync(id);

		if (request.Name != null)
			connection.Name = request.Name;
		if (request.Enable != null)
			connection.Enable = request.Enable.Value;
		if (request.Host != null)
			connection.Host = request.Host;
		if (request.Port != null)
			connection.Port = request.Port.Value;
		if (request.UseSsl != null)
			connection.UseSsl = request.UseSsl.Value;
		if (request.ApiKey != null)
			connection.ApiKey = request.ApiKey;
		if (request.OnGrab != null)
			connection.OnGrab = request.OnGrab.Value;
		if (request.OnImport != null)
			connection.OnImport = request.OnImport.Value;
		if (request.OnRename != null)
			connection.OnRename = request.OnRename.Value;
		if (request.Tags != null)
			connection.Tags = request.Tags;

		await _repository.UpdateAsync(connection);

		return connection;
	}

	public async Task<Connection> DeleteAsync(int id)
	{
		var connection = await GetAsync(id);

		await _repository.DeleteAsync(connection);

		return connection;
	}

	public async Task TestAsync(int id, CancellationToken cancellationToken)
	{
		var connection = await GetAsync(id);

		await _clientFactory.Create(connection).TestAsync(cancellationToken);
	}
}
