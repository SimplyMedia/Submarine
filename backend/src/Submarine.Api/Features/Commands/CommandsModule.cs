using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Common;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Commands;

/// <summary>
///     Command queue endpoints.
/// </summary>
public sealed class CommandsModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/commands");
		group.MapPost("/", EnqueueAsync).Accepts<EnqueueCommandRequest>("application/json");
		group.MapGet("/", ListAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapDelete("/{id:int}", CancelAsync);
	}

	internal static async Task<Results<Created<Command>, ProblemHttpResult>> EnqueueAsync(
		HttpRequest request,
		ICommandQueue queue,
		CommandRegistry registry,
		CancellationToken cancellationToken)
	{
		JsonDocument document;
		try
		{
			document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
		}
		catch (JsonException)
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Body must be valid JSON");
		}

		using var _ = document;
		var root = document.RootElement;
		if (root.ValueKind != JsonValueKind.Object
			|| !root.TryGetProperty("name", out var nameElement)
			|| nameElement.GetString() is not { Length: > 0 } name)
		{
			return TypedResults.Problem(
				statusCode: StatusCodes.Status400BadRequest,
				title: "Body must be a JSON object with a name property");
		}

		Type commandType;
		try
		{
			commandType = registry.Resolve(name);
		}
		catch (KeyNotFoundException)
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: $"Unknown command '{name}'");
		}

		ICommand command;
		try
		{
			command = (ICommand)root.Deserialize(commandType, SubmarineJson.Default)!;
		}
		catch (JsonException exception)
		{
			return TypedResults.Problem(
				statusCode: StatusCodes.Status400BadRequest,
				title: $"Command body does not match '{name}'",
				detail: exception.Message);
		}

		var row = await queue.EnqueueAsync(command, CommandTrigger.MANUAL, cancellationToken: cancellationToken);
		return TypedResults.Created($"/api/v1/commands/{row.Id}", row);
	}

	private static async Task<Ok<PagedResult<Command>>> ListAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		CancellationToken cancellationToken)
		=> TypedResults.Ok(await PagedResult<Command>.CreateAsync(db.Commands, query, x => x, cancellationToken));

	private static async Task<Results<Ok<Command>, ProblemHttpResult>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var row = await db.Commands.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		return row is null
			? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Command not found")
			: TypedResults.Ok(row);
	}

	private static async Task<Results<NoContent, ProblemHttpResult>> CancelAsync(
		int id,
		SubmarineDbContext db,
		ICommandCancellation cancellation,
		CancellationToken cancellationToken)
	{
		var row = await db.Commands.FindAsync([id], cancellationToken);
		if (row is null)
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Command not found");
		}

		if (row.Status is not (CommandStatus.QUEUED or CommandStatus.RUNNING))
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Command is not active");
		}

		await cancellation.TryCancelAsync(id, cancellationToken);
		return TypedResults.NoContent();
	}
}

/// <summary>
///     Body of an enqueue request: the command name plus the command's own properties.
/// </summary>
/// <param name="Name">Command name, the record name without the Command suffix, for example RssSync.</param>
public sealed record EnqueueCommandRequest(string Name)
{
	/// <summary>Command-specific properties, for example seriesId or episodeIds.</summary>
	[JsonExtensionData]
	public Dictionary<string, JsonElement>? Parameters { get; init; }
}
