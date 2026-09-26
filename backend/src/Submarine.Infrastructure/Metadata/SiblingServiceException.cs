namespace Submarine.Infrastructure.Metadata;

/// <summary>
///     Thrown when a call to the Metadata or Mappings sibling service fails at the transport level
///     (unreachable, timed out, or a non-2xx/404 response after resilience retries). Carries the
///     service name so the API can report which sibling failed instead of a generic 500. Maps to HTTP 502.
/// </summary>
public sealed class SiblingServiceException(string serviceName, string message, Exception inner)
	: Exception(message, inner)
{
	/// <summary>The human readable name of the sibling service that failed.</summary>
	public string ServiceName { get; } = serviceName;
}
