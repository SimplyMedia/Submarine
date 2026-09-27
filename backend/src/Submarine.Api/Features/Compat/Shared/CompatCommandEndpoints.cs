using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Core.Commands;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Shared;

/// <summary>Command routes shared by Sonarr and Radarr compatibility facades.</summary>
public sealed class CompatCommandEndpoints : IEndpointModule
{
	public void Map(IEndpointRouteBuilder endpoints)
	{
		MapFacade(endpoints, "sonarr");
		MapFacade(endpoints, "radarr");
	}

	private static void MapFacade(IEndpointRouteBuilder endpoints, string facade)
	{
		var group = CompatRoutes.CreateRestGroup(endpoints, facade, "v3");
		group.MapPost("/command", (HttpRequest request, ICommandQueue queue, CancellationToken ct) => EnqueueAsync(facade, request, queue, ct))
			.Accepts<JsonElement>("application/json");
		group.MapGet("/command", (SubmarineDbContext db, CancellationToken ct) => ListAsync(facade, db, ct));
		group.MapGet("/command/{id:int}", (int id, SubmarineDbContext db, CancellationToken ct) => GetAsync(facade, id, db, ct));
		group.MapDelete("/command/{id:int}", (int id, SubmarineDbContext db, ICommandCancellation cancellation, CancellationToken ct) => CancelAsync(facade, id, db, cancellation, ct));
	}

	private static async Task<IResult> EnqueueAsync(
		string facade,
		HttpRequest request,
		ICommandQueue queue,
		CancellationToken cancellationToken)
	{
		JsonDocument document;
		try
		{
			document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
		}
		catch (JsonException)
		{
			return Error("Command body must be valid JSON.", StatusCodes.Status400BadRequest);
		}

		using (document)
		{
			if (!CompatCommandAdapter.TryCreate(document.RootElement, facade, out var nativeCommand, out var error))
			{
				return Error(error, StatusCodes.Status400BadRequest);
			}

			var row = await queue.EnqueueAsync(nativeCommand, CommandTrigger.MANUAL, cancellationToken: cancellationToken);
			if (!CompatCommandAdapter.TryProject(row, facade, out var dto))
			{
				return Error("The queued command has no compatibility representation.", StatusCodes.Status500InternalServerError);
			}

			return Results.Json(dto, CompatJson.Options, statusCode: StatusCodes.Status201Created);
		}
	}

	private static async Task<IResult> ListAsync(string facade, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var rows = await db.Commands.AsNoTracking().OrderByDescending(row => row.Id).ToListAsync(cancellationToken);
		var commands = rows.Where(row => CompatCommandAdapter.TryProject(row, facade, out _))
			.Select(row =>
			{
				CompatCommandAdapter.TryProject(row, facade, out var dto);
				return dto;
			})
			.ToArray();
		return Results.Json(commands, CompatJson.Options);
	}

	private static async Task<IResult> GetAsync(string facade, int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var row = await db.Commands.AsNoTracking().FirstOrDefaultAsync(command => command.Id == id, cancellationToken);
		return row is not null && CompatCommandAdapter.TryProject(row, facade, out var dto)
			? Results.Json(dto, CompatJson.Options)
			: Error("Command not found.", StatusCodes.Status404NotFound);
	}

	private static async Task<IResult> CancelAsync(
		string facade,
		int id,
		SubmarineDbContext db,
		ICommandCancellation cancellation,
		CancellationToken cancellationToken)
	{
		var row = await db.Commands.AsNoTracking().FirstOrDefaultAsync(command => command.Id == id, cancellationToken);
		if (row is null || !CompatCommandAdapter.TryProject(row, facade, out _))
			return Error("Command not found.", StatusCodes.Status404NotFound);
		if (row.Status is not (CommandStatus.QUEUED or CommandStatus.RUNNING))
			return Error("Command is not active.", StatusCodes.Status409Conflict);
		await cancellation.TryCancelAsync(id, cancellationToken);
		return Results.NoContent();
	}

	private static IResult Error(string message, int statusCode)
		=> Results.Json(new { message }, CompatJson.Options, statusCode: statusCode);
}
