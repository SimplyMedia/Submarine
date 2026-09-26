namespace Submarine.Metadata.Upstream;

/// <summary>
///     Raised when an upstream metadata provider answers with an error status.
///     <see cref="StatusCode" /> carries the upstream HTTP status so endpoints
///     can surface it.
/// </summary>
public sealed class UpstreamException(string provider, int statusCode, string message)
	: Exception(message)
{
	public string Provider { get; } = provider;

	public int StatusCode { get; } = statusCode;
}
