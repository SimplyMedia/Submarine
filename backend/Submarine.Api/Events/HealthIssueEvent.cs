namespace Submarine.Api.Events;

/// <summary>
///     Raised when a new health issue was detected
/// </summary>
/// <param name="Type">severity of the issue ("warning" or "error")</param>
/// <param name="Source">health check the issue originates from</param>
/// <param name="Message">description of the issue</param>
public record HealthIssueEvent(string Type, string Source, string Message);
