namespace Submarine.Core.Entities;

/// <summary>
///     Score assigned to a custom format inside a quality profile.
/// </summary>
/// <param name="CustomFormatId">Id of the custom format.</param>
/// <param name="Score">Score added when the format matches.</param>
public sealed record ProfileFormatItem(int CustomFormatId, int Score);
