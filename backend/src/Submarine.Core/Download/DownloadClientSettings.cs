namespace Submarine.Core.Download;

/// <summary>
///     Base type of all download client settings records
/// </summary>
public abstract record DownloadClientSettings;

/// <summary>
///     Connection shape shared by all network download clients
/// </summary>
public interface IDownloadClientEndpoint
{
	/// <summary>
	///     Hostname or ip address of the client
	/// </summary>
	string Host { get; }

	/// <summary>
	///     Port of the client
	/// </summary>
	int Port { get; }

	/// <summary>
	///     Whether to connect using https
	/// </summary>
	bool UseSsl { get; }

	/// <summary>
	///     Url base path the client is served under, if any
	/// </summary>
	string? UrlBase => null;
}

/// <summary>
///     Url helpers for <see cref="IDownloadClientEndpoint" />
/// </summary>
public static class DownloadClientUrl
{
	/// <summary>
	///     Scheme, host and port of the client, without a trailing slash
	/// </summary>
	public static string BaseUrl(this IDownloadClientEndpoint endpoint)
		=> $"{(endpoint.UseSsl ? "https" : "http")}://{endpoint.Host}:{endpoint.Port}";

	/// <summary>
	///     Normalized url base prefix: empty or a leading slash without a trailing slash
	/// </summary>
	public static string Prefix(this IDownloadClientEndpoint endpoint)
		=> NormalizePath(endpoint.UrlBase);

	/// <summary>
	///     Combines base url, url base and a relative path into an absolute url
	/// </summary>
	public static string Build(this IDownloadClientEndpoint endpoint, string path)
		=> $"{endpoint.BaseUrl()}{endpoint.Prefix()}{path}";

	/// <summary>
	///     Normalizes a configured path fragment to "" or "/segment[/segment]"
	/// </summary>
	public static string NormalizePath(string? path)
		=> string.IsNullOrWhiteSpace(path) ? string.Empty : "/" + path.Trim().Trim('/');
}
