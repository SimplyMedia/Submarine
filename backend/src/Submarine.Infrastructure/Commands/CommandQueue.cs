using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Common;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Commands;

/// <summary>
///     Cancelation and status side of the command queue, shared by the API and the executor.
/// </summary>
public interface ICommandCancellation
{
	/// <summary>
	///     Cancel a queued or running command. Returns false when the row does not exist.
	/// </summary>
	Task<bool> TryCancelAsync(int commandId, CancellationToken cancellationToken = default);
}

/// <summary>
///     Persists commands, deduplicates them and feeds the executor via a wake-up channel.
/// </summary>
public sealed class CommandQueue(
	IServiceScopeFactory scopeFactory,
	IEventBus eventBus,
	CommandExecutionRegistry executionRegistry,
	TimeProvider timeProvider)
	: ICommandQueue, ICommandCancellation
{
	private readonly Channel<int> _signals = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
	{
		SingleReader = false,
		SingleWriter = false
	});

	/// <summary>
	///     Reader used by the executor as a wake-up signal. The authoritative queue state is the database.
	/// </summary>
	public ChannelReader<int> SignalReader => _signals.Reader;

	/// <summary>
	///     Wake the executor without enqueueing anything, for example when a slot frees up.
	/// </summary>
	public void Wake() => _signals.Writer.TryWrite(0);

	/// <inheritdoc />
	public async Task<Command> EnqueueAsync(
		ICommand command,
		CommandTrigger trigger,
		CommandPriority priority = CommandPriority.NORMAL,
		CancellationToken cancellationToken = default)
	{
		var name = CommandRegistry.GetName(command.GetType());
		// Serialize<object> keeps the runtime type contract; serializing the ICommand interface would drop all payload.
		var body = JsonSerializer.Serialize<object>(command, SubmarineJson.Default);
		var bodyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));

		await using var scope = scopeFactory.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();

		var existing = await db.Commands
			.FirstOrDefaultAsync(
				x => x.Name == name
					&& x.BodyHash == bodyHash
					&& (x.Status == CommandStatus.QUEUED || x.Status == CommandStatus.RUNNING),
				cancellationToken);
		if (existing is not null)
		{
			return existing;
		}

		var row = new Command
		{
			Name = name,
			Body = body,
			BodyHash = bodyHash,
			Status = CommandStatus.QUEUED,
			Priority = priority,
			Trigger = trigger
		};
		db.Commands.Add(row);
		try
		{
			await db.SaveChangesAsync(cancellationToken);
		}
		catch (DbUpdateException)
		{
			// Lost a race against another enqueue of the same (Name, BodyHash): the filtered unique
			// index rejected our insert, so the concurrent winner is the row to return.
			db.Entry(row).State = EntityState.Detached;
			var winner = await db.Commands
				.FirstOrDefaultAsync(
					x => x.Name == name
						&& x.BodyHash == bodyHash
						&& (x.Status == CommandStatus.QUEUED || x.Status == CommandStatus.RUNNING),
					cancellationToken);
			return winner ?? throw new InvalidOperationException($"Enqueue of '{name}' conflicted but no matching row was found.");
		}

		_signals.Writer.TryWrite(row.Id);
		await PublishAsync(row, cancellationToken);
		return row;
	}

	/// <inheritdoc />
	public async Task<bool> TryCancelAsync(int commandId, CancellationToken cancellationToken = default)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
		var row = await db.Commands.FindAsync([commandId], cancellationToken);
		if (row is null)
		{
			return false;
		}

		switch (row.Status)
		{
			case CommandStatus.QUEUED:
				row.Status = CommandStatus.CANCELLED;
				row.Message = "Cancelled before execution";
				row.EndedAt = timeProvider.GetUtcNow().UtcDateTime;
				await db.SaveChangesAsync(cancellationToken);
				await PublishAsync(row, cancellationToken);
				return true;
			case CommandStatus.RUNNING:
				return executionRegistry.TryCancel(commandId);
			default:
				return false;
		}
	}

	private async Task PublishAsync(Command row, CancellationToken cancellationToken)
		=> await eventBus.PublishAsync(
			new CommandUpdated(row.Id, row.Name, row.Status, row.Progress, row.Message, row.StartedAt, row.EndedAt),
			cancellationToken);
}
