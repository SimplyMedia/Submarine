using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Health;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Logs;

/// <summary>
///     Log table and rolling file access.
/// </summary>
public sealed partial class LogsModule : IEndpointModule
{
	/// <summary>Matches safe log file names, guards against path traversal.</summary>
	[GeneratedRegex(@"^[A-Za-z0-9._-]+$")]
	private static partial Regex FileNamePattern();

	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/logs");
		group.MapGet("/", ListAsync);
		group.MapDelete("/", ClearAsync);
		group.MapGet("/files", ListFilesAsync);
		group.MapGet("/files/{name}", GetFileAsync).Produces(StatusCodes.Status200OK, contentType: "text/plain");
	}

	private static async Task<Ok<PagedResult<LogDto>>> ListAsync(
		LogDbContext db,
		[AsParameters] PagingQuery query,
		string? level,
		string? q,
		CancellationToken cancellationToken)
	{
		var source = db.Logs.AsNoTracking().AsQueryable();
		if (!string.IsNullOrEmpty(level))
		{
			source = source.Where(x => x.Level == level);
		}

		if (!string.IsNullOrEmpty(q))
		{
			source = source.Where(x => x.Message.Contains(q) || x.Logger.Contains(q));
		}

		// Newest first by default; an explicit sort key takes over once one is requested.
		var effectiveQuery = string.IsNullOrWhiteSpace(query.SortKey)
			? query with { SortKey = nameof(Log.Time), SortDirection = "desc" }
			: query;

		return TypedResults.Ok(await PagedResult<LogDto>.CreateAsync(
			source,
			effectiveQuery,
			x => new LogDto(x.Id, x.Time, x.Level, x.Logger, x.Message, x.Exception),
			cancellationToken));
	}

	private static async Task<NoContent> ClearAsync(LogDbContext db, CancellationToken cancellationToken)
	{
		await db.Logs.ExecuteDeleteAsync(cancellationToken);
		return TypedResults.NoContent();
	}

	private static Ok<IReadOnlyList<LogFileDto>> ListFilesAsync(DataDirectory dataDirectory)
	{
		var logsDirectory = Path.Combine(dataDirectory.Path, "logs");
		if (!Directory.Exists(logsDirectory))
		{
			return TypedResults.Ok<IReadOnlyList<LogFileDto>>([]);
		}

		var files = Directory.EnumerateFiles(logsDirectory, "*.txt")
			.Select(file =>
			{
				var info = new FileInfo(file);
				return new LogFileDto(info.Name, info.Length, info.LastWriteTimeUtc);
			})
			.OrderByDescending(x => x.Name)
			.ToList();
		return TypedResults.Ok<IReadOnlyList<LogFileDto>>(files);
	}

	private static IResult GetFileAsync(string name, DataDirectory dataDirectory)
	{
		if (!FileNamePattern().IsMatch(name) || !name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
		{
			return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Invalid log file name");
		}

		var path = Path.Combine(dataDirectory.Path, "logs", name);
		if (!File.Exists(path))
		{
			return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: $"Log file {name} does not exist");
		}

		return Results.Text(File.ReadAllText(path), "text/plain");
	}
}
/// <summary>One log row.</summary>
/// <param name="Id">Row id.</param>
/// <param name="Time">UTC timestamp.</param>
/// <param name="Level">Serilog level name.</param>
/// <param name="Logger">Logger name.</param>
/// <param name="Message">Rendered message.</param>
/// <param name="Exception">Exception text.</param>
public sealed record LogDto(int Id, DateTime Time, string Level, string Logger, string Message, string? Exception);

/// <summary>One rolling log file.</summary>
/// <param name="Name">File name.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="LastModified">UTC last write time.</param>
public sealed record LogFileDto(string Name, long Size, DateTime LastModified);
