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

	/// <summary>Whether automatic updates are enabled, null means unset.</summary>
	public bool? UpdateAutomatically { get; set; }
}
