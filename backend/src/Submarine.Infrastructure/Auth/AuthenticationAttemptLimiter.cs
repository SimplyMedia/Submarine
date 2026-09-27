using Microsoft.Extensions.Caching.Memory;

namespace Submarine.Infrastructure.Auth;

/// <summary>Limits repeated Basic authentication attempts per client and username.</summary>
public interface IAuthenticationAttemptLimiter
{
	/// <summary>Returns whether another authentication attempt is allowed.</summary>
	bool TryAcquire(string partitionKey);
}

/// <summary>Fixed-window login limiter matching the interactive login policy.</summary>
public sealed class AuthenticationAttemptLimiter(IMemoryCache cache, TimeProvider timeProvider) : IAuthenticationAttemptLimiter
{
	private const int PermitLimit = 10;
	private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);
	private readonly object gate = new();

	/// <inheritdoc />
	public bool TryAcquire(string partitionKey)
	{
		lock (gate)
		{
			var now = timeProvider.GetUtcNow();
			var window = cache.GetOrCreate(partitionKey, entry =>
			{
				entry.AbsoluteExpiration = now + Window;
				return new AttemptWindow(now);
			})!;
			if (now - window.StartedAt >= Window)
			{
				window.StartedAt = now;
				window.Attempts = 0;
			}

			if (window.Attempts >= PermitLimit)
			{
				return false;
			}

			window.Attempts++;
			return true;
		}
	}

	private sealed class AttemptWindow(DateTimeOffset startedAt)
	{
		public DateTimeOffset StartedAt { get; set; } = startedAt;
		public int Attempts { get; set; }
	}
}
