namespace Submarine.Core.Metadata;

/// <summary>
///     Base type of all metadata consumer settings records.
/// </summary>
public abstract record MetadataConsumerSettings;

/// <summary>
///     Kodi/Emby compatible NFO settings, covering both series and movies.
/// </summary>
public sealed record KodiMetadataConsumerSettings : MetadataConsumerSettings
{
	/// <summary>Write tvshow.nfo in the series folder.</summary>
	public bool SeriesMetadata { get; init; } = true;

	/// <summary>Append the TheTVDB url to tvshow.nfo.</summary>
	public bool SeriesMetadataUrl { get; init; }

	/// <summary>Write "{episode file}.nfo" next to each episode file.</summary>
	public bool EpisodeMetadata { get; init; } = true;

	/// <summary>Write fanart.jpg, poster.jpg and banner.jpg in the series folder.</summary>
	public bool SeriesImages { get; init; } = true;

	/// <summary>Write season##-poster.jpg and season##-banner.jpg in the series folder.</summary>
	public bool SeasonImages { get; init; } = true;

	/// <summary>Write "{episode file}-thumb.jpg" next to each episode file.</summary>
	public bool EpisodeImages { get; init; } = true;

	/// <summary>Write "{movie file}.nfo" next to each movie file.</summary>
	public bool MovieMetadata { get; init; } = true;

	/// <summary>Append the TMDB and IMDB urls to the movie NFO.</summary>
	public bool MovieMetadataUrl { get; init; }

	/// <summary>Write fanart.jpg and poster.jpg next to each movie file.</summary>
	public bool MovieImages { get; init; } = true;
}

/// <summary>
///     Plex .plexmatch settings, series only.
/// </summary>
public sealed record PlexMetadataConsumerSettings : MetadataConsumerSettings
{
	/// <summary>Write a .plexmatch file in the series folder.</summary>
	public bool SeriesPlexMatchFile { get; init; } = true;

	/// <summary>Include an Episode mapping line per file in the .plexmatch file.</summary>
	public bool EpisodeMappings { get; init; }
}

/// <summary>
///     Emby legacy movie.xml settings, movies only.
/// </summary>
public sealed record EmbyMetadataConsumerSettings : MetadataConsumerSettings
{
	/// <summary>Write movie.xml in the movie folder.</summary>
	public bool MovieMetadata { get; init; } = true;
}

/// <summary>
///     Roksbox compatible settings, covering both series and movies.
/// </summary>
public sealed record RoksboxMetadataConsumerSettings : MetadataConsumerSettings
{
	/// <summary>Write "{episode file}.xml" next to each episode file.</summary>
	public bool EpisodeMetadata { get; init; } = true;

	/// <summary>Write "{series folder name}.jpg" in the series folder.</summary>
	public bool SeriesImages { get; init; } = true;

	/// <summary>Write "{season folder name}.jpg" in each season folder.</summary>
	public bool SeasonImages { get; init; } = true;

	/// <summary>Write "{episode file}.jpg" next to each episode file.</summary>
	public bool EpisodeImages { get; init; } = true;

	/// <summary>Write "{movie file}.xml" next to each movie file.</summary>
	public bool MovieMetadata { get; init; } = true;

	/// <summary>Write images next to each movie file.</summary>
	public bool MovieImages { get; init; } = true;
}

/// <summary>
///     WDTV compatible settings, covering both series and movies.
/// </summary>
public sealed record WdtvMetadataConsumerSettings : MetadataConsumerSettings
{
	/// <summary>Write "{episode file}.xml" next to each episode file.</summary>
	public bool EpisodeMetadata { get; init; } = true;

	/// <summary>Write folder.jpg in the series folder.</summary>
	public bool SeriesImages { get; init; } = true;

	/// <summary>Write folder.jpg in each season folder.</summary>
	public bool SeasonImages { get; init; } = true;

	/// <summary>Write "{episode file}.metathumb" next to each episode file.</summary>
	public bool EpisodeImages { get; init; } = true;

	/// <summary>Write "{movie file}.xml" next to each movie file.</summary>
	public bool MovieMetadata { get; init; } = true;

	/// <summary>Write images next to each movie file.</summary>
	public bool MovieImages { get; init; } = true;
}
