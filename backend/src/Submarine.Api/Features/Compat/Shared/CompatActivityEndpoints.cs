using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Features.Calendar;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Core.Profiles;
using Submarine.Core.Quality;
using Submarine.Api.Features.History;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Shared;

/// <summary>Read-only history, calendar, and wanted endpoints for Sonarr and Radarr.</summary>
public sealed class CompatActivityEndpoints : IEndpointModule
{
	public void Map(IEndpointRouteBuilder endpoints)
	{
		MapFacade(endpoints, "sonarr", true);
		MapFacade(endpoints, "radarr", false);
	}

	private static void MapFacade(IEndpointRouteBuilder endpoints, string facade, bool seriesFacade)
	{
		var group = CompatRoutes.CreateRestGroup(endpoints, facade, "v3");
		group.MapGet("/history", (HttpRequest request, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
			=> HistoryAsync(seriesFacade, true, request, db, selection, ct));
		if (seriesFacade)
			group.MapGet("/history/series", (HttpRequest request, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
				=> HistoryAsync(true, false, request, db, selection, ct));
		else
			group.MapGet("/history/movie", (HttpRequest request, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
				=> HistoryAsync(false, false, request, db, selection, ct));
		group.MapPost("/history/failed/{id:int}", (int id, SubmarineDbContext db, CompatVersionSelection selection, HistoryFailureService failureService, CancellationToken ct)
			=> MarkFailedAsync(seriesFacade, id, db, selection, failureService, ct));
		group.MapGet("/calendar", (DateTime? start, DateTime? end, bool? unmonitored, bool? includeSeries, bool? includeEpisodeFile, bool? includeEpisodeImages, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
			=> CalendarAsync(seriesFacade, start, end, unmonitored ?? false, includeSeries ?? false, includeEpisodeFile ?? false, includeEpisodeImages ?? false, db, selection, ct));
		group.MapGet("/wanted/missing", (HttpRequest request, bool? includeUnmonitored, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
			=> WantedAsync(seriesFacade, false, includeUnmonitored ?? false, CompatPageRequest.FromQuery(request.Query), db, selection, ct));
		group.MapGet("/wanted/cutoff", (HttpRequest request, bool? includeUnmonitored, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
			=> WantedAsync(seriesFacade, true, includeUnmonitored ?? false, CompatPageRequest.FromQuery(request.Query), db, selection, ct));
	}

	private static async Task<IResult> HistoryAsync(bool seriesFacade, bool pagedResponse, HttpRequest request, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
	{
		var page = CompatPageRequest.FromQuery(request.Query);
		var events = await db.HistoryEvents.AsNoTracking()
			.Include(x => x.Series).Include(x => x.Episode).Include(x => x.Movie)
			.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
			.ToListAsync(ct);
		var selected = new Dictionary<(bool Series, int Id), int>();
		foreach (var id in events.Where(x => seriesFacade ? x.SeriesId != null : x.MovieId != null)
			.Select(x => seriesFacade ? x.SeriesId!.Value : x.MovieId!.Value).Distinct())
		{
			var binding = seriesFacade
				? await selection.GetForSeriesAsync(id, ct)
				: await selection.GetForMovieAsync(id, ct);
			if (binding?.MediaVersionId is { } versionId)
				selected[(seriesFacade, id)] = versionId;
		}

		var query = request.Query;
		var eventTypeId = ReadQueryInt(query, "eventType");
		var seriesId = ReadQueryInt(query, "seriesId");
		var episodeId = ReadQueryInt(query, "episodeId");
		var movieIds = ReadQueryIds(query, "movieIds");
		if (ReadQueryInt(query, "movieId") is { } movieId)
			movieIds.Add(movieId);
		var downloadId = query.TryGetValue("downloadId", out var downloadIdValue) ? downloadIdValue.ToString() : null;
		var filter = query.TryGetValue("filter", out var filterValue) ? filterValue.ToString() : string.Empty;
		var records = events.Where(x => seriesFacade ? x.SeriesId != null : x.MovieId != null)
			.Where(x => x.MediaVersionId is null || selected.TryGetValue((seriesFacade, seriesFacade ? x.SeriesId!.Value : x.MovieId!.Value), out var versionId) && versionId == x.MediaVersionId)
			.Where(x => seriesFacade
				? (seriesId is null || x.SeriesId == seriesId) && (episodeId is null || x.EpisodeId == episodeId)
				: (movieIds.Count == 0 || x.MovieId is { } id && movieIds.Contains(id)))
			.Where(x => eventTypeId is null || EventTypeId(x.Type) == eventTypeId)
			.Where(x => downloadId is null || x.DownloadId == downloadId)
			.Where(x => string.IsNullOrEmpty(filter) || EventType(x.Type, seriesFacade).Equals(filter, StringComparison.OrdinalIgnoreCase))
			.ToList();
		if (page.SortKey.Equals("id", StringComparison.OrdinalIgnoreCase))
			records = page.SortDirection == "descending" ? records.OrderByDescending(x => x.Id).ToList() : records.OrderBy(x => x.Id).ToList();
		else if (page.SortKey.Equals("eventType", StringComparison.OrdinalIgnoreCase))
			records = page.SortDirection == "descending" ? records.OrderByDescending(x => EventType(x.Type, seriesFacade)).ToList() : records.OrderBy(x => EventType(x.Type, seriesFacade)).ToList();
		else if (page.SortKey.Equals("sourceTitle", StringComparison.OrdinalIgnoreCase))
			records = page.SortDirection == "descending" ? records.OrderByDescending(x => x.SourceTitle).ToList() : records.OrderBy(x => x.SourceTitle).ToList();
		else if (page.SortDirection == "descending")
			records = records.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToList();
		else
			records = records.OrderBy(x => x.Date).ThenBy(x => x.Id).ToList();

		if (!pagedResponse)
			return Results.Json(records.Select(x => HistoryRecord(x, seriesFacade)).ToList(), CompatJson.Options);
		var paged = page.Apply(records);
		return Results.Json(new CompatPagedResult<object>(paged.Records.Select(x => HistoryRecord(x, seriesFacade)).ToList(), paged.TotalRecords, paged.Page, paged.PageSize, paged.SortKey, paged.SortDirection), CompatJson.Options);
	}

	private static async Task<IResult> MarkFailedAsync(
		bool seriesFacade,
		int id,
		SubmarineDbContext db,
		CompatVersionSelection selection,
		HistoryFailureService failureService,
		CancellationToken ct)
	{
		var grabbed = await db.HistoryEvents.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == id && (seriesFacade ? x.SeriesId != null : x.MovieId != null), ct);
		if (grabbed?.MediaVersionId is not { } versionId)
			return CompatErrors.Message("History event not found.", StatusCodes.Status404NotFound);
		var titleId = seriesFacade ? grabbed.SeriesId!.Value : grabbed.MovieId!.Value;
		var binding = await GetBindingAsync(seriesFacade, titleId, db, selection, ct);
		if (binding?.MediaVersionId != versionId)
			return CompatErrors.Message("History event not found.", StatusCodes.Status404NotFound);

		var result = await failureService.MarkFailedAsync(id, ct);
		if (result.FailedEvent is { } failedEvent)
			return Results.Json(HistoryRecord(failedEvent, seriesFacade), CompatJson.Options);
		return result.Error is { } error
			? CompatErrors.Message(error, StatusCodes.Status400BadRequest)
			: CompatErrors.Message("History event not found.", StatusCodes.Status404NotFound);
	}

	private static object HistoryRecord(HistoryEvent item, bool seriesFacade)
		=> new
		{
			id = item.Id,
			eventType = EventType(item.Type, seriesFacade),
			date = item.Date,
			seriesId = item.SeriesId,
			episodeId = item.EpisodeId,
			movieId = item.MovieId,
			mediaVersionId = item.MediaVersionId,
			sourceTitle = item.SourceTitle,
			quality = QualityProjection(item.Quality),
			data = DataProjection(item.Data),
			series = item.Series is null ? null : new { id = item.Series.Id, title = item.Series.Title },
			episode = item.Episode is null ? null : new { id = item.Episode.Id, title = item.Episode.Title, seasonNumber = item.Episode.SeasonNumber, episodeNumber = item.Episode.EpisodeNumber },
			movie = item.Movie is null ? null : new { id = item.Movie.Id, title = item.Movie.Title },
			languages = item.Languages?.Select(language => LanguageProjection(language, seriesFacade ? "sonarr" : "radarr")).ToArray() ?? [],
			downloadId = item.DownloadId,
			episodeFile = (object?)null,
			movieFile = (object?)null
		};
	private static string EventType(HistoryEventType type, bool series)
		=> type switch
		{
			HistoryEventType.GRABBED => "grabbed",
			HistoryEventType.IMPORTED or HistoryEventType.UPGRADED => "downloadFolderImported",
			HistoryEventType.RENAMED => series ? "episodeFileRenamed" : "movieFileRenamed",
			HistoryEventType.DELETED => series ? "episodeFileDeleted" : "movieFileDeleted",
			HistoryEventType.FAILED => "downloadFailed",
			HistoryEventType.IGNORED => "downloadIgnored",
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
		};
	private static int EventTypeId(HistoryEventType type)
		=> type switch
		{
			HistoryEventType.GRABBED => 1,
			HistoryEventType.IMPORTED or HistoryEventType.UPGRADED => 3,
			HistoryEventType.DELETED => 4,
			HistoryEventType.RENAMED => 5,
			HistoryEventType.FAILED => 6,
			HistoryEventType.IGNORED => 7,
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
		};

	private static int? ReadQueryInt(IQueryCollection query, string name)
		=> query.TryGetValue(name, out var value) && int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;

	private static List<int> ReadQueryIds(IQueryCollection query, string name)
		=> query.TryGetValue(name, out var values)
			? values.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(value => int.TryParse(value, out var parsed) && parsed > 0 ? parsed : 0)
				.Where(value => value > 0).Distinct().ToList()
			: [];


	private static object? QualityProjection(QualityModel? quality)
	{
		if (quality is null) return null;
		var resolution = quality.Resolution;
		return new
		{
			quality = new
			{
				id = Array.FindIndex(QualityResolutionModel.All, known => known.Source == resolution.Source && known.Resolution == resolution.Resolution) + 1,
				name = resolution.Name,
				source = resolution.Source?.ToString(),
				resolution = resolution.Resolution?.ToString()
			},
			revision = new { version = quality.Revision.Version, isRepack = quality.Revision.IsRepack, isProper = quality.Revision.IsProper, isReal = quality.Revision.IsReal }
		};
	}
	private static object LanguageProjection(Language language, string facade)
		=> new
		{
			id = CompatLanguageMap.ToUpstreamId(language, facade),
			name = CompatLanguageMap.Name(language)
		};

	private static JsonElement? DataProjection(string? data)
	{
		if (string.IsNullOrWhiteSpace(data)) return null;
		using var document = JsonDocument.Parse(data);
		return document.RootElement.Clone();
	}


	private static async Task<IResult> CalendarAsync(bool seriesFacade, DateTime? start, DateTime? end, bool unmonitored, bool includeSeries, bool includeEpisodeFile, bool includeEpisodeImages, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
	{
		var (from, to) = CalendarModule.NormalizeRange(start, end);
		var result = new List<object>();
		if (seriesFacade)
		{
			var episodes = await db.Episodes.AsNoTracking().Include(x => x.Series).Include(x => x.Files)
				.Where(x => x.AirDateUtc != null && x.AirDateUtc >= from && x.AirDateUtc <= to)
				.Where(x => unmonitored || (x.Monitored && x.Series.Monitored))
				.OrderBy(x => x.AirDateUtc).ThenBy(x => x.Id).ToListAsync(ct);
			var bindings = new Dictionary<int, int>();
			foreach (var id in episodes.Select(x => x.SeriesId).Distinct())
				if (await GetBindingAsync(true, id, db, selection, ct) is { MediaVersionId: { } versionId }) bindings[id] = versionId;
			foreach (var episode in episodes.Where(x => bindings.ContainsKey(x.SeriesId)))
			{
				var file = episode.Files.FirstOrDefault(x => x.MediaVersionId == bindings[episode.SeriesId]);
				result.Add(new { id = episode.Id, seriesId = episode.SeriesId, episodeFileId = file?.Id, seasonNumber = episode.SeasonNumber, episodeNumber = episode.EpisodeNumber, title = episode.Title, airDateUtc = episode.AirDateUtc, hasFile = file is not null, monitored = episode.Monitored && episode.Series.Monitored, series = includeSeries ? new { id = episode.Series.Id, title = episode.Series.Title, tvdbId = episode.Series.TvdbId } : null, episodeFile = includeEpisodeFile ? (object?)null : null, images = includeEpisodeImages ? Array.Empty<object>() : null });
			}
		}
		else
		{
			var movies = await db.Movies.AsNoTracking().Include(x => x.Files)
				.Where(x => unmonitored || x.Monitored)
				.OrderBy(x => x.Id).ToListAsync(ct);
			foreach (var movie in movies)
			{
				var binding = await GetBindingAsync(false, movie.Id, db, selection, ct);
				if (binding is null) continue;
				var file = movie.Files.FirstOrDefault(x => x.MediaVersionId == binding.MediaVersionId);
				var hasDateInRange = movie.InCinemasDate is { } inCinemas && inCinemas >= from && inCinemas <= to
					|| movie.DigitalReleaseDate is { } digital && digital >= from && digital <= to
					|| movie.PhysicalReleaseDate is { } physical && physical >= from && physical <= to;
				if (!hasDateInRange) continue;
				result.Add(new { id = movie.Id, tmdbId = movie.TmdbId, imdbId = movie.ImdbId, title = movie.Title, year = movie.Year, inCinemas = movie.InCinemasDate, physicalRelease = movie.PhysicalReleaseDate, digitalRelease = movie.DigitalReleaseDate, hasFile = file is not null, isAvailable = file is not null, monitored = movie.Monitored, status = MovieStatusName(movie.Status), movieFile = (object?)null });
			}
		}
		return Results.Json(result, CompatJson.Options);
	}
	private static string MovieStatusName(MovieStatus status)
		=> status switch
		{
			MovieStatus.ANNOUNCED => "announced",
			MovieStatus.IN_CINEMAS => "inCinemas",
			MovieStatus.RELEASED => "released",
			_ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
		};

	private static async Task<IResult> WantedAsync(bool seriesFacade, bool cutoff, bool includeUnmonitored, CompatPageRequest page, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
	{
		var records = new List<WantedRecord>();
		var availabilityDelayDays = !seriesFacade && !cutoff
			? await db.IndexerConfig.AsNoTracking().Select(x => x.AvailabilityDelayDays).SingleAsync(ct)
			: 0;
		if (seriesFacade && cutoff)
		{
			var files = await db.EpisodeFiles.AsNoTracking().Include(x => x.Series).Include(x => x.Episodes).OrderBy(x => x.Id).ToListAsync(ct);
			foreach (var file in files)
			{
				var binding = await GetBindingAsync(true, file.SeriesId, db, selection, ct);
				if (binding is null || file.MediaVersionId != binding.MediaVersionId || file.Episodes.Count == 0) continue;
				if (!includeUnmonitored && (!file.Series.Monitored || file.Episodes.All(x => !x.Monitored))) continue;
				var qualityProfileId = await db.MediaVersions.AsNoTracking().Where(x => x.Id == binding.MediaVersionId).Select(x => x.QualityProfileId).FirstOrDefaultAsync(ct);
				var qualityProfile = await db.QualityProfiles.AsNoTracking().Where(x => x.Id == qualityProfileId).FirstOrDefaultAsync(ct);
				if (qualityProfile is null || qualityProfile.MeetsCutoff(file.Quality)) continue;
				var episode = file.Episodes.OrderBy(x => x.SeasonNumber).ThenBy(x => x.EpisodeNumber).First();
				records.Add(new(episode.Id, "episode", episode.LastSearchTime, new { id = episode.Id, seriesId = episode.SeriesId, episodeId = episode.Id, title = episode.Title, seriesTitle = file.Series.Title, seasonNumber = episode.SeasonNumber, episodeNumber = episode.EpisodeNumber, airDateUtc = episode.AirDateUtc, lastSearchTime = episode.LastSearchTime, monitored = file.Series.Monitored, episodeFileId = file.Id, episodeFile = (object?)null }));
			}
		}
		else if (seriesFacade)
		{
			var episodes = await db.Episodes.AsNoTracking().Include(x => x.Series).Include(x => x.Files).OrderBy(x => x.Id).ToListAsync(ct);
			foreach (var episode in episodes)
			{
				var binding = await GetBindingAsync(true, episode.SeriesId, db, selection, ct);
				if (binding is null || (!includeUnmonitored && (!episode.Monitored || !episode.Series.Monitored))) continue;
				if (!await db.MediaVersions.AsNoTracking().AnyAsync(x => x.Id == binding.MediaVersionId && x.Monitored, ct)) continue;
				var file = episode.Files.FirstOrDefault(x => x.MediaVersionId == binding.MediaVersionId);
				if (file is not null || episode.AirDateUtc is null || episode.AirDateUtc > DateTime.UtcNow) continue;
				records.Add(new(episode.Id, "episode", episode.LastSearchTime, new { id = episode.Id, seriesId = episode.SeriesId, episodeId = episode.Id, title = episode.Title, seriesTitle = episode.Series.Title, seasonNumber = episode.SeasonNumber, episodeNumber = episode.EpisodeNumber, airDateUtc = episode.AirDateUtc, lastSearchTime = episode.LastSearchTime, monitored = episode.Monitored && episode.Series.Monitored, episodeFileId = (int?)null, episodeFile = (object?)null }));
			}
		}
		else if (cutoff)
		{
			var files = await db.MovieFiles.AsNoTracking().Include(x => x.Movie).OrderBy(x => x.Id).ToListAsync(ct);
			foreach (var file in files)
			{
				var binding = await GetBindingAsync(false, file.MovieId, db, selection, ct);
				if (binding is null || file.MediaVersionId != binding.MediaVersionId || (!includeUnmonitored && !file.Movie.Monitored)) continue;
				var qualityProfileId = await db.MediaVersions.AsNoTracking().Where(x => x.Id == binding.MediaVersionId).Select(x => x.QualityProfileId).FirstOrDefaultAsync(ct);
				var qualityProfile = await db.QualityProfiles.AsNoTracking().Where(x => x.Id == qualityProfileId).FirstOrDefaultAsync(ct);
				if (qualityProfile is null || qualityProfile.MeetsCutoff(file.Quality)) continue;
				records.Add(new(file.MovieId, "movie", file.Movie.LastSearchTime, new { id = file.MovieId, movieId = file.MovieId, title = file.Movie.Title, year = file.Movie.Year, lastSearchTime = file.Movie.LastSearchTime, monitored = file.Movie.Monitored, movieFileId = file.Id, movieFile = (object?)null }));
			}
		}
		else
		{
			var movies = await db.Movies.AsNoTracking().Include(x => x.Files).OrderBy(x => x.Id).ToListAsync(ct);
			foreach (var movie in movies)
			{
				var binding = await GetBindingAsync(false, movie.Id, db, selection, ct);
				if (binding is null || (!includeUnmonitored && !movie.Monitored) || movie.Files.Any(x => x.MediaVersionId == binding.MediaVersionId)) continue;
				if (!await db.MediaVersions.AsNoTracking().AnyAsync(x => x.Id == binding.MediaVersionId && x.Monitored, ct)) continue;
				var date = movie.MinimumAvailability == MinimumAvailability.IN_CINEMAS ? movie.InCinemasDate : movie.PhysicalReleaseDate ?? movie.DigitalReleaseDate ?? movie.InCinemasDate;
				if (date is null || date.Value.AddDays(availabilityDelayDays) > DateTime.UtcNow) continue;
				records.Add(new(movie.Id, "movie", movie.LastSearchTime, new { id = movie.Id, movieId = movie.Id, title = movie.Title, year = movie.Year, physicalRelease = movie.PhysicalReleaseDate, digitalRelease = movie.DigitalReleaseDate, inCinemas = movie.InCinemasDate, lastSearchTime = movie.LastSearchTime, monitored = movie.Monitored, movieFileId = (int?)null, movieFile = (object?)null }));
			}
		}
		var sorted = page.SortKey.Equals("lastSearchTime", StringComparison.OrdinalIgnoreCase)
			? (page.SortDirection == "descending" ? records.OrderByDescending(x => x.LastSearchTime).ThenBy(x => x.Id) : records.OrderBy(x => x.LastSearchTime).ThenBy(x => x.Id))
			: (page.SortDirection == "descending" ? records.OrderByDescending(x => x.Id) : records.OrderBy(x => x.Id));
		var paged = page.Apply(sorted.Select(x => x.Value));
		return Results.Json(paged, CompatJson.Options);
	}
	private static async Task<CompatLibraryBinding?> GetBindingAsync(bool seriesFacade, int id, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
	{
		if (!await db.MediaVersions.AsNoTracking().AnyAsync(x => seriesFacade ? x.SeriesId == id : x.MovieId == id, ct)) return null;
		return seriesFacade ? await selection.GetForSeriesAsync(id, ct) : await selection.GetForMovieAsync(id, ct);
	}


	private sealed record WantedRecord(int Id, string Type, DateTime? LastSearchTime, object Value);
}
