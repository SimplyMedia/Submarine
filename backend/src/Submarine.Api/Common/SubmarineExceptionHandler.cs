using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Submarine.Core.Common;
using Submarine.Infrastructure.Metadata;

namespace Submarine.Api.Common;

/// <summary>
///     Maps well known exceptions to ProblemDetails responses:
///     KeyNotFoundException to 404, ValidationException to 400 with errors,
///     ConflictException to 409, SiblingServiceException to 502 naming the service,
///     everything else to 500 with no exception detail.
/// </summary>
public sealed class SubmarineExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
	/// <inheritdoc />
	public async ValueTask<bool> TryHandleAsync(
		HttpContext httpContext,
		Exception exception,
		CancellationToken cancellationToken)
	{
		var (status, title, detail) = exception switch
		{
			KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found", exception.Message),
			ValidationException => (StatusCodes.Status400BadRequest, "Validation failed", exception.Message),
			ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
			SiblingServiceException => (StatusCodes.Status502BadGateway, "Upstream service unavailable", exception.Message),
			_ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", "An unexpected error occurred")
		};

		httpContext.Response.StatusCode = status;

		var problemDetails = new ProblemDetailsContext
		{
			HttpContext = httpContext,
			Exception = exception,
			ProblemDetails = new ProblemDetails
			{
				Status = status,
				Title = title,
				Detail = detail
			}
		};

		if (exception is ValidationException validationException)
		{
			problemDetails.ProblemDetails.Extensions["errors"] = validationException.Errors
				.GroupBy(failure => failure.PropertyName)
				.ToDictionary(
					group => ToCamelCase(group.Key),
					group => group.Select(failure => failure.ErrorMessage).ToArray());
		}

		return await problemDetailsService.TryWriteAsync(problemDetails);
	}

	private static string ToCamelCase(string name)
		=> string.IsNullOrEmpty(name) || char.IsLower(name[0])
			? name
			: char.ToLowerInvariant(name[0]) + name[1..];
}
