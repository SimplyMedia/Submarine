namespace Submarine.Core.Download;

/// <summary>
///     Thrown when a download client request fails
/// </summary>
public class DownloadClientException : Exception
{
	/// <summary>
	///     Creates a new <see cref="DownloadClientException" />
	/// </summary>
	/// <param name="message">Failure description</param>
	public DownloadClientException(string message)
		: base(message)
	{
	}

	/// <summary>
	///     Creates a new <see cref="DownloadClientException" />
	/// </summary>
	/// <param name="message">Failure description</param>
	/// <param name="innerException">Underlying failure</param>
	public DownloadClientException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
