namespace Submarine.Core.Common;

/// <summary>
///     Thrown when a request cannot complete because of the current state of the resource
///     (duplicate name, still in use, invalid state transition). Maps to HTTP 409.
/// </summary>
public sealed class ConflictException(string message) : Exception(message);
