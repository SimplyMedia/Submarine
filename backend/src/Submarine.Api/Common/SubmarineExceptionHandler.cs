using System.Text.Json;
using Submarine.Api.Features.Compat.Shared;
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
public sealed class SubmarineExceptionHandler(
	IProblemDetailsService problemDetailsService,
	ILogger<SubmarineExceptionHandler> logger) : IExceptionHandler
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
		if (httpContext.Request.Path.StartsWithSegments("/compat"))
			return await TryHandleCompatAsync(httpContext, exception, cancellationToken);


		// Handled exceptions are not logged by the middleware, so unexpected ones would vanish without this.
		if (status == StatusCodes.Status500InternalServerError)
		{
			logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
		}

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
	private async ValueTask<bool> TryHandleCompatAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
	{
		var status = exception switch
		{
			ValidationException or ArgumentException or BadHttpRequestException => StatusCodes.Status400BadRequest,
			KeyNotFoundException => StatusCodes.Status404NotFound,
			ConflictException => StatusCodes.Status409Conflict,
			_ => StatusCodes.Status500InternalServerError
		};
		if (status >= StatusCodes.Status500InternalServerError)
			logger.LogError(exception, "Unhandled compatibility exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

		object payload = exception switch
		{
			ValidationException validation => validation.Errors.Select(error =>
				new CompatValidationIssue(ToCamelCase(error.PropertyName), error.ErrorMessage)).ToArray(),
			ArgumentException argument => new[] { new CompatValidationIssue(ToCamelCase(argument.ParamName ?? "request"), argument.Message) },
			BadHttpRequestException badRequest when badRequest.StatusCode == StatusCodes.Status400BadRequest
				=> new[] { new CompatValidationIssue("request", "The request could not be read.") },
			BadHttpRequestException badRequest => new { message = badRequest.Message },
			KeyNotFoundException missing => new { message = missing.Message },
			ConflictException conflict => new { message = conflict.Message },
			_ => new { message = "An unexpected error occurred" }
		};
		httpContext.Response.StatusCode = status;
		httpContext.Response.ContentType = "application/json; charset=utf-8";
		await JsonSerializer.SerializeAsync(httpContext.Response.Body, payload, payload.GetType(), CompatJson.Options, cancellationToken);
		return true;
	}


	private static string ToCamelCase(string name)
		=> string.IsNullOrEmpty(name) || char.IsLower(name[0])
			? name
			: char.ToLowerInvariant(name[0]) + name[1..];
}
