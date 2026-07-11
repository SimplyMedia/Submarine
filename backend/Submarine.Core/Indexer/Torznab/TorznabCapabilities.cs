using System.Collections.Generic;

namespace Submarine.Core.Indexer.Torznab;

/// <summary>
///     The capabilities of a Torznab/Newznab indexer as reported by its caps endpoint
/// </summary>
public record TorznabCapabilities
{
	/// <summary>
	///     The title of the indexer server, if reported
	/// </summary>
	public string? ServerTitle { get; init; }

	/// <summary>
	///     The version of the indexer server, if reported
	/// </summary>
	public string? ServerVersion { get; init; }

	/// <summary>
	///     The maximum amount of results per request, if reported
	/// </summary>
	public int? LimitsMax { get; init; }

	/// <summary>
	///     The default amount of results per request, if reported
	/// </summary>
	public int? LimitsDefault { get; init; }

	/// <summary>
	///     The free text search mode, if reported
	/// </summary>
	public TorznabSearchMode? Search { get; init; }

	/// <summary>
	///     The tv search mode, if reported
	/// </summary>
	public TorznabSearchMode? TvSearch { get; init; }

	/// <summary>
	///     The movie search mode, if reported
	/// </summary>
	public TorznabSearchMode? MovieSearch { get; init; }

	/// <summary>
	///     The category tree of the indexer
	/// </summary>
	public IReadOnlyList<TorznabCategory> Categories { get; init; } = [];
}

/// <summary>
///     A search mode supported by a Torznab/Newznab indexer
/// </summary>
public record TorznabSearchMode
{
	/// <summary>
	///     Whether the indexer offers this search mode
	/// </summary>
	public bool Available { get; init; }

	/// <summary>
	///     The query parameters supported by this search mode
	/// </summary>
	public IReadOnlyList<string> SupportedParams { get; init; } = [];
}

/// <summary>
///     A category of a Torznab/Newznab indexer
/// </summary>
public record TorznabCategory
{
	/// <summary>
	///     The id of the category
	/// </summary>
	public int Id { get; init; }

	/// <summary>
	///     The name of the category
	/// </summary>
	public string Name { get; init; }

	/// <summary>
	///     The subcategories of the category
	/// </summary>
	public IReadOnlyList<TorznabCategory> Subcategories { get; init; } = [];
}
