namespace Submarine.Core.Enums;

/// <summary>
///     Authentication modes of the application.
/// </summary>
public enum AuthMethod
{
	/// <summary>
	///     No login required, every request is authenticated as an anonymous administrator.
	/// </summary>
	NONE,

	/// <summary>
	///     Users authenticate with username and password.
	/// </summary>
	FORMS
}
