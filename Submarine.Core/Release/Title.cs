using System.Collections.Generic;

namespace Submarine.Core.Release;

public record Title(string Main, IReadOnlyList<string> Aliases);
