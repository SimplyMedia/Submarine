using Microsoft.EntityFrameworkCore;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Logging;

/// <summary>
///     Serilog batched sink persisting log events into the LogDbContext.
/// </summary>
public sealed class LogDbSink(Func<LogDbContext> contextFactory) : IBatchedLogEventSink
{
	/// <inheritdoc />
	public async Task EmitBatchAsync(IEnumerable<LogEvent> batch)
	{
		var rows = batch.Select(ToLog).ToList();
		if (rows.Count == 0)
		{
			return;
		}

		await using var db = contextFactory();
		db.Logs.AddRange(rows);
		try
		{
			await db.SaveChangesAsync();
		}
		catch (Exception)
		{
			// Never let log persistence crash the application. The console and file sinks stay unaffected.
		}
	}

	/// <inheritdoc />
	public Task OnEmptyBatchAsync() => Task.CompletedTask;

	private static Log ToLog(LogEvent logEvent)
	{
		var logger = logEvent.Properties.TryGetValue("SourceContext", out var value)
			? value.ToString().Trim('"')
			: string.Empty;
		return new Log
		{
			Time = logEvent.Timestamp.UtcDateTime,
			Level = logEvent.Level.ToString(),
			Logger = logger,
			Message = logEvent.RenderMessage(),
			Exception = logEvent.Exception?.ToString()
		};
	}
}
