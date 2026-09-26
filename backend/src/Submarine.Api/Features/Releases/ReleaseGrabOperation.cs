using Microsoft.EntityFrameworkCore;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Languages;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Grab;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Search;
using Submarine.Core.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Submarine.Api.Features.Releases;

/// <summary>Re-evaluates and grabs a cached release through the native decision and grab pipeline.</summary>
public sealed class ReleaseGrabOperation(
	SubmarineDbContext db,
	ReleaseResultCache cache,
	DecisionContextFactory contextFactory,
	IDownloadDecisionMaker decisionMaker,
	IGrabService grabService)
{
	public async Task<GrabResultResource?> ExecuteAsync(GrabReleaseRequest request, CancellationToken cancellationToken = default)
	{
		if (!cache.TryGet(request.Guid, request.IndexerId, out var candidate)) return null;
		var version = await db.MediaVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.MediaVersionId, cancellationToken);
		if (version is null || (request.SeriesId is { } seriesId && version.SeriesId != seriesId) || (request.MovieId is { } movieId && version.MovieId != movieId)) return null;
		candidate = ApplyOverrides(candidate, request);
		var context = await contextFactory.BuildAsync(version, request.EpisodeIds, isInteractive: true, cancellationToken: cancellationToken);
		var decision = decisionMaker.Decide(candidate, context);
		if (request.Override && !decision.Approved) decision = decision with { Approved = true, Rejections = [] };
		var outcome = await grabService.GrabAsync(decision, request.MediaVersionId, request.SeriesId, request.EpisodeIds, request.MovieId, cancellationToken);
		return outcome switch
		{
			GrabbedOutcome grabbed => new GrabResultResource(true, grabbed.Download.DownloadClientId, grabbed.Download.DownloadId, null, null),
			PendingOutcome pending => new GrabResultResource(false, null, null, pending.Pending.Id, "Release held as pending"),
			_ => new GrabResultResource(false, null, null, null, "Unknown outcome")
		};
	}

	private static Submarine.Core.DecisionEngine.ReleaseCandidate ApplyOverrides(Submarine.Core.DecisionEngine.ReleaseCandidate candidate, GrabReleaseRequest request)
	{
		var parsed = candidate.Parsed;
		if (request.QualitySource is not null || request.QualityResolution is not null)
		{
			var source = request.QualitySource is null ? parsed.Quality.Resolution.Source : Enum.Parse<QualitySource>(request.QualitySource, ignoreCase: true);
			var resolution = request.QualityResolution is null ? parsed.Quality.Resolution.Resolution : Enum.Parse<QualityResolution>(request.QualityResolution, ignoreCase: true);
			parsed = parsed with { Quality = parsed.Quality with { Resolution = new QualityResolutionModel(source, resolution) } };
		}
		if (request.Languages is not null) parsed = parsed with { Languages = [.. request.Languages.Select(x => Enum.Parse<Language>(x, ignoreCase: true))] };
		return candidate with { Parsed = parsed };
	}
}

public sealed class ReleaseGrabOperationModule : IServiceModule
{
	public void Register(IServiceCollection services, IConfiguration configuration) => services.AddScoped<ReleaseGrabOperation>();
}
