using System.Collections.Generic;

namespace Submarine.Core.Release;

public record Title(string FullTitle, string MainTitle, IReadOnlyList<string> Aliases);
