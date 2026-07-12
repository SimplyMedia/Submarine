namespace Submarine.Api.Models.Response;

/// <summary>
///     Result of a bulk editor operation
/// </summary>
/// <param name="UpdatedCount">number of entities updated</param>
public record EditorResult(int UpdatedCount);
