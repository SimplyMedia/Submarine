using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Modules;
using Submarine.Infrastructure.Search;

namespace Submarine.Api.Features.Search;

/// <summary>
///     Interactive (manual) search over the configured indexers.
/// </summary>
public sealed class SearchModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
		=> endpoints.MapGet("/api/v1/search", SearchAsync);

	private static async Task<Ok<IReadOnlyList<ReleaseResource>>> SearchAsync(
		InteractiveSearchService searchService,
		[FromQuery] int? seriesId,
		[FromQuery] int? seasonNumber,
		[FromQuery] int? episodeId,
		[FromQuery] int? movieId,
		[FromQuery] string? term,
		[FromQuery] int? mediaVersionId,
		[FromQuery] string? categories,
		[FromQuery] string? indexerIds,
		[FromQuery] string? type,
		[FromQuery] int? page,
		[FromQuery] int? pageSize,
		CancellationToken cancellationToken)
	{
		var results = await searchService.SearchAsync(
			seriesId, seasonNumber, episodeId, movieId, term, mediaVersionId,
			ParseIds(categories), ParseIds(indexerIds), type, cancellationToken);

		IEnumerable<SearchResult> page1 = results;
		if (page is { } pageNumber && pageSize is { } size and > 0)
		{
			page1 = results.Skip((Math.Max(pageNumber, 1) - 1) * size).Take(size);
		}

		return TypedResults.Ok((IReadOnlyList<ReleaseResource>)[.. page1.Select(ToResource)]);
	}

	private static List<int>? ParseIds(string? value)
		=> string.IsNullOrWhiteSpace(value)
			? null
			: [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse)];

	private static ReleaseResource ToResource(SearchResult result)
	{
		var info = result.Candidate.Info;
		var parsed = result.Candidate.Parsed;

		return new ReleaseResource(
			info.Guid,
			info.Title,
			info.IndexerId,
			info.Indexer,
			info.Protocol.ToString(),
			info.Size,
			info.Seeders,
			info.Leechers,
			info.PublishDate,
			info.InfoUrl,
			info.MagnetUrl is not null,
			[.. info.IndexerFlags.Select(flag => flag.ToString())],
			parsed.Quality.Resolution.Name,
			[.. parsed.Languages.Select(language => language.ToString())],
			parsed.ReleaseGroup,
			result.Candidate.MatchedSeriesId,
			result.Candidate.MatchedMovieId,
			result.Candidate.EpisodeIds ?? [],
			[.. result.Decisions.Select(decision => new ReleaseVersionDecisionResource(
				decision.MediaVersionId,
				decision.Approved,
				decision.Score,
				[.. decision.Rejections.Select(rejection => rejection.Reason)],
				decision.CustomFormatScore))]);
	}
}

/// <summary>One release found by an interactive search.</summary>
public sealed record ReleaseResource(
	string Guid,
	string Title,
	int? IndexerId,
	string? Indexer,
	string Protocol,
	long? Size,
	int? Seeders,
	int? Leechers,
	DateTime? PublishDate,
	string? InfoUrl,
	bool HasMagnet,
	IReadOnlyList<string> IndexerFlags,
	string QualityName,
	IReadOnlyList<string> Languages,
	string? ReleaseGroup,
	int? MappedSeriesId,
	int? MappedMovieId,
	IReadOnlyList<int> EpisodeIds,
	IReadOnlyList<ReleaseVersionDecisionResource> Decisions);

/// <summary>The decision made for one media version against a release.</summary>
public sealed record ReleaseVersionDecisionResource(int MediaVersionId, bool Approved, int Score, IReadOnlyList<string> Rejections, int CustomFormatScore);
