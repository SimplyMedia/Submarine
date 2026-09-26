using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.ScheduledTasks;

/// <summary>
///     Scheduled task management.
/// </summary>
public sealed class ScheduledTasksModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/scheduled-tasks");
		group.MapGet("/", ListAsync);
		group.MapPost("/", CreateAsync);
		group.MapPost("/{id:int}/run", RunAsync);
	}

	private static async Task<Ok<PagedResult<ScheduledTask>>> ListAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		CancellationToken cancellationToken)
		=> TypedResults.Ok(await PagedResult<ScheduledTask>.CreateAsync(db.ScheduledTasks, query, x => x, cancellationToken));

	private static async Task<Results<Created<ScheduledTask>, ProblemHttpResult>> CreateAsync(
		SubmarineDbContext db,
		IValidator<CreateScheduledTaskRequest> validator,
		CreateScheduledTaskRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		if (await db.ScheduledTasks.AnyAsync(x => x.Name == request.Name, cancellationToken))
		{
			return TypedResults.Problem(
				statusCode: StatusCodes.Status409Conflict,
				title: $"A scheduled task named '{request.Name}' already exists");
		}

		var task = new ScheduledTask
		{
			Name = request.Name,
			IntervalMinutes = request.IntervalMinutes
		};
		db.ScheduledTasks.Add(task);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Created($"/api/v1/scheduled-tasks/{task.Id}", task);
	}

	private static async Task<Results<Created<Command>, ProblemHttpResult>> RunAsync(
		int id,
		SubmarineDbContext db,
		ICommandQueue queue,
		CommandRegistry registry,
		CancellationToken cancellationToken)
	{
		var task = await db.ScheduledTasks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (task is null)
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Scheduled task not found");
		}

		if (!registry.TryResolve(task.Name, out var commandType))
		{
			return TypedResults.Problem(
				statusCode: StatusCodes.Status400BadRequest,
				title: $"No command registered for '{task.Name}'");
		}

		try
		{
			var command = (ICommand)Activator.CreateInstance(commandType)!;
			var row = await queue.EnqueueAsync(command, CommandTrigger.MANUAL, cancellationToken: cancellationToken);
			return TypedResults.Created($"/api/v1/commands/{row.Id}", row);
		}
		catch (MissingMethodException)
		{
			return TypedResults.Problem(
				statusCode: StatusCodes.Status400BadRequest,
				title: $"Command '{task.Name}' cannot be triggered manually");
		}
	}
}

/// <summary>Create scheduled task request.</summary>
/// <param name="Name">Command name.</param>
/// <param name="IntervalMinutes">Interval in minutes.</param>
public sealed record CreateScheduledTaskRequest(string Name, int IntervalMinutes);

/// <summary>Validator for <see cref="CreateScheduledTaskRequest" />.</summary>
public sealed class CreateScheduledTaskRequestValidator : AbstractValidator<CreateScheduledTaskRequest>
{
	/// <inheritdoc />
	public CreateScheduledTaskRequestValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
		RuleFor(x => x.IntervalMinutes).InclusiveBetween(1, 525600);
	}
}
