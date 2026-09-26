namespace Submarine.Core.Enums;

/// <summary>
///     Whether authentication is required for every request or bypassed for local addresses.
/// </summary>
public enum AuthenticationRequiredType
{
	/// <summary>Authentication is required for every request.</summary>
	ENABLED,

	/// <summary>
	///     Requests from a local address (loopback, RFC 1918, link-local or IPv6 unique local)
	///     are authenticated as an anonymous administrator without a login.
	/// </summary>
	DISABLED_FOR_LOCAL_ADDRESSES
}
