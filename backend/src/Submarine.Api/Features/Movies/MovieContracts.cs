using FluentValidation;
using Submarine.Core.Enums;
using Submarine.Api.Features.MediaFiles;
using Submarine.Api.Features.Series;

namespace Submarine.Api.Features.Movies;

/// <summary>Movie list item.</summary>
public sealed record MovieListItemDto(
	int Id,
	int TmdbId,
	string? ImdbId,
	string Title,
	string SortTitle,
	string? OriginalTitle,
	string? Overview,
	int? Year,
	int? Runtime,
	string? Studio,
	string? PosterUrl,
	string? BackdropUrl,
	MovieStatus Status,
	bool IsAnime,
	bool Monitored,
	MinimumAvailability MinimumAvailability,
	int? TmdbCollectionId,
	string? CollectionTitle,
	List<string> Genres,
	string? Certification,
	string? YouTubeTrailerId,
	DateTime? InCinemasDate,
	DateTime? DigitalReleaseDate,
	DateTime? PhysicalReleaseDate,
	DateTime Added,
	List<int> TagIds,
	List<VersionDto> Versions,
	bool HasFile,
	long SizeOnDisk,
	bool IsAvailable);

/// <summary>Movie detail, list item plus file summaries.</summary>
public sealed record MovieDetailDto(
	MovieListItemDto Movie,
	List<MovieFileDto> Files);

/// <summary>Summary of a movie file.</summary>
/// <param name="Id">File id.</param>
/// <param name="MediaVersionId">Owning version.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="Quality">Quality of the file.</param>
/// <param name="Languages">Languages of the file.</param>
/// <param name="ReleaseGroup">Release group.</param>
/// <param name="SceneName">Original scene name, if known.</param>
/// <param name="Edition">Edition of the file.</param>
/// <param name="MediaInfo">Technical media info.</param>
public sealed record MovieFileDto(
	int Id,
	int MediaVersionId,
	long Size,
	Submarine.Core.Quality.QualityModel Quality,
	List<Submarine.Core.Languages.Language> Languages,
	string? ReleaseGroup,
	string? SceneName,
	string? Edition,
	MediaInfoDto? MediaInfo);

/// <summary>Movie lookup hit.</summary>
/// <param name="TvdbId">TVDB id, when known.</param>
/// <param name="TmdbId">TMDB id.</param>
/// <param name="ImdbId">IMDB id.</param>
/// <param name="Title">Title.</param>
/// <param name="Year">Year.</param>
/// <param name="Overview">Overview.</param>
/// <param name="PosterUrl">Poster url.</param>
/// <param name="Status">Status from the provider.</param>
/// <param name="Provider">Provider the hit came from.</param>
/// <param name="ExistingMovieId">Id of the library movie with this TMDB id, when present.</param>
public sealed record MovieLookupDto(
	int? TvdbId,
	int? TmdbId,
	string? ImdbId,
	string Title,
	int? Year,
	string? Overview,
	string? PosterUrl,
	string? Status,
	string Provider,
	int? ExistingMovieId);

/// <summary>Add movie request.</summary>
/// <param name="TmdbId">TMDB id.</param>
/// <param name="Title">Fallback title.</param>
/// <param name="RootFolderId">Default root folder for versions.</param>
/// <param name="IsAnime">Whether the movie is anime.</param>
/// <param name="Monitored">Whether the movie is monitored.</param>
/// <param name="MinimumAvailability">Earliest availability before grabbing.</param>
/// <param name="TagIds">Tags to apply.</param>
/// <param name="Versions">Versions to create.</param>
/// <param name="SearchOnAdd">Queue a search after saving.</param>
public sealed record AddMovieRequest(
	int? TmdbId,
	string? Title,
	int RootFolderId,
	bool? IsAnime,
	bool? Monitored,
	MinimumAvailability? MinimumAvailability,
	List<int>? TagIds,
	List<AddVersionRequest>? Versions,
	bool? SearchOnAdd);

/// <summary>Update movie request, null fields keep their value.</summary>
/// <param name="Monitored">New monitored flag.</param>
/// <param name="MinimumAvailability">New minimum availability.</param>
/// <param name="IsAnime">New anime flag.</param>
/// <param name="TagIds">Replaces tags.</param>
/// <param name="RootFolderId">Moves all versions to this root folder.</param>
/// <param name="MoveFiles">Whether files move on disk.</param>
/// <param name="Versions">Per version edits.</param>
public sealed record UpdateMovieRequest(
	bool? Monitored,
	MinimumAvailability? MinimumAvailability,
	bool? IsAnime,
	List<int>? TagIds,
	int? RootFolderId,
	bool? MoveFiles,
	List<UpdateVersionRequest>? Versions);

/// <summary>Bulk movie editor request.</summary>
/// <param name="Ids">Movie ids.</param>
/// <param name="Monitored">New monitored flag.</param>
/// <param name="MinimumAvailability">New minimum availability.</param>
/// <param name="QualityProfileId">Applied to all versions.</param>
/// <param name="LanguageProfileId">Applied to all versions.</param>
/// <param name="RootFolderId">Moves all versions to this root folder.</param>
/// <param name="MoveFiles">Whether files move on disk.</param>
/// <param name="Tags">Tag change.</param>
public sealed record MovieEditorRequest(
	List<int> Ids,
	bool? Monitored,
	MinimumAvailability? MinimumAvailability,
	int? QualityProfileId,
	int? LanguageProfileId,
	int? RootFolderId,
	bool? MoveFiles,
	TagChangeRequest? Tags);

/// <summary>Validator for <see cref="AddMovieRequest" />.</summary>
public sealed class AddMovieRequestValidator : AbstractValidator<AddMovieRequest>
{
	/// <inheritdoc />
	public AddMovieRequestValidator()
	{
		RuleFor(x => x.TmdbId).NotNull().GreaterThan(0);
		RuleFor(x => x.RootFolderId).GreaterThan(0);
		RuleFor(x => x.MinimumAvailability).IsInEnum().When(x => x.MinimumAvailability.HasValue);
		RuleForEach(x => x.TagIds).GreaterThan(0);
		RuleFor(x => x.Versions).NotNull().NotEmpty().WithMessage("At least one version is required");
		RuleForEach(x => x.Versions).ChildRules(version =>
		{
			version.RuleFor(x => x.QualityProfileId).GreaterThan(0);
			version.RuleFor(x => x.LanguageProfileId).GreaterThan(0);
			version.RuleFor(x => x.RootFolderId).GreaterThan(0).When(x => x.RootFolderId.HasValue);
		});
	}
}

/// <summary>Validator for <see cref="UpdateMovieRequest" />.</summary>
public sealed class UpdateMovieRequestValidator : AbstractValidator<UpdateMovieRequest>
{
	/// <inheritdoc />
	public UpdateMovieRequestValidator()
	{
		RuleFor(x => x.MinimumAvailability).IsInEnum().When(x => x.MinimumAvailability.HasValue);
		RuleFor(x => x.RootFolderId).GreaterThan(0).When(x => x.RootFolderId.HasValue);
		RuleForEach(x => x.TagIds).GreaterThan(0);
		RuleForEach(x => x.Versions).ChildRules(version =>
		{
			version.RuleFor(x => x.Id).GreaterThan(0);
			version.RuleFor(x => x.QualityProfileId).GreaterThan(0).When(x => x.QualityProfileId.HasValue);
			version.RuleFor(x => x.LanguageProfileId).GreaterThan(0).When(x => x.LanguageProfileId.HasValue);
		});
	}
}

/// <summary>Validator for <see cref="MovieEditorRequest" />.</summary>
public sealed class MovieEditorRequestValidator : AbstractValidator<MovieEditorRequest>
{
	/// <inheritdoc />
	public MovieEditorRequestValidator()
	{
		RuleFor(x => x.Ids).NotNull().NotEmpty();
		RuleForEach(x => x.Ids).GreaterThan(0);
		RuleFor(x => x.MinimumAvailability).IsInEnum().When(x => x.MinimumAvailability.HasValue);
		RuleFor(x => x.RootFolderId).GreaterThan(0).When(x => x.RootFolderId.HasValue);
		RuleFor(x => x.QualityProfileId).GreaterThan(0).When(x => x.QualityProfileId.HasValue);
		RuleFor(x => x.LanguageProfileId).GreaterThan(0).When(x => x.LanguageProfileId.HasValue);
		RuleFor(x => x.Tags)
			.Must(tags => tags is null || tags.Mode is "add" or "remove" or "replace")
			.WithMessage("Mode must be add, remove or replace")
			.Must(tags => tags is null || tags.TagIds.Count > 0)
			.WithMessage("TagIds must not be empty")
			.When(x => x.Tags is not null);
	}
}
