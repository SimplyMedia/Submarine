using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Auth;

/// <summary>
///     Verifies a username and password against the Users table. Shared by cookie login and
///     HTTP Basic authentication so both pay the same timing-safe cost for an unknown username.
/// </summary>
public interface IUserCredentialVerifier
{
	/// <summary>
	///     Verifies the credentials, returning the matching user or null.
	/// </summary>
	Task<User?> VerifyAsync(string username, string password, CancellationToken cancellationToken = default);
}

/// <summary>
///     Default implementation backed by <see cref="SubmarineDbContext" /> and <see cref="PasswordHasher{TUser}" />.
/// </summary>
public sealed class UserCredentialVerifier(SubmarineDbContext db, IPasswordHasher<User> hasher) : IUserCredentialVerifier
{
	// Verified against every unknown-username login so the response time does not reveal
	// whether the account exists; the password never matches, so this only costs one PBKDF2 hash.
	private static readonly Lazy<string> DummyPasswordHash =
		new(() => new PasswordHasher<User>().HashPassword(new User(), "Submarine-Dummy-Hash-Never-Used"));

	/// <inheritdoc />
	public async Task<User?> VerifyAsync(string username, string password, CancellationToken cancellationToken = default)
	{
		var user = await db.Users.FirstOrDefaultAsync(x => x.Username == username, cancellationToken);
		if (user is null)
		{
			hasher.VerifyHashedPassword(new User(), DummyPasswordHash.Value, password);
			return null;
		}

		var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
		return verification == PasswordVerificationResult.Failed ? null : user;
	}
}
