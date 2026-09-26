using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Modules;
using Submarine.Core.Commands;
using Submarine.Infrastructure.Backups;
using Submarine.Infrastructure.Commands;

namespace Submarine.Api.Features.Backups;

/// <summary>
///     Backup archives: list, create, download, delete and restore.
/// </summary>
public sealed class BackupsModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/backups");
		group.MapGet("/", ListAsync);
		group.MapPost("/", CreateAsync);
		group.MapPost("/restore", RestoreUploadAsync).Accepts<IFormFile>("multipart/form-data");
		group.MapDelete("/{name}", DeleteAsync);
		group.MapGet("/{name}/download", DownloadAsync).Produces(StatusCodes.Status200OK, contentType: "application/zip");
		group.MapPost("/{name}/restore", RestoreFileAsync);
	}

	private static Ok<IReadOnlyList<BackupEntry>> ListAsync(BackupService backups)
		=> TypedResults.Ok(backups.List());

	private static async Task<Created<EnqueueResultDto>> CreateAsync(ICommandQueue queue, CancellationToken cancellationToken)
	{
		var row = await queue.EnqueueAsync(new BackupCommand(), CommandTrigger.MANUAL, cancellationToken: cancellationToken);
		return TypedResults.Created($"/api/v1/commands/{row.Id}", new EnqueueResultDto(row.Id));
	}

	private static IResult DownloadAsync(string name, BackupService backups)
	{
		try
		{
			var stream = backups.Open(name);
			return Results.File(stream, "application/zip", fileDownloadName: name);
		}
		catch (KeyNotFoundException)
		{
			return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: $"Backup {name} does not exist");
		}
		catch (InvalidOperationException)
		{
			return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Invalid backup name");
		}
	}

	private static Results<NoContent, ProblemHttpResult> DeleteAsync(string name, BackupService backups)
	{
		try
		{
			backups.Delete(name);
			return TypedResults.NoContent();
		}
		catch (KeyNotFoundException)
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: $"Backup {name} does not exist");
		}
		catch (InvalidOperationException)
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Invalid backup name");
		}
	}

	private static async Task<Results<Accepted<RestartRequiredDto>, ProblemHttpResult>> RestoreFileAsync(
		string name,
		BackupService backups,
		IHostApplicationLifetime lifetime,
		HttpContext httpContext,
		CancellationToken cancellationToken)
	{
		if (!BackupService.FileNamePattern().IsMatch(name))
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Invalid backup name");
		}

		try
		{
			await using var stream = backups.Open(name);
			await backups.RestoreAsync(stream, cancellationToken);
		}
		catch (NotSupportedException ex)
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status501NotImplemented, title: "Restore not supported", detail: ex.Message);
		}
		catch (InvalidOperationException ex)
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Restore failed", detail: ex.Message);
		}
		catch (KeyNotFoundException)
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: $"Backup {name} does not exist");
		}

		return RestartResponse(lifetime, httpContext);
	}

	private static async Task<Results<Accepted<RestartRequiredDto>, ProblemHttpResult>> RestoreUploadAsync(
		BackupService backups,
		IHostApplicationLifetime lifetime,
		HttpContext httpContext,
		HttpRequest request,
		CancellationToken cancellationToken)
	{
		if (!request.HasFormContentType)
		{
			return TypedResults.Problem(
				statusCode: StatusCodes.Status400BadRequest,
				title: "Restore failed",
				detail: "Request must be multipart/form-data with a file part");
		}

		var form = await request.ReadFormAsync(cancellationToken);
		var file = form.Files.FirstOrDefault();
		if (file is null || file.Length == 0)
		{
			return TypedResults.Problem(
				statusCode: StatusCodes.Status400BadRequest,
				title: "Restore failed",
				detail: "No file uploaded");
		}

		try
		{
			await using var stream = file.OpenReadStream();
			await backups.RestoreAsync(stream, cancellationToken);
		}
		catch (NotSupportedException ex)
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status501NotImplemented, title: "Restore not supported", detail: ex.Message);
		}
		catch (InvalidOperationException ex)
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Restore failed", detail: ex.Message);
		}

		return RestartResponse(lifetime, httpContext);
	}

	private static Accepted<RestartRequiredDto> RestartResponse(IHostApplicationLifetime lifetime, HttpContext context)
	{
		context.Response.OnCompleted(() =>
		{
			lifetime.StopApplication();
			return Task.CompletedTask;
		});
		return TypedResults.Accepted((string?)null, new RestartRequiredDto(true));
	}
}

/// <summary>Result of enqueuing a command.</summary>
/// <param name="CommandId">Id of the enqueued command.</param>
public sealed record EnqueueResultDto(int CommandId);

/// <summary>Restore accepted, a restart is required to complete it.</summary>
/// <param name="RestartRequired">Whether the application will restart.</param>
public sealed record RestartRequiredDto(bool RestartRequired);
