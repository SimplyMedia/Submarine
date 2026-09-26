using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Features.Compat.Shared;
using Submarine.Api.Modules;
using Submarine.Core.DecisionEngine;
using Submarine.Infrastructure.Grab;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Search;

namespace Submarine.Api.Features.Compat.Sonarr;

/// <summary>
///     Sonarr-shaped interactive release search and grab, over the native <see cref="InteractiveSearchService" />
///     and <see cref="IGrabService" /> used by the native search endpoints, scoped to the facade's selected version.
/// </summary>
public sealed class SonarrReleaseModule : IEndpointModule
{
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = CompatRoutes.CreateRestGroup(endpoints, "sonarr", "v3");
		group.MapGet("/release", SearchAsync);
		group.MapPost("/release", GrabAsync);
	}

	private static async Task<IResult> SearchAsync(
		int? seriesId, int? seasonNumber, int? episodeId,
		SubmarineDbContext db, CompatVersionSelection versions, InteractiveSearchService searchService, CancellationToken ct)
	{
		if (seriesId is null && episodeId is null)
			return CompatErrors.Validation("seriesId", "seriesId or episodeId is required");

		var resolvedSeriesId = seriesId;
		if (resolvedSeriesId is null && episodeId is { } id)
		{
			resolvedSeriesId = await db.Episodes.AsNoTracking().Where(x => x.Id == id).Select(x => (int?)x.SeriesId).FirstOrDefaultAsync(ct);
		}

		var binding = resolvedSeriesId is { } sid ? await versions.GetForSeriesAsync(sid, ct) : null;
		var results = await searchService.SearchAsync(seriesId, seasonNumber, episodeId, null, null, binding?.MediaVersionId, cancellationToken: ct);
		return Results.Json(results.Select(x => ProjectRelease(x, binding?.MediaVersionId)), CompatJson.Options);
	}

	private static async Task<IResult> GrabAsync(
		JsonElement body,
		ReleaseResultCache cache,
		SubmarineDbContext db,
		CompatVersionSelection versions,
		DecisionContextFactory contextFactory,
		IDownloadDecisionMaker decisionMaker,
		IGrabService grabService,
		CancellationToken ct)
	{
		var guid = SonarrModule.String(body, "guid");
		var indexerId = SonarrModule.Int(body, "indexerId");
		if (guid is null || indexerId is null)
			return CompatErrors.Validation("guid", "guid and indexerId are required");

		if (!cache.TryGet(guid, indexerId.Value, out var candidate))
			return CompatErrors.Message("The release was not found; search again before grabbing.", StatusCodes.Status404NotFound);

		int mediaVersionId;
		if (candidate.MatchedSeriesId is { } seriesId)
		{
			var binding = await versions.GetForSeriesAsync(seriesId, ct);
			if (binding is null) return CompatErrors.Message($"Series {seriesId} has no selected version", StatusCodes.Status404NotFound);
			mediaVersionId = binding.MediaVersionId!.Value;
		}
		else
		{
			return CompatErrors.Message("The release did not match a known series.", StatusCodes.Status404NotFound);
		}

		var version = await db.MediaVersions.AsNoTracking().FirstAsync(x => x.Id == mediaVersionId, ct);
		var context = await contextFactory.BuildAsync(version, candidate.EpisodeIds, isInteractive: true, cancellationToken: ct);
		var decision = decisionMaker.Decide(candidate, context);
		var outcome = await grabService.GrabAsync(decision, mediaVersionId, candidate.MatchedSeriesId, candidate.EpisodeIds, null, ct);
		return Results.Json(new { guid, indexerId, grabbed = outcome is GrabbedOutcome }, CompatJson.Options);
	}

	private static object ProjectRelease(SearchResult result, int? selectedVersionId)
	{
		var info = result.Candidate.Info;
		var parsed = result.Candidate.Parsed;
		var decision = result.Decisions.FirstOrDefault(x => x.MediaVersionId == selectedVersionId) ?? result.Decisions.FirstOrDefault();
		var ageHours = info.PublishDate is { } published ? (DateTime.UtcNow - published).TotalHours : 0;
		return new
		{
			guid = info.Guid,
			seriesId = result.Candidate.MatchedSeriesId,
			episodeIds = result.Candidate.EpisodeIds ?? [],
			quality = new { quality = new { id = (int)(parsed.Quality.Resolution.Source ?? default), name = parsed.Quality.Resolution.Name }, revision = parsed.Quality.Revision },
			age = (int)(ageHours / 24),
			ageHours,
			ageMinutes = ageHours * 60,
			size = info.Size ?? 0,
			indexerId = info.IndexerId ?? 0,
			indexer = info.Indexer,
			releaseGroup = parsed.ReleaseGroup,
			title = info.Title,
			fullSeason = false,
			languages = parsed.Languages.Select(x => new { id = (int)x, name = x.ToString() }),
			protocol = info.Protocol.ToString().ToLowerInvariant(),
			publishDate = info.PublishDate,
			downloadUrl = info.DownloadUrl,
			infoUrl = info.InfoUrl,
			seeders = info.Seeders,
			leechers = info.Leechers,
			approved = decision?.Approved ?? false,
			temporarilyRejected = false,
			rejections = decision?.Rejections.Select(x => x.Reason) ?? [],
			rejected = decision is { Approved: false },
			indexerFlags = info.IndexerFlags.Select(x => x.ToString())
		};
	}
}
