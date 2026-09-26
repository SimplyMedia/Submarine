using System;

namespace Submarine.Core.Indexers;

/// <summary>
///     An indexer failed in an unexpected way
/// </summary>
/// <param name="message">The error message</param>
/// <param name="innerException">The causing exception, if any</param>
public class IndexerException(string message, Exception? innerException = null)
	: Exception(message, innerException);

/// <summary>
///     The indexer credentials are missing or wrong
/// </summary>
/// <param name="message">The error message</param>
public class IndexerAuthException(string message) : IndexerException(message);

/// <summary>
///     The indexer rate limited us
/// </summary>
/// <param name="retryAfter">How long to wait before retrying</param>
public class IndexerRateLimitException(TimeSpan retryAfter)
	: IndexerException($"Rate limited by the indexer, retry after {retryAfter.TotalSeconds:0} seconds")
{
	/// <summary>
	///     How long to wait before retrying
	/// </summary>
	public TimeSpan RetryAfter { get; } = retryAfter;
}
