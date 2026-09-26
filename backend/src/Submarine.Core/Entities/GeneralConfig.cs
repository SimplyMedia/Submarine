using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     General instance configuration, singleton row with Id 1.
/// </summary>
public sealed class GeneralConfig : SingletonEntity
{
	/// <summary>Authentication mode.</summary>
	public AuthMethod AuthMethod { get; set; } = AuthMethod.FORMS;

	/// <summary>Global API key, 32 hex characters.</summary>
	public string ApiKey { get; set; } = string.Empty;

	/// <summary>Read only token for feeds like iCal, 32 hex characters.</summary>
	public string FeedToken { get; set; } = string.Empty;

	/// <summary>Base URL the app is served under, empty for root.</summary>
	public string UrlBase { get; set; } = string.Empty;

	/// <summary>Display name of this instance.</summary>
	public string InstanceName { get; set; } = "Submarine";

	/// <summary>Serilog minimum level.</summary>
	public string LogLevel { get; set; } = "Information";

	/// <summary>Update branch.</summary>
	public string Branch { get; set; } = "master";

	/// <summary>Whether authentication is required for every request or bypassed for local addresses.</summary>
	public AuthenticationRequiredType AuthenticationRequired { get; set; } = AuthenticationRequiredType.ENABLED;

	/// <summary>
	///     Comma separated CIDR list of reverse proxies trusted to set X-Forwarded-For; forwarded
	///     headers from any other peer are ignored.
	/// </summary>
	public string TrustedProxies { get; set; } = string.Empty;

	/// <summary>How strictly outbound HTTPS clients validate the remote TLS certificate.</summary>
	public CertificateValidationType CertificateValidation { get; set; } = CertificateValidationType.ENABLED;

	/// <summary>Whether an outbound proxy is used for external HTTP requests.</summary>
	public bool ProxyEnabled { get; set; }

	/// <summary>Outbound proxy protocol; FLARESOLVERR is not a valid value here.</summary>
	public IndexerProxyType ProxyType { get; set; } = IndexerProxyType.HTTP;

	/// <summary>Outbound proxy host.</summary>
	public string ProxyHost { get; set; } = string.Empty;

	/// <summary>Outbound proxy port.</summary>
	public int ProxyPort { get; set; } = 8080;

	/// <summary>Outbound proxy username.</summary>
	public string? ProxyUsername { get; set; }

	/// <summary>Outbound proxy password.</summary>
	public string? ProxyPassword { get; set; }

	/// <summary>
	///     Comma separated list of hosts, domains (with a leading *) or CIDR ranges that bypass
	///     the outbound proxy.
	/// </summary>
	public string ProxyBypassFilter { get; set; } = string.Empty;

	/// <summary>Whether local addresses (and hostnames without a dot) bypass the outbound proxy.</summary>
	public bool ProxyBypassLocalAddresses { get; set; } = true;

	/// <summary>Backup folder, relative to the app data directory when not rooted; empty uses "backups".</summary>
	public string BackupFolder { get; set; } = string.Empty;

	/// <summary>How often the scheduled backup task runs, in days.</summary>
	public int BackupIntervalDays { get; set; } = 7;

	/// <summary>How many of the most recent backups to keep.</summary>
	public int BackupRetention { get; set; } = 7;

	/// <summary>Externally reachable URL of this instance, used in notification links.</summary>
	public string ApplicationUrl { get; set; } = string.Empty;
}
