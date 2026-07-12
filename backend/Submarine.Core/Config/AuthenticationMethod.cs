namespace Submarine.Core.Config;

/// <summary>
///     Supported authentication methods for the API
/// </summary>
public enum AuthenticationMethod
{
	/// <summary>
	///     No authentication, all requests are allowed
	/// </summary>
	NONE,

	/// <summary>
	///     Requests must provide the configured API key
	/// </summary>
	API_KEY
}
