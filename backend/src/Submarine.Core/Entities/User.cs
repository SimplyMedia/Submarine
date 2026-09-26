namespace Submarine.Core.Entities;

/// <summary>
///     A user able to log into the web UI.
/// </summary>
public sealed class User : Entity
{
	/// <summary>Unique username.</summary>
	public string Username { get; set; } = string.Empty;

	/// <summary>PBKDF2 password hash produced by PasswordHasher.</summary>
	public string PasswordHash { get; set; } = string.Empty;
}
