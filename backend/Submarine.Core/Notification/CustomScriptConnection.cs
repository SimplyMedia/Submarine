namespace Submarine.Core.Notification;

/// <summary>
///     A Connection which executes a local script on media events
/// </summary>
public class CustomScriptConnection : Connection
{
	/// <summary>
	///     Path to the script executed on media events
	/// </summary>
	public string ScriptPath { get; set; } = string.Empty;

	/// <summary>
	///     Creates a new instance of <see cref="CustomScriptConnection" />
	/// </summary>
	public CustomScriptConnection()
		=> Type = ConnectionType.CUSTOM_SCRIPT;
}
