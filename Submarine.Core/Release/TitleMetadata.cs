using System.Collections.Generic;

namespace Submarine.Core.Release;

public record TitleMetadata(string MainTitle, IReadOnlyList<string> Aliases, int? Season, int? Episode, int? AbsoluteEpisode, string? Group, string? Hash);
