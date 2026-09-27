namespace Submarine.Api.Features.Compat.Radarr;

public sealed class RadarrMovieResource
{
	public int Id { get; init; }
	public int TmdbId { get; init; }
	public string? ImdbId { get; init; }
	public string? Title { get; init; }
	public string? OriginalTitle { get; init; }
	public string? SortTitle { get; init; }
	public string? TitleSlug { get; init; }
	public int? Year { get; init; }
	public string? Overview { get; init; }
	public string? Status { get; init; }
	public string? MinimumAvailability { get; init; }
	public bool? Monitored { get; init; }
	public DateTime? InCinemas { get; init; }
	public DateTime? DigitalRelease { get; init; }
	public DateTime? PhysicalRelease { get; init; }
	public DateTime? Added { get; init; }
	public string? RootFolderPath { get; init; }
	public string? Path { get; init; }
	public int? QualityProfileId { get; init; }
	public int? ProfileId { get; init; }
	public int? LanguageProfileId { get; init; }
	public IReadOnlyList<int>? Tags { get; init; }
	public IReadOnlyList<RadarrImageResource>? Images { get; init; }
	public IReadOnlyList<string>? Genres { get; init; }
	public int? Runtime { get; init; }
	public string? Studio { get; init; }
	public string? Certification { get; init; }
	public bool HasFile { get; init; }
	public bool IsAvailable { get; init; }
	public long SizeOnDisk { get; init; }
	public RadarrMovieFileResource? MovieFile { get; init; }
	public bool? SearchOnAdd { get; init; }
	public RadarrAddOptions? AddOptions { get; init; }
	public RadarrStatistics? Statistics { get; init; }

	public static RadarrMovieResource FromLookup(Submarine.Contracts.Metadata.SearchResultResource hit)
		=> new() { TmdbId = hit.TmdbId ?? 0, ImdbId = hit.ImdbId, Title = hit.Title, Year = hit.Year, Overview = hit.Overview,
			Images = hit.PosterUrl is null ? [] : [new RadarrImageResource("poster", hit.PosterUrl)] };
}

public sealed record RadarrAddOptions(bool? SearchForMovie);
public sealed record RadarrImageResource(string CoverType, string? Url);
public sealed record RadarrStatistics(int MovieFileCount, long SizeOnDisk);
public sealed record RadarrMovieFileResource(int Id, int MovieId, string RelativePath, string Path, long Size, DateTime DateAdded, object Quality,
	IReadOnlyList<string> Languages, string? ReleaseGroup, string? SceneName, string? Edition, object? MediaInfo);
public sealed class RadarrReleaseGrabRequest
{
	public string Guid { get; init; } = string.Empty;
	public int IndexerId { get; init; }
	public int? MovieId { get; init; }
	public string? QualitySource { get; init; }
	public string? QualityResolution { get; init; }
	public IReadOnlyList<string>? Languages { get; init; }
	public bool Override { get; init; }
}
