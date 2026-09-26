using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Features.Auth;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Users;

/// <summary>
///     User management.
/// </summary>
public sealed class UsersModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/users");
		group.MapGet("/", ListAsync);
		group.MapPost("/", CreateAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<PagedResult<UserDto>>> ListAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		CancellationToken cancellationToken)
		=> TypedResults.Ok(await PagedResult<UserDto>.CreateAsync(
			db.Users,
			query,
			x => new UserDto(x.Id, x.Username),
			cancellationToken));

	private static async Task<Results<Created<UserDto>, ProblemHttpResult>> CreateAsync(
		SubmarineDbContext db,
		IPasswordHasher<User> hasher,
		IValidator<CreateUserRequest> validator,
		CreateUserRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		if (await db.Users.AnyAsync(x => x.Username == request.Username, cancellationToken))
		{
			return TypedResults.Problem(
				statusCode: StatusCodes.Status409Conflict,
				title: $"Username '{request.Username}' is already taken");
		}

		var user = new User { Username = request.Username };
		user.PasswordHash = hasher.HashPassword(user, request.Password);
		db.Users.Add(user);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Created($"/api/v1/users/{user.Id}", new UserDto(user.Id, user.Username));
	}

	private static async Task<Results<Ok<UserDto>, ProblemHttpResult>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		IPasswordHasher<User> hasher,
		IValidator<UpdateUserRequest> validator,
		UpdateUserRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (user is null)
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found");
		}

		if (request.Username is not null && request.Username != user.Username)
		{
			if (await db.Users.AnyAsync(x => x.Username == request.Username, cancellationToken))
			{
				return TypedResults.Problem(
					statusCode: StatusCodes.Status409Conflict,
					title: $"Username '{request.Username}' is already taken");
			}

			user.Username = request.Username;
		}

		if (!string.IsNullOrEmpty(request.Password))
		{
			user.PasswordHash = hasher.HashPassword(user, request.Password);
		}

		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(new UserDto(user.Id, user.Username));
	}

	private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
		int id,
		SubmarineDbContext db,
		CancellationToken cancellationToken)
	{
		var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (user is null)
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found");
		}

		if (await db.Users.CountAsync(cancellationToken) <= 1)
		{
			return TypedResults.Problem(
				statusCode: StatusCodes.Status409Conflict,
				title: "Cannot delete the last user");
		}

		db.Users.Remove(user);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.NoContent();
	}
}

/// <summary>Create user request.</summary>
/// <param name="Username">Username.</param>
/// <param name="Password">Password.</param>
public sealed record CreateUserRequest(string Username, string Password);

/// <summary>Update user request, fields left out keep their value.</summary>
/// <param name="Username">New username, or null.</param>
/// <param name="Password">New password, or null.</param>
public sealed record UpdateUserRequest(string? Username, string? Password);

/// <summary>Validator for <see cref="CreateUserRequest" />.</summary>
public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
	/// <inheritdoc />
	public CreateUserRequestValidator()
	{
		RuleFor(x => x.Username).NotEmpty().MaximumLength(256);
		RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
	}
}

/// <summary>Validator for <see cref="UpdateUserRequest" />.</summary>
public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
	/// <inheritdoc />
	public UpdateUserRequestValidator()
	{
		RuleFor(x => x.Username).MaximumLength(256);
		RuleFor(x => x.Password).MaximumLength(256);
	}
}
