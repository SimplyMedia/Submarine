namespace Submarine.Api.Models.Response;

/// <summary>
///     A detected issue with the current system configuration or state
/// </summary>
/// <param name="Type">Severity of the issue, "warning" or "error"</param>
/// <param name="Source">Health check which raised the issue</param>
/// <param name="Message">Human readable description of the issue</param>
/// <param name="WikiFragment">Optional wiki anchor with more information about the issue</param>
public record HealthIssue(string Type, string Source, string Message, string? WikiFragment = null);
