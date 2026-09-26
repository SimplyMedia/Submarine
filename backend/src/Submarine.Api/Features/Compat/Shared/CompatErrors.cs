namespace Submarine.Api.Features.Compat.Shared;

public sealed record CompatValidationIssue(string PropertyName, string ErrorMessage, string Severity = "error");

public static class CompatErrors
{
	public static IResult Validation(string propertyName, string errorMessage)
		=> Results.Json(new[] { new CompatValidationIssue(propertyName, errorMessage) }, CompatJson.Options, statusCode: StatusCodes.Status400BadRequest);

	public static IResult Message(string message, int statusCode)
		=> Results.Json(new { message }, CompatJson.Options, statusCode: statusCode);
}
