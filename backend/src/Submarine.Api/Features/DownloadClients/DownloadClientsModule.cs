using System.Reflection;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.DownloadClients;

/// <summary>
///     Download client CRUD, connection testing and settings schema.
/// </summary>
public sealed class DownloadClientsModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/download-clients");
		group.MapGet("/", ListAsync);
		group.MapGet("/schema", GetSchema);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPost("/", CreateAsync);
		group.MapPost("/test", TestUnsavedAsync);
		group.MapPost("/{id:int}/test", TestAsync);
		group.MapPut("/bulk", BulkUpdateAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<PagedResult<DownloadClientDto>>> ListAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		CancellationToken cancellationToken)
	{
		var clients = await db.DownloadClients
			.AsNoTracking()
			.Include(client => client.Tags)
			.OrderBy(client => client.Priority)
			.ToListAsync(cancellationToken);

		return TypedResults.Ok(await PagedResult<DownloadClientDto>.CreateAsync(
			clients.Select(ToDto).AsQueryable(),
			query,
			cancellationToken));
	}

	private static Ok<IReadOnlyList<DownloadClientSchema>> GetSchema()
		=> TypedResults.Ok(BuildSchema());

	private static async Task<Results<Ok<DownloadClientDto>, NotFound>> GetAsync(
		int id,
		SubmarineDbContext db,
		CancellationToken cancellationToken)
	{
		var client = await db.DownloadClients.AsNoTracking().Include(x => x.Tags)
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		return client is null ? TypedResults.NotFound() : TypedResults.Ok(ToDto(client));
	}

	private static async Task<Created<DownloadClientDto>> CreateAsync(
		SubmarineDbContext db,
		IValidator<DownloadClientRequest> validator,
		[FromBody] DownloadClientRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		ValidateSettingsOrThrow(request.Type, request.Settings);

		var client = new DownloadClient();
		await ApplyAsync(client, request, db, cancellationToken);

		db.DownloadClients.Add(client);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Created($"/api/v1/download-clients/{client.Id}", ToDto(client));
	}

	private static async Task<Results<Ok<DownloadClientDto>, NotFound>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		IValidator<DownloadClientRequest> validator,
		[FromBody] DownloadClientRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		ValidateSettingsOrThrow(request.Type, request.Settings);

		var client = await db.DownloadClients.Include(x => x.Tags).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (client is null)
		{
			return TypedResults.NotFound();
		}

		await ApplyAsync(client, request, db, cancellationToken);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(ToDto(client));
	}

	private static async Task<Results<NoContent, NotFound>> DeleteAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var client = await db.DownloadClients.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (client is null)
		{
			return TypedResults.NotFound();
		}

		db.DownloadClients.Remove(client);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.NoContent();
	}

	private static async Task<Ok<DownloadClientTestResult>> TestUnsavedAsync(
		IDownloadClientFactory factory,
		[FromBody] DownloadClientTestRequest request,
		CancellationToken cancellationToken)
		=> TypedResults.Ok(await RunTestAsync(factory, request.Type, request.Settings.GetRawText(), 0, request.Type.ToString(), cancellationToken));

	private static async Task<Results<Ok<DownloadClientTestResult>, NotFound>> TestAsync(
		int id,
		SubmarineDbContext db,
		IDownloadClientFactory factory,
		CancellationToken cancellationToken)
	{
		var client = await db.DownloadClients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (client is null)
		{
			return TypedResults.NotFound();
		}

		return TypedResults.Ok(await RunTestAsync(factory, client.Type, client.SettingsJson, client.Id, client.Name, cancellationToken));
	}

	private static async Task<Ok<BulkUpdateResult>> BulkUpdateAsync(
		SubmarineDbContext db,
		IValidator<DownloadClientBulkRequest> validator,
		[FromBody] DownloadClientBulkRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);

		var clients = await db.DownloadClients
			.Include(x => x.Tags)
			.Where(x => request.Ids.Contains(x.Id))
			.ToListAsync(cancellationToken);

		if (request.Tags is not null)
		{
			var tags = await db.Tags.Where(x => request.Tags.TagIds.Contains(x.Id)).ToListAsync(cancellationToken);
			foreach (var client in clients)
			{
				ApplyTagChange(client, request.Tags, tags);
			}
		}

		foreach (var client in clients)
		{
			if (request.Enable.HasValue)
			{
				client.Enable = request.Enable.Value;
			}

			if (request.Priority.HasValue)
			{
				client.Priority = request.Priority.Value;
			}
		}

		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(new BulkUpdateResult(clients.Count));
	}

	private static async Task<DownloadClientTestResult> RunTestAsync(
		IDownloadClientFactory factory,
		DownloadClientType type,
		string settingsJson,
		int id,
		string name,
		CancellationToken cancellationToken)
	{
		var validation = DownloadClientSettingsJson.Validate(type, settingsJson);
		if (!validation.IsValid)
		{
			return new DownloadClientTestResult(false, "Settings are invalid", validation.Errors);
		}

		try
		{
			var client = factory.Create(type, settingsJson, id, name);
			await client.TestAsync(cancellationToken);
			return new DownloadClientTestResult(true, null, EmptyErrors);
		}
		catch (DownloadClientException exception)
		{
			return new DownloadClientTestResult(false, exception.Message, EmptyErrors);
		}
	}

	private static readonly IReadOnlyDictionary<string, string[]> EmptyErrors = new Dictionary<string, string[]>();

	private static async Task ApplyAsync(DownloadClient client, DownloadClientRequest request, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		client.Name = request.Name;
		client.Type = request.Type;
		client.Enable = request.Enable;
		client.Priority = request.Priority;
		client.SettingsJson = request.Settings.GetRawText();
		client.RemoveCompleted = request.RemoveCompleted;
		client.RemoveFailed = request.RemoveFailed;

		if (request.TagIds is not null)
		{
			var tags = await db.Tags.Where(x => request.TagIds.Contains(x.Id)).ToListAsync(cancellationToken);
			client.Tags.Clear();
			foreach (var tag in tags)
			{
				client.Tags.Add(tag);
			}
		}
	}

	private static void ApplyTagChange(DownloadClient client, TagChangeRequest change, List<Tag> tags)
	{
		switch (change.Mode)
		{
			case "add":
				foreach (var tag in tags.Where(tag => client.Tags.All(existing => existing.Id != tag.Id)))
				{
					client.Tags.Add(tag);
				}

				break;
			case "remove":
				foreach (var tag in tags)
				{
					var existing = client.Tags.FirstOrDefault(x => x.Id == tag.Id);
					if (existing is not null)
					{
						client.Tags.Remove(existing);
					}
				}

				break;
			default:
				client.Tags.Clear();
				foreach (var tag in tags)
				{
					client.Tags.Add(tag);
				}

				break;
		}
	}

	private static void ValidateSettingsOrThrow(DownloadClientType type, JsonElement settings)
	{
		var validation = DownloadClientSettingsJson.Validate(type, settings.GetRawText());
		if (!validation.IsValid)
		{
			var failures = validation.Errors
				.SelectMany(error => error.Value.Select(message => new ValidationFailure($"settings.{error.Key}", message)));
			throw new ValidationException(failures);
		}
	}

	private static DownloadClientDto ToDto(DownloadClient client)
		=> new(
			client.Id,
			client.Name,
			client.Type,
			client.Enable,
			client.Priority,
			JsonSerializer.Deserialize<JsonElement>(client.SettingsJson),
			client.RemoveCompleted,
			client.RemoveFailed,
			client.Tags.Select(x => x.Id).ToList());

	private static IReadOnlyList<DownloadClientSchema> BuildSchema()
		=> Enum.GetValues<DownloadClientType>()
			.Select(type => new DownloadClientSchema(type, DownloadClientSettingsSchema.FieldsOf(type)))
			.ToList();
}

/// <summary>A configured download client.</summary>
/// <param name="Id">Id.</param>
/// <param name="Name">Display name.</param>
/// <param name="Type">Client implementation.</param>
/// <param name="Enable">Whether the client is enabled.</param>
/// <param name="Priority">Priority, lower is tried first.</param>
/// <param name="Settings">Implementation specific settings.</param>
/// <param name="RemoveCompleted">Remove completed downloads from the client.</param>
/// <param name="RemoveFailed">Remove failed downloads from the client.</param>
/// <param name="TagIds">Tag ids.</param>
public sealed record DownloadClientDto(
	int Id,
	string Name,
	DownloadClientType Type,
	bool Enable,
	int Priority,
	JsonElement Settings,
	bool RemoveCompleted,
	bool RemoveFailed,
	IReadOnlyList<int> TagIds);

/// <summary>Create or update request for a download client.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Type">Client implementation.</param>
/// <param name="Enable">Whether the client is enabled.</param>
/// <param name="Priority">Priority, lower is tried first.</param>
/// <param name="Settings">Implementation specific settings, validated against <see cref="Type" />.</param>
/// <param name="RemoveCompleted">Remove completed downloads from the client.</param>
/// <param name="RemoveFailed">Remove failed downloads from the client.</param>
/// <param name="TagIds">Tag ids, null keeps existing tags on update.</param>
public sealed record DownloadClientRequest(
	string Name,
	DownloadClientType Type,
	bool Enable,
	int Priority,
	JsonElement Settings,
	bool RemoveCompleted,
	bool RemoveFailed,
	List<int>? TagIds);

/// <summary>Test request for settings not yet saved.</summary>
/// <param name="Type">Client implementation.</param>
/// <param name="Settings">Implementation specific settings.</param>
public sealed record DownloadClientTestRequest(DownloadClientType Type, JsonElement Settings);

/// <summary>Result of a connection test.</summary>
/// <param name="IsValid">Whether the settings are valid and the client is reachable.</param>
/// <param name="Message">Error message, when not valid.</param>
/// <param name="FieldErrors">Field level validation errors, when the settings themselves are invalid.</param>
public sealed record DownloadClientTestResult(bool IsValid, string? Message, IReadOnlyDictionary<string, string[]> FieldErrors);

/// <summary>Bulk edit request.</summary>
/// <param name="Ids">Ids to update.</param>
/// <param name="Enable">New enabled flag, applied to all.</param>
/// <param name="Priority">New priority, applied to all.</param>
/// <param name="Tags">Tag change.</param>
public sealed record DownloadClientBulkRequest(List<int> Ids, bool? Enable, int? Priority, TagChangeRequest? Tags);

/// <summary>Tag change applied in a bulk request.</summary>
/// <param name="Mode">add, remove or replace.</param>
/// <param name="TagIds">Tag ids.</param>
public sealed record TagChangeRequest(string Mode, List<int> TagIds);

/// <summary>Result of a bulk update.</summary>
/// <param name="Updated">Number of rows updated.</param>
public sealed record BulkUpdateResult(int Updated);

/// <summary>Settings schema for one download client type.</summary>
/// <param name="Type">Client implementation.</param>
/// <param name="Fields">Settings fields.</param>
public sealed record DownloadClientSchema(DownloadClientType Type, IReadOnlyList<DownloadClientFieldSchema> Fields);

/// <summary>One settings field descriptor.</summary>
/// <param name="Name">camelCase field name.</param>
/// <param name="ClrType">string, int, double, bool or enum.</param>
/// <param name="Required">Whether the field is a non-nullable reference type.</param>
/// <param name="EnumValues">Allowed values, when <see cref="ClrType" /> is enum.</param>
/// <param name="Default">Default value.</param>
public sealed record DownloadClientFieldSchema(string Name, string ClrType, bool Required, IReadOnlyList<string>? EnumValues, object? Default);

/// <summary>Validator for <see cref="DownloadClientRequest" />.</summary>
public sealed class DownloadClientRequestValidator : AbstractValidator<DownloadClientRequest>
{
	/// <inheritdoc />
	public DownloadClientRequestValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
		RuleFor(x => x.Type).IsInEnum();
		RuleFor(x => x.Priority).GreaterThan(0);
		RuleForEach(x => x.TagIds).GreaterThan(0).When(x => x.TagIds is not null);
	}
}

/// <summary>Validator for <see cref="DownloadClientBulkRequest" />.</summary>
public sealed class DownloadClientBulkRequestValidator : AbstractValidator<DownloadClientBulkRequest>
{
	/// <inheritdoc />
	public DownloadClientBulkRequestValidator()
	{
		RuleFor(x => x.Ids).NotNull().NotEmpty();
		RuleForEach(x => x.Ids).GreaterThan(0);
		RuleFor(x => x.Priority).GreaterThan(0).When(x => x.Priority.HasValue);
		RuleFor(x => x.Tags)
			.Must(tags => tags is null || tags.Mode is "add" or "remove" or "replace")
			.WithMessage("Mode must be add, remove or replace")
			.Must(tags => tags is null || tags.TagIds.Count > 0)
			.WithMessage("TagIds must not be empty")
			.When(x => x.Tags is not null);
	}
}

/// <summary>
///     Reflects download client settings records into field schemas using nullable reference annotations
///     to decide which fields are required.
/// </summary>
internal static class DownloadClientSettingsSchema
{
	private static readonly IReadOnlyDictionary<DownloadClientType, Type> SettingsTypes = new Dictionary<DownloadClientType, Type>
	{
		[DownloadClientType.QBITTORRENT] = typeof(QBittorrentSettings),
		[DownloadClientType.TRANSMISSION] = typeof(TransmissionSettings),
		[DownloadClientType.DELUGE] = typeof(DelugeSettings),
		[DownloadClientType.RTORRENT] = typeof(RTorrentSettings),
		[DownloadClientType.UTORRENT] = typeof(UTorrentSettings),
		[DownloadClientType.ARIA2] = typeof(Aria2Settings),
		[DownloadClientType.FLOOD] = typeof(FloodSettings),
		[DownloadClientType.DOWNLOAD_STATION] = typeof(DownloadStationSettings),
		[DownloadClientType.SABNZBD] = typeof(SabnzbdSettings),
		[DownloadClientType.NZBGET] = typeof(NzbGetSettings),
		[DownloadClientType.TORRENT_BLACKHOLE] = typeof(TorrentBlackholeSettings),
		[DownloadClientType.USENET_BLACKHOLE] = typeof(UsenetBlackholeSettings)
	};

	public static IReadOnlyList<DownloadClientFieldSchema> FieldsOf(DownloadClientType type)
	{
		var settingsType = SettingsTypes[type];
		var instance = Activator.CreateInstance(settingsType)!;
		var context = new NullabilityInfoContext();

		return settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Select(property =>
			{
				var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
				var isEnum = propertyType.IsEnum;
				var required = property.PropertyType == typeof(string) && context.Create(property).ReadState == NullabilityState.NotNull;
				var clrType = isEnum ? "enum" : propertyType switch
				{
					{ } t when t == typeof(int) => "int",
					{ } t when t == typeof(double) => "double",
					{ } t when t == typeof(bool) => "bool",
					_ => "string"
				};

				return new DownloadClientFieldSchema(
					ToCamelCase(property.Name),
					clrType,
					required,
					isEnum ? Enum.GetNames(propertyType) : null,
					property.GetValue(instance));
			})
			.ToList();
	}

	private static string ToCamelCase(string name)
		=> string.IsNullOrEmpty(name) || char.IsLower(name[0]) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
