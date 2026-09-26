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
	FORMS,

	/// <summary>
	///     Users authenticate with HTTP Basic credentials, e.g. for tools that cannot follow
	///     a login redirect.
	/// </summary>
	BASIC,

	/// <summary>
	///     Authentication is handled entirely by a reverse proxy in front of the app; behaves
	///     exactly like <see cref="NONE" /> since the app trusts the proxy to gate access.
	/// </summary>
	EXTERNAL
}
