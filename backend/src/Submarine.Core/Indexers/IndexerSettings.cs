using System.Collections.Generic;

namespace Submarine.Core.Indexers;

/// <summary>
///     Seed rules an indexer demands before a download may be removed
/// </summary>
/// <param name="SeedRatio">The minimum ratio, if any</param>
/// <param name="SeedTimeMinutes">The minimum seed time in minutes, if any</param>
/// <param name="SeasonPackSeedTimeMinutes">The minimum seed time in minutes for season packs, if any</param>
public record IndexerSeedCriteria(
	double? SeedRatio = null,
	int? SeedTimeMinutes = null,
	int? SeasonPackSeedTimeMinutes = null);

/// <summary>
///     Settings of a Torznab indexer
/// </summary>
/// <param name="BaseUrl">The base url of the indexer</param>
/// <param name="ApiPath">The path of the api endpoint relative to the base url</param>
/// <param name="ApiKey">The api key, if required</param>
/// <param name="Categories">The standard category ids to search</param>
/// <param name="AnimeCategories">The standard category ids to search for anime</param>
/// <param name="AdditionalParameters">Extra query parameters appended to every request</param>
/// <param name="MinimumSeeders">The minimum amount of seeders a release needs</param>
/// <param name="SeedCriteria">The seed rules of the indexer, if any</param>
public record TorznabSettings(
	string BaseUrl,
	string ApiPath = "/api",
	string? ApiKey = null,
	IReadOnlyList<int>? Categories = null,
	IReadOnlyList<int>? AnimeCategories = null,
	string? AdditionalParameters = null,
	int MinimumSeeders = 1,
	IndexerSeedCriteria? SeedCriteria = null);

/// <summary>
///     Settings of a Newznab indexer
/// </summary>
/// <param name="BaseUrl">The base url of the indexer</param>
/// <param name="ApiPath">The path of the api endpoint relative to the base url</param>
/// <param name="ApiKey">The api key, if required</param>
/// <param name="Categories">The standard category ids to search</param>
/// <param name="AnimeCategories">The standard category ids to search for anime</param>
/// <param name="AdditionalParameters">Extra query parameters appended to every request</param>
public record NewznabSettings(
	string BaseUrl,
	string ApiPath = "/api",
	string? ApiKey = null,
	IReadOnlyList<int>? Categories = null,
	IReadOnlyList<int>? AnimeCategories = null,
	string? AdditionalParameters = null);

/// <summary>
///     Settings of a Cardigann indexer
/// </summary>
/// <param name="DefinitionId">The id of the Cardigann definition</param>
/// <param name="BaseUrl">The base url overriding the first definition link, if set</param>
/// <param name="Fields">The user supplied values of the definition setting fields</param>
public record CardigannSettings(
	string DefinitionId,
	string? BaseUrl = null,
	IReadOnlyDictionary<string, string>? Fields = null);
