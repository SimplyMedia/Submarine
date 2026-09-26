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

/// <summary>Path helpers shared by the upstream clients.</summary>
internal static class UpstreamPath
{
	/// <summary>
	///     Strips the query string so credentials (TMDB's api_key, search terms) never end up in
	///     logs or ProblemDetails.
	/// </summary>
	public static string WithoutQuery(string path)
	{
		var index = path.IndexOf('?');
		return index < 0 ? path : path[..index];
	}
}
