using Submarine.Core.Indexers;
using Submarine.Core.Provider;
using Submarine.Core.Release;

namespace Submarine.Core.CustomFormats;

/// <summary>
///     The release side inputs the <see cref="CustomFormatCalculator" /> evaluates. Indexer side fields carry data the
///     parsed release does not hold.
/// </summary>
/// <param name="Title">The title matched by release title specifications, usually the full release title.</param>
/// <param name="Release">The parsed release.</param>
/// <param name="Size">The release size in bytes, if reported.</param>
/// <param name="Year">The year of the media, overriding the year parsed from the title.</param>
/// <param name="IndexerFlags">Flags the indexer reports for the release.</param>
/// <param name="Protocol">The protocol of the release, falling back to the protocol of the parsed release.</param>
/// <param name="Indexer">The name of the indexer the release came from, if any.</param>
public sealed record ReleaseContext(
	string Title,
	BaseRelease Release,
	long? Size = null,
	int? Year = null,
	IReadOnlyList<IndexerFlag>? IndexerFlags = null,
	Protocol? Protocol = null,
	string? Indexer = null);
