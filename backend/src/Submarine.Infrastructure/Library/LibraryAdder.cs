using Submarine.Core.Commands;
using Submarine.Core.Events;
using Submarine.Infrastructure.Commands;
using Submarine.Contracts.Metadata;
using Submarine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Library;
using Submarine.Core.MediaFiles;
using Submarine.Core.Common;
using Submarine.Core.Naming;
using Submarine.Infrastructure.Metadata;

namespace Submarine.Infrastructure.Library;

/// <summary>
///     Options for adding a series.
/// </summary>
/// <param name="TvdbId">TVDB id, when the provider is TVDB.</param>
/// <param name="TmdbId">TMDB id, when the provider is TMDB.</param>
/// <param name="Provider">Where the metadata comes from.</param>
/// <param name="TitleFallback">Title used when the provider has none.</param>
/// <param name="RootFolderId">Default root folder for versions.</param>
/// <param name="SeriesType">Standard, daily or anime.</param>
/// <param name="Numbering">Episode numbering scheme.</param>
/// <param name="SeasonFolder">Whether episodes live in season folders.</param>
/// <param name="Monitored">Whether the series is monitored.</param>
/// <param name="MonitorOption">Which episodes are monitored.</param>
/// <param name="MonitorSpecials">Whether specials take part in monitoring.</param>
/// <param name="MonitorNewItems">How new seasons are monitored.</param>
/// <param name="TagIds">Tags to apply.</param>
/// <param name="Versions">Versions to create, at least one.</param>
/// <param name="SearchOnAdd">Queue a search after saving.</param>
public sealed record AddSeriesOptions(
	int? TvdbId,
	int? TmdbId,
	MetadataProvider Provider,
	string? TitleFallback,
	int RootFolderId,
	SeriesType SeriesType,
	SeriesNumbering Numbering,
	bool SeasonFolder,
	bool Monitored,
	AddMonitorOption MonitorOption,
	bool MonitorSpecials,
	MonitorNewItems MonitorNewItems,
	IReadOnlyList<int> TagIds,
	IReadOnlyList<VersionOptions> Versions,
	bool SearchOnAdd);

/// <summary>
///     One version of a series or movie to create.
/// </summary>
/// <param name="Name">Display name, for example 1080p.</param>
/// <param name="QualityProfileId">Quality profile.</param>
/// <param name="LanguageProfileId">Language profile.</param>
/// <param name="RootFolderId">Root folder, null uses the series or movie root folder.</param>
public sealed record VersionOptions(string Name, int QualityProfileId, int LanguageProfileId, int? RootFolderId);

/// <summary>
///     Options for adding a movie.
/// </summary>
/// <param name="TmdbId">TMDB id.</param>
/// <param name="TitleFallback">Title used when the provider has none.</param>
/// <param name="RootFolderId">Default root folder for versions.</param>
/// <param name="IsAnime">Whether the movie is anime.</param>
/// <param name="Monitored">Whether the movie is monitored.</param>
/// <param name="MinimumAvailability">Earliest availability before grabbing.</param>
/// <param name="TagIds">Tags to apply.</param>
/// <param name="Versions">Versions to create, at least one.</param>
/// <param name="SearchOnAdd">Queue a search after saving.</param>
public sealed record AddMovieOptions(
	int TmdbId,
	string? TitleFallback,
	int RootFolderId,
	bool IsAnime,
	bool Monitored,
	MinimumAvailability MinimumAvailability,
	IReadOnlyList<int> TagIds,
	IReadOnlyList<VersionOptions> Versions,
	bool SearchOnAdd);

/// <summary>
///     Adds series and movies from metadata, creating seasons, episodes, versions and folders.
/// </summary>
public sealed class LibraryAdder(
	SubmarineDbContext db,
	IMetadataClient metadata,
	ICommandQueue commandQueue,
	IEventBus eventBus,
	IFolderNameRenderer folderNameRenderer,
	CollectionSyncService collectionSync,
	TimeProvider timeProvider)
{
	/// <summary>
	///     Fetch metadata and add the series with seasons, episodes, alternative titles, versions and monitoring.
	/// </summary>
	/// <exception cref="InvalidOperationException">A series with the TVDB id already exists.</exception>
	/// <exception cref="KeyNotFoundException">Root folder, profile, tag or metadata not found.</exception>
	/// <exception cref="FluentValidation.ValidationException">Invalid combination of options.</exception>
	public async Task<Series> AddSeriesAsync(AddSeriesOptions options, CancellationToken cancellationToken = default)
	{
		if (options.Versions.Count == 0)
		{
			throw new FluentValidation.ValidationException("At least one version is required");
		}

		var defaultRoot = await RequireRootFolderAsync(options.RootFolderId, MediaKind.SERIES, cancellationToken);

		var metadataResource = options.Provider switch
		{
			MetadataProvider.TVDB => options.TvdbId is null
				? throw new FluentValidation.ValidationException("TVDB provider requires a tvdbId")
				: await metadata.GetSeriesByTvdbAsync(options.TvdbId.Value, cancellationToken),
			_ => options.TmdbId is null
				? throw new FluentValidation.ValidationException("TMDB provider requires a tmdbId")
				: await metadata.GetSeriesByTmdbAsync(options.TmdbId.Value, cancellationToken)
		} ?? throw new KeyNotFoundException("Series not found on the metadata provider");

		if (await db.Series.AnyAsync(x => x.TvdbId == metadataResource.TvdbId, cancellationToken))
		{
			throw new ConflictException($"Series with TVDB id {metadataResource.TvdbId} is already in the library");
		}

		var naming = await db.NamingConfig.SingleAsync(cancellationToken);
		var mediaManagement = await db.MediaManagementConfig.SingleAsync(cancellationToken);
		var tags = await LoadTagsAsync(options.TagIds, cancellationToken);
		var now = timeProvider.GetUtcNow().UtcDateTime;

		var series = new Series
		{
			TvdbId = metadataResource.TvdbId,
			TmdbId = metadataResource.TmdbId,
			ImdbId = metadataResource.ImdbId,
			Title = metadataResource.Title ?? options.TitleFallback ?? string.Empty,
			SortTitle = TitleNormalizer.SortTitle(metadataResource.Title ?? options.TitleFallback ?? string.Empty),
			CleanTitle = TitleNormalizer.CleanTitle(metadataResource.Title ?? options.TitleFallback ?? string.Empty),
			Overview = metadataResource.Overview,
			Network = metadataResource.Network,
			Runtime = metadataResource.Runtime,
			Year = metadataResource.Year ?? metadataResource.FirstAired?.Year,
			PosterUrl = metadataResource.PosterUrl,
			BackdropUrl = metadataResource.BackdropUrl,
			Status = MapStatus(metadataResource.Status),
			Type = options.SeriesType,
			MetadataProvider = options.Provider,
			Numbering = options.Numbering,
			Monitored = options.Monitored,
			MonitorNewItems = options.MonitorNewItems,
			SeasonFolder = options.SeasonFolder,
			Genres = [.. metadataResource.Genres],
			Certification = metadataResource.Certification,
			FirstAired = metadataResource.FirstAired.HasValue
				? DateTime.SpecifyKind(metadataResource.FirstAired.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
				: null,
			LastRefreshedAt = now,
			Tags = tags
		};

		AddSeasonsAndEpisodes(series, metadataResource);
		var alternativeTitles = CollectAlternativeTitles(series, metadataResource);
		var roots = new Dictionary<int, string> { [defaultRoot.Id] = defaultRoot.Path };

		foreach (var version in options.Versions)
		{
			var root = version.RootFolderId is null
				? defaultRoot
				: await RequireRootFolderAsync(version.RootFolderId.Value, MediaKind.SERIES, cancellationToken);
			roots[root.Id] = root.Path;
			await RequireProfilesAsync(version.QualityProfileId, version.LanguageProfileId, cancellationToken);
			series.Versions.Add(new MediaVersion
			{
				Name = version.Name,
				SeriesId = series.Id,
				QualityProfileId = version.QualityProfileId,
				LanguageProfileId = version.LanguageProfileId,
				RootFolderId = root.Id,
				Path = folderNameRenderer.RenderSeriesFolder(naming, series)
			});
		}

		MonitorRules.Apply(series, options.MonitorOption, options.MonitorSpecials, now);
		db.Series.Add(series);
		await db.SaveChangesAsync(cancellationToken);

		foreach (var title in alternativeTitles)
		{
			title.SeriesId = series.Id;
			db.AlternativeTitles.Add(title);
		}

		await db.SaveChangesAsync(cancellationToken);

		CreateFolders(mediaManagement.CreateEmptySeriesFolders, series.Versions, roots);
		if (options.SearchOnAdd)
		{
			await commandQueue.EnqueueAsync(new SeriesSearchCommand(series.Id), CommandTrigger.SYSTEM, CommandPriority.HIGH, cancellationToken);
		}

		await eventBus.PublishAsync(new SeriesAddedEvent(series.Id), cancellationToken);
		return series;
	}

	/// <summary>
	///     Fetch metadata and add the movie with versions and monitoring.
	/// </summary>
	/// <exception cref="InvalidOperationException">A movie with the TMDB id already exists.</exception>
	/// <exception cref="KeyNotFoundException">Root folder, profile, tag or metadata not found.</exception>
	public async Task<Movie> AddMovieAsync(AddMovieOptions options, CancellationToken cancellationToken = default)
	{
		if (options.Versions.Count == 0)
		{
			throw new FluentValidation.ValidationException("At least one version is required");
		}

		var defaultRoot = await RequireRootFolderAsync(options.RootFolderId, MediaKind.MOVIES, cancellationToken);
		var resource = await metadata.GetMovieAsync(options.TmdbId, cancellationToken)
			?? throw new KeyNotFoundException($"Movie {options.TmdbId} not found on the metadata provider");

		if (await db.Movies.AnyAsync(x => x.TmdbId == resource.TmdbId, cancellationToken))
		{
			throw new ConflictException($"Movie with TMDB id {resource.TmdbId} is already in the library");
		}

		var naming = await db.NamingConfig.SingleAsync(cancellationToken);
		var mediaManagement = await db.MediaManagementConfig.SingleAsync(cancellationToken);
		var tags = await LoadTagsAsync(options.TagIds, cancellationToken);
		var roots = new Dictionary<int, string> { [defaultRoot.Id] = defaultRoot.Path };

		var title = resource.Title ?? options.TitleFallback ?? string.Empty;
		var movie = new Movie
		{
			TmdbId = resource.TmdbId,
			ImdbId = resource.ImdbId,
			Title = title,
			SortTitle = TitleNormalizer.SortTitle(title),
			CleanTitle = TitleNormalizer.CleanTitle(title),
			OriginalTitle = resource.OriginalTitle,
			Overview = resource.Overview,
			Year = resource.Year,
			Runtime = resource.Runtime,
			Studio = resource.Studio,
			PosterUrl = resource.PosterUrl,
			BackdropUrl = resource.BackdropUrl,
			Status = MapStatus(resource.Status),
			InCinemasDate = ToUtc(resource.InCinemasDate),
			DigitalReleaseDate = ToUtc(resource.DigitalReleaseDate),
			PhysicalReleaseDate = ToUtc(resource.PhysicalReleaseDate),
			IsAnime = options.IsAnime,
			Monitored = options.Monitored,
			MinimumAvailability = options.MinimumAvailability,
			TmdbCollectionId = resource.TmdbCollectionId,
			CollectionTitle = resource.CollectionTitle,
			Genres = [.. resource.Genres],
			Certification = resource.Certification,
			YouTubeTrailerId = resource.YouTubeTrailerId,
			LastRefreshedAt = timeProvider.GetUtcNow().UtcDateTime,
			Tags = tags
		};

		foreach (var version in options.Versions)
		{
			var root = version.RootFolderId is null
				? defaultRoot
				: await RequireRootFolderAsync(version.RootFolderId.Value, MediaKind.MOVIES, cancellationToken);
			roots[root.Id] = root.Path;
			await RequireProfilesAsync(version.QualityProfileId, version.LanguageProfileId, cancellationToken);
			movie.Versions.Add(new MediaVersion
			{
				Name = version.Name,
				MovieId = movie.Id,
				QualityProfileId = version.QualityProfileId,
				LanguageProfileId = version.LanguageProfileId,
				RootFolderId = root.Id,
				Path = folderNameRenderer.RenderMovieFolder(naming, movie)
			});
		}

		db.Movies.Add(movie);
		await db.SaveChangesAsync(cancellationToken);

		CreateFolders(mediaManagement.CreateEmptyMovieFolders, movie.Versions, roots);
		if (options.SearchOnAdd)
		{
			await commandQueue.EnqueueAsync(
				new MovieSearchCommand([movie.Id]),
				CommandTrigger.SYSTEM,
				CommandPriority.HIGH,
				cancellationToken);
		}

		await collectionSync.SyncAsync(movie, cancellationToken);
		await eventBus.PublishAsync(new MovieAddedEvent(movie.Id), cancellationToken);
		return movie;
	}

	private void AddSeasonsAndEpisodes(Series series, SeriesResource resource)
	{
		foreach (var seasonResource in resource.Seasons)
		{
			series.Seasons.Add(new Season { SeriesId = series.Id, SeasonNumber = seasonResource.SeasonNumber });
		}

		foreach (var episodeResource in resource.Episodes)
		{
			var numbers = PickNumbers(episodeResource, series.Numbering);
			if (numbers is null)
			{
				continue;
			}

			var (seasonNumber, episodeNumber, absolute) = numbers.Value;
			if (series.Seasons.All(x => x.SeasonNumber != seasonNumber))
			{
				series.Seasons.Add(new Season { SeriesId = series.Id, SeasonNumber = seasonNumber });
			}

			series.Episodes.Add(new Episode
			{
				SeriesId = series.Id,
				SeasonNumber = seasonNumber,
				EpisodeNumber = episodeNumber,
				AbsoluteEpisodeNumber = absolute,
				TvdbId = episodeResource.TvdbId,
				TmdbId = episodeResource.TmdbId,
				Title = episodeResource.Title,
				Overview = episodeResource.Overview,
				AirDate = episodeResource.AirDate?.ToString("yyyy-MM-dd"),
				AirDateUtc = episodeResource.AirDateUtc.HasValue
					? DateTime.SpecifyKind(episodeResource.AirDateUtc.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
					: null,
				Runtime = episodeResource.Runtime
			});
		}
	}

	private static (int Season, int Episode, int? Absolute)? PickNumbers(EpisodeResource resource, SeriesNumbering numbering)
	{
		var preferred = numbering switch
		{
			SeriesNumbering.DVD => EpisodeOrdering.DVD,
			SeriesNumbering.ABSOLUTE => EpisodeOrdering.ABSOLUTE,
			_ => EpisodeOrdering.AIRED
		};

		var chosen = resource.Numbers.FirstOrDefault(x => x.Ordering == preferred)
			?? resource.Numbers.FirstOrDefault(x => x.Ordering == EpisodeOrdering.AIRED)
			?? resource.Numbers.FirstOrDefault();
		if (chosen is null || chosen.SeasonNumber is null || chosen.Number is null)
		{
			return null;
		}

		return (chosen.SeasonNumber.Value, chosen.Number.Value, chosen.AbsoluteNumber);
	}

	private static List<AlternativeTitle> CollectAlternativeTitles(Series series, SeriesResource resource)
	{
		var titles = new List<AlternativeTitle>();
		foreach (var title in resource.AlternateTitles)
		{
			if (!string.IsNullOrWhiteSpace(title.Title) && title.Title != series.Title)
			{
				titles.Add(new AlternativeTitle { SeriesId = series.Id, Title = title.Title });
			}
		}

		return titles;
	}

	private async Task<RootFolder> RequireRootFolderAsync(int rootFolderId, MediaKind kind, CancellationToken cancellationToken)
	{
		var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == rootFolderId, cancellationToken)
			?? throw new KeyNotFoundException($"Root folder {rootFolderId} not found");
		if (root.MediaKind != kind)
		{
			throw new FluentValidation.ValidationException($"Root folder '{root.Path}' does not hold {kind.ToString().ToLowerInvariant()}");
		}

		return root;
	}

	private async Task RequireProfilesAsync(int qualityProfileId, int languageProfileId, CancellationToken cancellationToken)
	{
		_ = await db.QualityProfiles.FirstOrDefaultAsync(x => x.Id == qualityProfileId, cancellationToken)
			?? throw new KeyNotFoundException($"Quality profile {qualityProfileId} not found");
		_ = await db.LanguageProfiles.FirstOrDefaultAsync(x => x.Id == languageProfileId, cancellationToken)
			?? throw new KeyNotFoundException($"Language profile {languageProfileId} not found");
	}

	private async Task<List<Tag>> LoadTagsAsync(IReadOnlyList<int> tagIds, CancellationToken cancellationToken)
	{
		if (tagIds.Count == 0)
		{
			return [];
		}

		var found = await db.Tags.Where(x => tagIds.Contains(x.Id)).ToListAsync(cancellationToken);
		if (found.Count != tagIds.Distinct().Count())
		{
			throw new KeyNotFoundException("One or more tags not found");
		}

		return found;
	}

	private void CreateFolders(bool enabled, IEnumerable<MediaVersion> versions, Dictionary<int, string> roots)
	{
		if (!enabled)
		{
			return;
		}

		foreach (var version in versions)
		{
			if (!roots.TryGetValue(version.RootFolderId, out var rootPath))
			{
				continue;
			}

			try
			{
				Directory.CreateDirectory(MediaVersionPathGuard.ResolveUnderRoot(rootPath, version.Path));
			}
			catch (IOException)
			{
				// Best effort: the library scan creates missing folders later.
			}
			catch (UnauthorizedAccessException)
			{
				// Best effort: the library scan creates missing folders later.
			}
			catch (InvalidOperationException)
			{
				// A rendered folder name must never escape its root folder.
			}
		}
	}

	private static Submarine.Core.Enums.SeriesStatus MapStatus(Contracts.Metadata.SeriesStatus status)
		=> status switch
		{
			Contracts.Metadata.SeriesStatus.CONTINUING => Submarine.Core.Enums.SeriesStatus.CONTINUING,
			Contracts.Metadata.SeriesStatus.ENDED => Submarine.Core.Enums.SeriesStatus.ENDED,
			Contracts.Metadata.SeriesStatus.UPCOMING => Submarine.Core.Enums.SeriesStatus.UPCOMING,
			_ => Submarine.Core.Enums.SeriesStatus.UNKNOWN
		};

	private static Submarine.Core.Enums.MovieStatus MapStatus(Contracts.Metadata.MovieStatus status)
		=> status switch
		{
			Contracts.Metadata.MovieStatus.ANNOUNCED => Submarine.Core.Enums.MovieStatus.ANNOUNCED,
			Contracts.Metadata.MovieStatus.IN_CINEMAS => Submarine.Core.Enums.MovieStatus.IN_CINEMAS,
			_ => Submarine.Core.Enums.MovieStatus.RELEASED
		};

	private static DateTime? ToUtc(DateOnly? date)
		=> date.HasValue ? DateTime.SpecifyKind(date.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) : null;
}
