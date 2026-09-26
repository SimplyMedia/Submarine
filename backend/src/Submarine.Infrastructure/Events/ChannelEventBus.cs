using System.Threading.Channels;
using Submarine.Core.Events;

namespace Submarine.Infrastructure.Events;

/// <summary>
///     In-process event bus backed by an unbounded channel.
/// </summary>
public sealed class ChannelEventBus : IEventBus
{
	private readonly Channel<IDomainEvent> _channel = Channel.CreateUnbounded<IDomainEvent>(new UnboundedChannelOptions
	{
		SingleReader = false,
		SingleWriter = false
	});

	/// <summary>
	///     Reader consumed by the dispatcher hosted service.
	/// </summary>
	public ChannelReader<IDomainEvent> Reader => _channel.Reader;

	/// <inheritdoc />
	public ValueTask PublishAsync(IDomainEvent @event, CancellationToken cancellationToken = default)
	{
		_channel.Writer.TryWrite(@event);
		return ValueTask.CompletedTask;
	}
}
