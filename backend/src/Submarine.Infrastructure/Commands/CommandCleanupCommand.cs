using Submarine.Core.Commands;

namespace Submarine.Infrastructure.Commands;

/// <summary>
///     Prunes finished command rows older than seven days.
/// </summary>
public sealed record CommandCleanupCommand : CommandBase;
