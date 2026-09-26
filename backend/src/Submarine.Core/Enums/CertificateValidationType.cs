namespace Submarine.Core.Enums;

/// <summary>
///     How strictly outbound HTTPS clients validate the remote TLS certificate.
/// </summary>
public enum CertificateValidationType
{
	/// <summary>Certificate errors always fail the request.</summary>
	ENABLED,

	/// <summary>Certificate errors are ignored when the remote host is a local address.</summary>
	DISABLED_FOR_LOCAL_ADDRESSES,

	/// <summary>Certificate errors are always ignored.</summary>
	DISABLED
}
