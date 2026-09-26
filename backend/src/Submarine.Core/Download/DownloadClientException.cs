namespace Submarine.Core.Download;

/// <summary>
///     Thrown when a download client is unreachable, rejects credentials, or fails an operation
/// </summary>
public class DownloadClientException(string message, Exception? innerException = null)
	: Exception(message, innerException);
