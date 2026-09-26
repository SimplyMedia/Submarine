using System.Collections.Generic;

namespace Submarine.Core.Indexers;

/// <summary>
///     The search capabilities of an indexer as reported by caps or a definition
/// </summary>
public record IndexerCapabilities
{
	/// <summary>
	///     Whether basic search (t=search) is available
	/// </summary>
	public bool SearchAvailable { get; init; } = true;

	/// <summary>
	///     The parameters supported by basic search
	/// </summary>
	public SearchParams SearchParams { get; init; } = SearchParams.Q;

	/// <summary>
	///     Whether tv search (t=tvsearch) is available
	/// </summary>
	public bool TvSearchAvailable { get; init; }

	/// <summary>
	///     The parameters supported by tv search
	/// </summary>
	public SearchParams TvSearchParams { get; init; } = SearchParams.Q;

	/// <summary>
	///     Whether movie search (t=movie) is available
	/// </summary>
	public bool MovieSearchAvailable { get; init; }

	/// <summary>
	///     The parameters supported by movie search
	/// </summary>
	public SearchParams MovieSearchParams { get; init; } = SearchParams.Q;

	/// <summary>
	///     Whether music search (t=music) is available
	/// </summary>
	public bool MusicSearchAvailable { get; init; }

	/// <summary>
	///     The parameters supported by music search
	/// </summary>
	public SearchParams MusicSearchParams { get; init; } = SearchParams.Q;

	/// <summary>
	///     Whether book search (t=book) is available
	/// </summary>
	public bool BookSearchAvailable { get; init; }

	/// <summary>
	///     The parameters supported by book search
	/// </summary>
	public SearchParams BookSearchParams { get; init; } = SearchParams.Q;

	/// <summary>
	///     The category tree of the indexer as advertised, mapped to standard ids where possible
	/// </summary>
	public List<IndexerCategory> Categories { get; init; } = [];

	/// <summary>
	///     The maximum amount of results per page reported by the indexer, if any
	/// </summary>
	public int? LimitsMax { get; init; }

	/// <summary>
	///     The default amount of results per page reported by the indexer, if any
	/// </summary>
	public int? LimitsDefault { get; init; }

	/// <summary>
	///     Whether queries without any search parameters are allowed
	 /// </summary>
	public bool SupportsRawSearch { get; init; }
}
