namespace Submarine.Core.Download;

/// <summary>
///     Connection settings for the NZBVortex api; NZBVortex is always reached over https
/// </summary>
public record NzbVortexSettings : DownloadClientSettings
{
	/// <summary>Hostname or ip address of the NZBVortex server</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>Port of the NZBVortex server</summary>
	public int Port { get; init; } = 4321;

	/// <summary>Url base path NZBVortex is served under, if any</summary>
	public string? UrlBase { get; init; }

	/// <summary>NZBVortex api key</summary>
	public string ApiKey { get; init; } = string.Empty;

	/// <summary>Group nzbs are added under and owned by</summary>
	public string? Category { get; init; }

	/// <summary>Priority for recent releases</summary>
	public NzbVortexPriority RecentPriority { get; init; } = NzbVortexPriority.NORMAL;

	/// <summary>Priority for older releases</summary>
	public NzbVortexPriority OlderPriority { get; init; } = NzbVortexPriority.NORMAL;
}

/// <summary>
///     Transfer priority supported by the NZBVortex api
/// </summary>
public enum NzbVortexPriority
{
	/// <summary>Low priority</summary>
	LOW = -1,

	/// <summary>Normal priority</summary>
	NORMAL = 0,

	/// <summary>High priority</summary>
	HIGH = 1
}
