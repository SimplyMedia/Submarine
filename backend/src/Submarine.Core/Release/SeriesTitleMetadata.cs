using System.Collections.Generic;

namespace Submarine.Core.Release;

/// <summary>
///     The Metadata for a Series Release Title
/// </summary>
/// <param name="MainTitle">The Main Title</param>
/// <param name="Aliases">The Aliases of this Release Title</param>
/// <param name="Seasons">The Seasons of this Release Titles</param>
/// <param name="Episodes">The Episodes of this Release Title</param>
/// <param name="AbsoluteEpisodes">The Absolute Episodes of this Release Title</param>
/// <param name="Year">The Year from this Release Title</param>
/// <param name="Group">The Release Group from this Release Title</param>
/// <param name="Hash">The Hash from this Release Title</param>
public record SeriesTitleMetadata(string MainTitle, IReadOnlyList<string> Aliases, IReadOnlyCollection<int>? Seasons,
		IReadOnlyCollection<int>? Episodes, IReadOnlyCollection<int>? AbsoluteEpisodes, string? Year, string? Group,
		string? Hash)
	: TitleMetadata(MainTitle, Aliases, Year, Group, Hash);
