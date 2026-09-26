using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Indexers;
using Submarine.Core.Languages;
using Submarine.Core.Parser;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Release.Usenet;
using Submarine.Infrastructure.Grab;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Search;

namespace Submarine.Api.Features.Releases;

/// <summary>
///     Grabs a release found by an earlier interactive search, or pushes a release from an external tool through the
///     decision pipeline.
/// </summary>
public sealed class ReleasesModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		endpoints.MapPost("/api/v1/releases/grab", GrabAsync);
		endpoints.MapPost("/api/v1/releases/push", PushAsync);
	}

	private static async Task<Results<Ok<GrabResultResource>, NotFound>> GrabAsync(
		SubmarineDbContext db,
		ReleaseResultCache cache,
		DecisionContextFactory contextFactory,
		IDownloadDecisionMaker decisionMaker,
		IGrabService grabService,
		IValidator<GrabReleaseRequest> validator,
		[FromBody] GrabReleaseRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);

		if (!cache.TryGet(request.Guid, request.IndexerId, out var candidate))
		{
			return TypedResults.NotFound();
		}

		var version = await db.MediaVersions.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == request.MediaVersionId, cancellationToken);
		if (version is null
		    || (request.SeriesId is { } requestSeriesId && version.SeriesId != requestSeriesId)
		    || (request.MovieId is { } requestMovieId && version.MovieId != requestMovieId))
		{
			return TypedResults.NotFound();
		}

		candidate = ApplyOverrides(candidate, request);

		var context = await contextFactory.BuildAsync(version, request.EpisodeIds, cancellationToken);

		var decision = decisionMaker.Decide(candidate, context);
		if (request.Override && !decision.Approved)
		{
			decision = decision with { Approved = true, Rejections = [] };
		}

		var outcome = await grabService.GrabAsync(decision, request.MediaVersionId, request.SeriesId, request.EpisodeIds, request.MovieId, cancellationToken);

		return TypedResults.Ok(ToResource(outcome));
	}

	private static async Task<Ok<GrabResultResource>> PushAsync(
		SubmarineDbContext db,
		ReleaseMatcher matcher,
		DecisionContextFactory contextFactory,
		IDownloadDecisionMaker decisionMaker,
		IGrabService grabService,
		IValidator<PushReleaseRequest> validator,
		IParser<TorrentRelease> torrentParser,
		IParser<UsenetRelease> usenetParser,
		[FromBody] PushReleaseRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);

		var protocol = Enum.Parse<Protocol>(request.Protocol, ignoreCase: true);
		var parsed = protocol == Protocol.USENET ? (BaseRelease)usenetParser.Parse(request.Title) : torrentParser.Parse(request.Title);

		var info = new ReleaseInfo
		{
			Guid = request.DownloadUrl,
			Title = request.Title,
			DownloadUrl = request.DownloadUrl,
			Size = request.Size,
			Protocol = protocol,
			Indexer = request.Indexer,
			PublishDate = DateTime.UtcNow
		};

		var match = await matcher.MatchLibraryAsync(parsed, cancellationToken);
		if (match is null)
		{
			return TypedResults.Ok(new GrabResultResource(false, null, null, null, "No matching series or movie was found"));
		}

		var candidate = new ReleaseCandidate(parsed, info, null, match.SeriesId, match.MovieId, match.EpisodeIds);
		var versions = match.SeriesId is { } seriesId
			? await db.MediaVersions.Where(version => version.SeriesId == seriesId).ToListAsync(cancellationToken)
			: await db.MediaVersions.Where(version => version.MovieId == match.MovieId).ToListAsync(cancellationToken);

		foreach (var version in versions)
		{
			var context = await contextFactory.BuildAsync(version, match.EpisodeIds, cancellationToken);
			var decision = decisionMaker.Decide(candidate, context);

			if (!decision.Approved)
			{
				continue;
			}

			var outcome = await grabService.GrabAsync(decision, version.Id, match.SeriesId, match.EpisodeIds, match.MovieId, cancellationToken);
			return TypedResults.Ok(ToResource(outcome));
		}

		return TypedResults.Ok(new GrabResultResource(false, null, null, null, "Release was rejected for every applicable media version"));
	}

	private static ReleaseCandidate ApplyOverrides(ReleaseCandidate candidate, GrabReleaseRequest request)
	{
		var parsed = candidate.Parsed;

		if (request.QualitySource is not null || request.QualityResolution is not null)
		{
			var source = request.QualitySource is null ? parsed.Quality.Resolution.Source : Enum.Parse<QualitySource>(request.QualitySource, ignoreCase: true);
			var resolution = request.QualityResolution is null
				? parsed.Quality.Resolution.Resolution
				: Enum.Parse<QualityResolution>(request.QualityResolution, ignoreCase: true);
			parsed = parsed with { Quality = parsed.Quality with { Resolution = new QualityResolutionModel(source, resolution) } };
		}

		if (request.Languages is not null)
		{
			parsed = parsed with { Languages = [.. request.Languages.Select(language => Enum.Parse<Language>(language, ignoreCase: true))] };
		}

		return candidate with { Parsed = parsed };
	}

	private static GrabResultResource ToResource(GrabOutcome outcome)
		=> outcome switch
		{
			GrabbedOutcome grabbed => new GrabResultResource(true, grabbed.Download.DownloadClientId, grabbed.Download.DownloadId, null, null),
			PendingOutcome pending => new GrabResultResource(false, null, null, pending.Pending.Id, "Release held as pending"),
			_ => new GrabResultResource(false, null, null, null, "Unknown outcome")
		};
}

/// <summary>Request to grab a release previously returned by an interactive search.</summary>
/// <param name="Guid">The release's indexer guid, as returned by the search.</param>
/// <param name="IndexerId">Id of the indexer the release came from.</param>
/// <param name="MediaVersionId">Id of the media version to grab for.</param>
/// <param name="SeriesId">Id of the matched series, when grabbing for a series.</param>
/// <param name="EpisodeIds">Ids of the matched episodes, when grabbing for a series.</param>
/// <param name="MovieId">Id of the matched movie, when grabbing for a movie.</param>
/// <param name="QualitySource">Overrides the parsed quality source, if set.</param>
/// <param name="QualityResolution">Overrides the parsed quality resolution, if set.</param>
/// <param name="Languages">Overrides the parsed languages, if set.</param>
/// <param name="Override">
///     When true, grabs the release even if the decision engine rejects it (e.g. a delay hold or a filter match).
/// </param>
public sealed record GrabReleaseRequest(
	string Guid,
	int IndexerId,
	int MediaVersionId,
	int? SeriesId,
	List<int>? EpisodeIds,
	int? MovieId,
	string? QualitySource,
	string? QualityResolution,
	List<string>? Languages,
	bool Override = false);

/// <summary>Request to push a release from an external tool through the decision pipeline.</summary>
public sealed record PushReleaseRequest(string Title, string DownloadUrl, string Protocol, string? Indexer, long? Size);

/// <summary>The result of a grab attempt.</summary>
public sealed record GrabResultResource(bool Grabbed, int? DownloadClientId, string? DownloadId, int? PendingReleaseId, string? Message = null);

/// <summary>Validator for <see cref="GrabReleaseRequest" />.</summary>
public sealed class GrabReleaseRequestValidator : AbstractValidator<GrabReleaseRequest>
{
	/// <inheritdoc />
	public GrabReleaseRequestValidator()
	{
		RuleFor(request => request.Guid).NotEmpty();
		RuleFor(request => request.IndexerId).GreaterThan(0);
		RuleFor(request => request.MediaVersionId).GreaterThan(0);
		RuleFor(request => request).Must(request => request.SeriesId is not null || request.MovieId is not null)
			.WithMessage("Either seriesId or movieId is required");
		RuleFor(request => request.QualitySource)
			.Must(value => value is null || Enum.TryParse<QualitySource>(value, ignoreCase: true, out _))
			.WithMessage("QualitySource is not a recognized quality source");
		RuleFor(request => request.QualityResolution)
			.Must(value => value is null || Enum.TryParse<QualityResolution>(value, ignoreCase: true, out _))
			.WithMessage("QualityResolution is not a recognized quality resolution");
		RuleForEach(request => request.Languages)
			.Must(value => Enum.TryParse<Language>(value, ignoreCase: true, out _))
			.WithMessage("Language is not a recognized language");
	}
}

/// <summary>Validator for <see cref="PushReleaseRequest" />.</summary>
public sealed class PushReleaseRequestValidator : AbstractValidator<PushReleaseRequest>
{
	/// <inheritdoc />
	public PushReleaseRequestValidator()
	{
		RuleFor(request => request.Title).NotEmpty();
		RuleFor(request => request.DownloadUrl).NotEmpty();
		RuleFor(request => request.Protocol).Must(value => Enum.TryParse<Protocol>(value, ignoreCase: true, out _))
			.WithMessage("Protocol must be BITTORRENT, USENET or XDCC");
	}
}
