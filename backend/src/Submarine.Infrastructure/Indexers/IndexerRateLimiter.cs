using System;
using System.Threading;
using System.Threading.Tasks;

namespace Submarine.Infrastructure.Indexers;

/// <summary>
///     Simple token bucket ensuring a minimum delay between two requests
/// </summary>
/// <param name="timeProvider">The time source</param>
/// <param name="delaySeconds">The minimum seconds between two requests</param>
internal sealed class IndexerRateLimiter(TimeProvider timeProvider, double delaySeconds) : IDisposable
{
	private readonly SemaphoreSlim _lock = new(1, 1);
	private DateTimeOffset _nextAllowedAt = DateTimeOffset.MinValue;

	/// <summary>
	///     Waits until the next request slot is available and claims it
	/// </summary>
	/// <param name="cancellationToken">Cancellation token</param>
	public async Task WaitForSlotAsync(CancellationToken cancellationToken)
	{
		while (true)
		{
			TimeSpan wait;
			await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				wait = _nextAllowedAt - timeProvider.GetUtcNow();
				if (wait <= TimeSpan.Zero)
				{
					_nextAllowedAt = timeProvider.GetUtcNow().AddSeconds(delaySeconds);
					return;
				}
			}
			finally
			{
				_lock.Release();
			}

			await Task.Delay(wait, timeProvider, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public void Dispose() => _lock.Dispose();
}
