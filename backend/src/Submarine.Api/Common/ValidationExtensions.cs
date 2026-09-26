using FluentValidation;

namespace Submarine.Api.Common;

/// <summary>
///     Runs a validator and turns a failed result into a ValidationException,
///     which the global exception handler maps to a 400 ProblemDetails with errors.
/// </summary>
public static class ValidationExtensions
{
	/// <summary>
	///     Validate and throw on failure.
	/// </summary>
	public static async Task ValidateOrThrowAsync<T>(this IValidator<T> validator, T instance, CancellationToken cancellationToken = default)
	{
		var result = await validator.ValidateAsync(instance, cancellationToken);
		if (!result.IsValid)
		{
			throw new ValidationException(result.Errors);
		}
	}
}
