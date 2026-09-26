using System.Collections.Generic;

namespace Submarine.Core.Release;

/// <summary>
///     Metadata of a release Title
/// </summary>
/// <param name="MainTitle">The Main Release Title (Without AKA Aliases)</param>
/// <param name="Aliases">Aliases included in this Title, if any</param>
/// <param name="Year">Year included in the Title, if any</param>
/// <param name="Group">Release Group included in the Title, if any</param>
/// <param name="Hash">The Hash included in the Title, if any</param>
public abstract record TitleMetadata(string MainTitle, IReadOnlyList<string> Aliases, string? Year, string? Group,
	string? Hash);
