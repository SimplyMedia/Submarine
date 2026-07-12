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
	private readonly INotificationSenderFactory _notificationSenderFactory;

	public ConnectionService(IConnectionRepository repository, IMediaServerClientFactory clientFactory,
		INotificationSenderFactory notificationSenderFactory)
	{
		_repository = repository;
		_clientFactory = clientFactory;
		_notificationSenderFactory = notificationSenderFactory;
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
		var connection = request.ToConnection();

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
		if (request.OnGrab != null)
			connection.OnGrab = request.OnGrab.Value;
		if (request.OnImport != null)
			connection.OnImport = request.OnImport.Value;
		if (request.OnRename != null)
			connection.OnRename = request.OnRename.Value;
		if (request.Tags != null)
			connection.Tags = request.Tags;

		ApplyTypedUpdate(connection, request);

		await _repository.UpdateAsync(connection);

		return connection;
	}

	private static void ApplyTypedUpdate(Connection connection, UpdateConnectionRequest request)
	{
		switch (connection)
		{
			case DiscordConnection discord:
				if (request.WebhookUrl != null)
					discord.WebhookUrl = request.WebhookUrl;
				break;
			case TelegramConnection telegram:
				if (request.BotToken != null)
					telegram.BotToken = request.BotToken;
				if (request.ChatId != null)
					telegram.ChatId = request.ChatId;
				break;
			case WebhookConnection webhook:
				if (request.Url != null)
					webhook.Url = request.Url;
				if (request.Method != null)
					webhook.Method = request.Method;
				if (request.Username != null)
					webhook.Username = request.Username;
				if (request.Password != null)
					webhook.Password = request.Password;
				break;
			default:
				if (request.Host != null)
					connection.Host = request.Host;
				if (request.Port != null)
					connection.Port = request.Port.Value;
				if (request.UseSsl != null)
					connection.UseSsl = request.UseSsl.Value;
				if (request.ApiKey != null)
					connection.ApiKey = request.ApiKey;
				break;
		}
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

		if (connection is DiscordConnection or TelegramConnection or WebhookConnection)
			await _notificationSenderFactory.Create(connection).TestAsync(cancellationToken);
		else
			await _clientFactory.Create(connection).TestAsync(cancellationToken);
	}
}
