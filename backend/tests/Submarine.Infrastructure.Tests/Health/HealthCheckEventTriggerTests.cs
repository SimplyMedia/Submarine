using NSubstitute;
using Shouldly;
using Submarine.Core.Commands;
using Submarine.Core.Events;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Health;
using Xunit;

namespace Submarine.Infrastructure.Tests.Health;

/// <summary>
///     Asserts that relevant domain events re-enqueue a health check.
/// </summary>
public sealed class HealthCheckEventTriggerTests
{
	[Theory]
	[MemberData(nameof(Events))]
	public async Task HandleAsync_ShouldEnqueueHealthCheck(IDomainEvent domainEvent)
	{
		var queue = Substitute.For<ICommandQueue>();
		var trigger = new HealthCheckEventTrigger(queue);

		await Dispatch(trigger, domainEvent, TestContext.Current.CancellationToken);

		await queue.Received(1).EnqueueAsync(
			Arg.Any<HealthCheckCommand>(),
			CommandTrigger.SYSTEM,
			Arg.Any<CommandPriority>(),
			Arg.Any<CancellationToken>());
	}

	public static TheoryData<IDomainEvent> Events() => new()
	{
		new IndexerStatusChangedEvent(1),
		new DownloadClientStatusChangedEvent(1),
		new SeriesDeletedEvent(1, "Show", false),
		new MovieDeletedEvent(1, "Movie", false)
	};

	private static Task Dispatch(HealthCheckEventTrigger trigger, IDomainEvent domainEvent, CancellationToken cancellationToken)
		=> domainEvent switch
		{
			IndexerStatusChangedEvent e => trigger.HandleAsync(e, cancellationToken),
			DownloadClientStatusChangedEvent e => trigger.HandleAsync(e, cancellationToken),
			SeriesDeletedEvent e => trigger.HandleAsync(e, cancellationToken),
			MovieDeletedEvent e => trigger.HandleAsync(e, cancellationToken),
			_ => throw new NotSupportedException(domainEvent.GetType().Name)
		};
}
