using System.Collections.Generic;

namespace Submarine.Core.Release;

/// <summary>
///     The Metadata for a Movie Release Title
/// </summary>
/// <param name="MainTitle">The Main Title of this Release</param>
/// <param name="Aliases">The Aliases of this Release</param>
/// <param name="Year">The Year of this Release</param>
/// <param name="Group">The Release Group of this Release</param>
/// <param name="Hash">The Hash of this Release</param>
/// <param name="Edition">The Movie Edition of this Release</param>
public record MovieTitleMetadata(string MainTitle, IReadOnlyList<string> Aliases, string? Year, string? Group,
		string? Hash, string? Edition)
	: TitleMetadata(MainTitle, Aliases, Year, Group, Hash);
