using FluentValidation;

namespace Submarine.Mappings.Infrastructure;

/// <summary>
/// Shared FluentValidation helpers for endpoint handlers.
/// </summary>
public static class ValidationExtensions
{
	/// <summary>
	/// Validates a request. Returns a 400 validation problem when the request is invalid, otherwise null.
	/// </summary>
	public static async Task<IResult?> ValidateOrNullAsync<TRequest>(this IValidator<TRequest> validator, TRequest request, CancellationToken cancellationToken)
	{
		var result = await validator.ValidateAsync(request, cancellationToken);
		return result.IsValid ? null : Results.ValidationProblem(result.ToDictionary());
	}
}
