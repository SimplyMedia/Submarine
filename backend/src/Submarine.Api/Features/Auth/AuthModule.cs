using System.Data;
using System.Data.Common;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Auth;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Auth;
using Submarine.Infrastructure.Persistence;
using Submarine.Core.Enums;
namespace Submarine.Api.Features.Auth;

/// <summary>
///     Login, logout, current user and first run setup.
/// </summary>
public sealed class AuthModule : IEndpointModule
{
	// Verified against every unknown-username login so the response time does not reveal
	// whether the account exists; the password never matches, so this only costs one PBKDF2 hash.
	private static readonly Lazy<string> DummyPasswordHash =
		new(() => new PasswordHasher<User>().HashPassword(new User(), "Submarine-Dummy-Hash-Never-Used"));

	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var auth = endpoints.MapGroup("/api/v1/auth");
		auth.MapPost("/login", LoginAsync).AllowAnonymous().RequireRateLimiting("login");
		auth.MapPost("/logout", (Delegate)LogoutAsync).AllowAnonymous();
		auth.MapGet("/me", MeAsync).AllowAnonymous();

		var setup = endpoints.MapGroup("/api/v1/setup");
		setup.MapGet("/status", StatusAsync).AllowAnonymous();
		setup.MapPost("/", SetupAsync).AllowAnonymous().RequireRateLimiting("login");
	}

	// Anonymous: the UI needs both values before sign in to pick setup, login or open access.
	private static async Task<Ok<SetupStatusDto>> StatusAsync(
		SubmarineDbContext db,
		IAuthConfigProvider authConfigProvider,
		CancellationToken cancellationToken)
		=> TypedResults.Ok(new SetupStatusDto(
			!await db.Users.AnyAsync(cancellationToken),
			(await authConfigProvider.GetSnapshotAsync(cancellationToken)).Method));

	private static async Task<Results<Created<UserDto>, ProblemHttpResult>> SetupAsync(
		SubmarineDbContext db,
		IPasswordHasher<User> hasher,
		IValidator<SetupRequest> validator,
		HttpContext httpContext,
		TimeProvider timeProvider,
		SetupRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);

		// Serializable isolation makes two concurrent setups conflict instead of both succeeding:
		// Sqlite's WAL mode already gives the reader a snapshot that a later writer cannot silently
		// invalidate (raises SQLITE_BUSY_SNAPSHOT), and Postgres needs Serializable explicitly to
		// detect the same read-then-insert race (raises a 40001 serialization failure). Either way
		// only the first insert commits; the loser's SaveChanges/Commit throws and gets mapped to 409.
		await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		try
		{
			if (await db.Users.AnyAsync(cancellationToken))
			{
				return SetupConflict();
			}

			var user = new User { Username = request.Username };
			user.PasswordHash = hasher.HashPassword(user, request.Password);
			db.Users.Add(user);
			await db.SaveChangesAsync(cancellationToken);
			await transaction.CommitAsync(cancellationToken);

			// The first user continues straight into the app, so sign them in here.
			await SignInAsync(httpContext, user, isPersistent: true, timeProvider);
			return TypedResults.Created($"/api/v1/users/{user.Id}", new UserDto(user.Id, user.Username));
		}
		catch (Exception exception) when (exception is DbUpdateException or DbException)
		{
			return SetupConflict();
		}
	}

	private static ProblemHttpResult SetupConflict()
		=> TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Setup already completed");

	private static async Task<Results<Ok<UserDto>, ProblemHttpResult>> LoginAsync(
		SubmarineDbContext db,
		IPasswordHasher<User> hasher,
		IValidator<LoginRequest> validator,
		HttpContext httpContext,
		TimeProvider timeProvider,
		LoginRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var user = await db.Users.FirstOrDefaultAsync(x => x.Username == request.Username, cancellationToken);
		if (user is null)
		{
			// Run a verification of the same cost as a real login so timing does not leak
			// whether the username exists.
			hasher.VerifyHashedPassword(new User(), DummyPasswordHash.Value, request.Password);
			return InvalidCredentials();
		}

		var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
		if (verification == PasswordVerificationResult.Failed)
		{
			return InvalidCredentials();
		}

		await SignInAsync(httpContext, user, request.RememberMe, timeProvider);
		return TypedResults.Ok(new UserDto(user.Id, user.Username));
	}

	private static Task SignInAsync(HttpContext httpContext, User user, bool isPersistent, TimeProvider timeProvider)
	{
		var identity = new ClaimsIdentity(
			[
				new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
				new Claim(ClaimTypes.Name, user.Username),
				// Full-precision UTC ticks (not whole Unix seconds) so this never appears to
				// predate a User.UpdatedAt that was stamped a fraction of a second earlier;
				// compared on every request (OnValidatePrincipal) so a password change
				// invalidates sessions issued before it.
				new Claim(AuthClaimTypes.AuthTime, timeProvider.GetUtcNow().UtcDateTime.Ticks.ToString())
			],
			AuthenticationSetup.CookieScheme);
		return httpContext.SignInAsync(
			AuthenticationSetup.CookieScheme,
			new ClaimsPrincipal(identity),
			new AuthenticationProperties { IsPersistent = isPersistent });
	}

	private static async Task<NoContent> LogoutAsync(HttpContext httpContext)
	{
		await httpContext.SignOutAsync(AuthenticationSetup.CookieScheme);
		return TypedResults.NoContent();
	}

	// Anonymous callers get 204 instead of 401 so the session probe never logs a browser console error.
	private static Results<Ok<UserDto>, NoContent> MeAsync(HttpContext httpContext)
	{
		var id = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
		var name = httpContext.User.FindFirstValue(ClaimTypes.Name);
		if (id is null || name is null || !int.TryParse(id, out var userId))
		{
			return TypedResults.NoContent();
		}

		return TypedResults.Ok(new UserDto(userId, name));
	}

	private static ProblemHttpResult InvalidCredentials()
		=> TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid username or password");
}

/// <summary>First run setup status.</summary>
/// <param name="NeedsSetup">Whether no user exists yet and setup must run.</param>
/// <param name="AuthMethod">How requests are authenticated; NONE means the UI is open.</param>
public sealed record SetupStatusDto(bool NeedsSetup, AuthMethod AuthMethod);

/// <summary>Setup request.</summary>
/// <param name="Username">Username of the first user.</param>
/// <param name="Password">Password of the first user.</param>
public sealed record SetupRequest(string Username, string Password);

/// <summary>Login request.</summary>
/// <param name="Username">Username.</param>
/// <param name="Password">Password.</param>
/// <param name="RememberMe">Keep the session cookie persistent.</param>
public sealed record LoginRequest(string Username, string Password, bool RememberMe = false);

/// <summary>User without secrets.</summary>
/// <param name="Id">User id.</param>
/// <param name="Username">Username.</param>
public sealed record UserDto(int Id, string Username);

/// <summary>Validator for <see cref="SetupRequest" />.</summary>
public sealed class SetupRequestValidator : AbstractValidator<SetupRequest>
{
	/// <inheritdoc />
	public SetupRequestValidator()
	{
		RuleFor(x => x.Username).NotEmpty().MaximumLength(256);
		RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(256);
	}
}

/// <summary>Validator for <see cref="LoginRequest" />.</summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
	/// <inheritdoc />
	public LoginRequestValidator()
	{
		RuleFor(x => x.Username).NotEmpty().MaximumLength(256);
		RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
	}
}
