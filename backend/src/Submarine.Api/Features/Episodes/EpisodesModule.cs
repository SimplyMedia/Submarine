using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Api.Features.Series;
using Submarine.Core.Entities;
using Submarine.Core.Languages;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Library;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Episodes;

/// <summary>
///     Episode library endpoints.
/// </summary>
public sealed class EpisodesModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/episodes");
		group.MapGet("/", ListAsync);
		group.MapPut("/monitor", MonitorAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPut("/{id:int}", UpdateAsync);
	}

	private static async Task<Ok<List<EpisodeDto>>> ListAsync(
		SubmarineDbContext db,
		[FromQuery] int seriesId,
		[FromQuery] int? seasonNumber,
		[FromQuery] bool includeFiles = false,
		CancellationToken cancellationToken = default)
	{
		if (!await db.Series.AnyAsync(x => x.Id == seriesId, cancellationToken))
		{
			throw new KeyNotFoundException($"Series {seriesId} not found");
		}

		var episodes = db.Episodes.AsNoTracking()
			.Include(x => x.Files)
			.Where(x => x.SeriesId == seriesId);
		if (seasonNumber.HasValue)
		{
			episodes = episodes.Where(x => x.SeasonNumber == seasonNumber.Value);
		}

		var list = await episodes
			.OrderBy(x => x.SeasonNumber)
			.ThenBy(x => x.EpisodeNumber)
			.ToListAsync(cancellationToken);
		return TypedResults.Ok(list.Select(x => ToDto(x, includeFiles)).ToList());
	}

	private static async Task<Ok<EpisodeDto>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var episode = await db.Episodes.AsNoTracking()
			.Include(x => x.Files)
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Episode {id} not found");
		return TypedResults.Ok(ToDto(episode, true));
	}

	private static async Task<Ok<BulkUpdateResultDto>> MonitorAsync(
		LibraryMutator mutator,
		[FromBody] EpisodeMonitorRequest request,
		CancellationToken cancellationToken)
	{
		var updated = await mutator.SetEpisodesMonitoredAsync(request.EpisodeIds, request.Monitored, cancellationToken);
		return TypedResults.Ok(new BulkUpdateResultDto(updated));
	}

	private static async Task<NoContent> UpdateAsync(
		LibraryMutator mutator,
		int id,
		[FromBody] EpisodeUpdateRequest request,
		CancellationToken cancellationToken)
	{
		await mutator.SetEpisodeMonitoredAsync(id, request.Monitored, cancellationToken);
		return TypedResults.NoContent();
	}

	private static EpisodeDto ToDto(Episode episode, bool includeFiles)
		=> new(
			episode.Id,
			episode.SeriesId,
			episode.SeasonNumber,
			episode.EpisodeNumber,
			episode.AbsoluteEpisodeNumber,
			episode.SceneSeasonNumber,
			episode.SceneEpisodeNumber,
			episode.SceneAbsoluteEpisodeNumber,
			episode.TvdbId,
			episode.TmdbId,
			episode.Title,
			episode.Overview,
			episode.AirDate,
			episode.AirDateUtc,
			episode.Runtime,
			episode.Monitored,
			episode.LastSearchTime,
			episode.Files.Count > 0,
			includeFiles
				? [.. episode.Files.Select(f => new EpisodeFileDto(
					f.Id,
					f.MediaVersionId,
					f.Size,
					f.Quality,
					[.. f.Languages],
					f.ReleaseGroup))]
				: []);
}

/// <summary>Episode update request.</summary>
/// <param name="Monitored">New monitored flag.</param>
public sealed record EpisodeUpdateRequest(bool Monitored);

/// <summary>An episode with a file summary.</summary>
public sealed record EpisodeDto(
	int Id,
	int SeriesId,
	int SeasonNumber,
	int EpisodeNumber,
	int? AbsoluteEpisodeNumber,
	int? SceneSeasonNumber,
	int? SceneEpisodeNumber,
	int? SceneAbsoluteEpisodeNumber,
	int? TvdbId,
	int? TmdbId,
	string? Title,
	string? Overview,
	string? AirDate,
	DateTime? AirDateUtc,
	int? Runtime,
	bool Monitored,
	DateTime? LastSearchTime,
	bool HasFile,
	List<EpisodeFileDto> Files);

/// <summary>Summary of a file containing an episode.</summary>
/// <param name="Id">File id.</param>
/// <param name="MediaVersionId">Owning version.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="Quality">Quality of the file.</param>
/// <param name="Languages">Languages of the file.</param>
/// <param name="ReleaseGroup">Release group.</param>
public sealed record EpisodeFileDto(
	int Id,
	int MediaVersionId,
	long Size,
	QualityModel Quality,
	List<Language> Languages,
	string? ReleaseGroup);
