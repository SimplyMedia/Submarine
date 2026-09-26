using FluentValidation;
using Submarine.Core.Enums;
using Submarine.Core.Library;

namespace Submarine.Api.Features.Series;

/// <summary>Statistics of a season or a whole series.</summary>
/// <param name="EpisodeCount">Monitored episodes.</param>
/// <param name="EpisodeFileCount">Monitored episodes with a file.</param>
/// <param name="TotalEpisodeCount">All episodes.</param>
/// <param name="SizeOnDisk">File size total in bytes.</param>
/// <param name="PercentOfEpisodes">Percentage of monitored episodes with a file.</param>
public sealed record SeriesStatisticsDto(
	int EpisodeCount,
	int EpisodeFileCount,
	int TotalEpisodeCount,
	long SizeOnDisk,
	double PercentOfEpisodes);

/// <summary>A season of a series with its statistics.</summary>
/// <param name="SeasonNumber">Season number, 0 for specials.</param>
/// <param name="Monitored">Whether the season is monitored.</param>
/// <param name="Statistics">Season statistics.</param>
public sealed record SeasonDto(int SeasonNumber, bool Monitored, SeriesStatisticsDto Statistics);

/// <summary>A version of a series or movie.</summary>
/// <param name="Id">Version id.</param>
/// <param name="Name">Display name.</param>
/// <param name="SeriesId">Owning series id, when a series version.</param>
/// <param name="MovieId">Owning movie id, when a movie version.</param>
/// <param name="QualityProfileId">Quality profile.</param>
/// <param name="LanguageProfileId">Language profile.</param>
/// <param name="RootFolderId">Root folder id.</param>
/// <param name="RootFolderPath">Path of the root folder.</param>
/// <param name="Path">Folder name inside the root folder.</param>
/// <param name="Monitored">Whether the version is monitored.</param>
public sealed record VersionDto(
	int Id,
	string Name,
	int? SeriesId,
	int? MovieId,
	int QualityProfileId,
	int LanguageProfileId,
	int RootFolderId,
	string? RootFolderPath,
	string Path,
	bool Monitored);

/// <summary>A tag reference.</summary>
/// <param name="Id">Tag id.</param>
/// <param name="Label">Tag label.</param>
public sealed record TagDto(int Id, string Label);

/// <summary>Series list item.</summary>
public sealed record SeriesListItemDto(
	int Id,
	int TvdbId,
	int? TmdbId,
	string? ImdbId,
	string Title,
	string SortTitle,
	string? Overview,
	string? Network,
	int? Runtime,
	int? Year,
	string? PosterUrl,
	string? BackdropUrl,
	SeriesStatus Status,
	SeriesType SeriesType,
	MetadataProvider MetadataProvider,
	SeriesNumbering Numbering,
	bool Monitored,
	MonitorNewItems MonitorNewItems,
	bool SeasonFolder,
	List<string> Genres,
	string? Certification,
	DateTime? FirstAired,
	DateTime Added,
	DateTime? NextAiring,
	DateTime? PreviousAiring,
	List<int> TagIds,
	List<VersionDto> Versions,
	SeriesStatisticsDto Statistics);

/// <summary>Series detail, list item plus seasons and alternative titles.</summary>
public sealed record SeriesDetailDto(
	SeriesListItemDto Series,
	List<SeasonDto> Seasons,
	List<string> AlternateTitles);

/// <summary>Lookup hit from the metadata service.</summary>
/// <param name="TvdbId">TVDB id.</param>
/// <param name="TmdbId">TMDB id.</param>
/// <param name="ImdbId">IMDB id.</param>
/// <param name="Title">Title.</param>
/// <param name="Year">Year.</param>
/// <param name="Overview">Overview.</param>
/// <param name="PosterUrl">Poster url.</param>
/// <param name="Status">Status from the provider.</param>
/// <param name="Provider">Provider the hit came from.</param>
/// <param name="ExistingSeriesId">Id of the library series with this TVDB id, when present.</param>
public sealed record SeriesLookupDto(
	int? TvdbId,
	int? TmdbId,
	string? ImdbId,
	string Title,
	int? Year,
	string? Overview,
	string? PosterUrl,
	string? Status,
	string Provider,
	int? ExistingSeriesId);

/// <summary>Add series request.</summary>
/// <param name="TvdbId">TVDB id, when adding from TVDB.</param>
/// <param name="TmdbId">TMDB id, when adding from TMDB.</param>
/// <param name="MetadataProvider">Provider backing the series.</param>
/// <param name="Title">Fallback title.</param>
/// <param name="RootFolderId">Default root folder for versions.</param>
/// <param name="SeriesType">Standard, daily or anime.</param>
/// <param name="Numbering">Episode numbering scheme.</param>
/// <param name="SeasonFolder">Whether episodes live in season folders.</param>
/// <param name="Monitored">Whether the series is monitored.</param>
/// <param name="MonitorOption">Which episodes are monitored.</param>
/// <param name="MonitorSpecials">Whether specials take part.</param>
/// <param name="MonitorNewItems">How new seasons are monitored.</param>
/// <param name="TagIds">Tags to apply.</param>
/// <param name="Versions">Versions to create.</param>
/// <param name="SearchOnAdd">Queue a search after saving.</param>
public sealed record AddSeriesRequest(
	int? TvdbId,
	int? TmdbId,
	MetadataProvider? MetadataProvider,
	string? Title,
	int RootFolderId,
	SeriesType? SeriesType,
	SeriesNumbering? Numbering,
	bool? SeasonFolder,
	bool? Monitored,
	AddMonitorOption? MonitorOption,
	bool? MonitorSpecials,
	MonitorNewItems? MonitorNewItems,
	List<int>? TagIds,
	List<AddVersionRequest> Versions,
	bool? SearchOnAdd);

/// <summary>One version of an add request.</summary>
/// <param name="Name">Display name, defaults to main.</param>
/// <param name="QualityProfileId">Quality profile.</param>
/// <param name="LanguageProfileId">Language profile.</param>
/// <param name="RootFolderId">Root folder, null uses the series root folder.</param>
public sealed record AddVersionRequest(string? Name, int QualityProfileId, int LanguageProfileId, int? RootFolderId);

/// <summary>Update series request, null fields keep their value.</summary>
/// <param name="Monitored">New monitored flag.</param>
/// <param name="SeasonFolder">New season folder flag.</param>
/// <param name="SeriesType">New series type.</param>
/// <param name="Numbering">New numbering scheme.</param>
/// <param name="MonitorNewItems">New new item monitoring.</param>
/// <param name="TagIds">Replaces tags.</param>
/// <param name="RootFolderId">Moves all versions to this root folder.</param>
/// <param name="MoveFiles">Whether files move on disk.</param>
/// <param name="Versions">Per version edits.</param>
public sealed record UpdateSeriesRequest(
	bool? Monitored,
	bool? SeasonFolder,
	SeriesType? SeriesType,
	SeriesNumbering? Numbering,
	MonitorNewItems? MonitorNewItems,
	List<int>? TagIds,
	int? RootFolderId,
	bool? MoveFiles,
	List<UpdateVersionRequest>? Versions);

/// <summary>Per version edit.</summary>
/// <param name="Id">Version id.</param>
/// <param name="Name">New display name.</param>
/// <param name="QualityProfileId">New quality profile.</param>
/// <param name="LanguageProfileId">New language profile.</param>
/// <param name="Path">New folder name inside the root folder.</param>
public sealed record UpdateVersionRequest(int Id, string? Name, int? QualityProfileId, int? LanguageProfileId, string? Path);

/// <summary>Bulk series editor request.</summary>
/// <param name="Ids">Series ids.</param>
/// <param name="Monitored">New monitored flag.</param>
/// <param name="SeriesType">New series type.</param>
/// <param name="SeasonFolder">New season folder flag.</param>
/// <param name="MonitorNewItems">New new item monitoring.</param>
/// <param name="QualityProfileId">Applied to all versions.</param>
/// <param name="LanguageProfileId">Applied to all versions.</param>
/// <param name="RootFolderId">Moves all versions to this root folder.</param>
/// <param name="MoveFiles">Whether files move on disk.</param>
/// <param name="Tags">Tag change.</param>
public sealed record SeriesEditorRequest(
	List<int> Ids,
	bool? Monitored,
	SeriesType? SeriesType,
	bool? SeasonFolder,
	MonitorNewItems? MonitorNewItems,
	int? QualityProfileId,
	int? LanguageProfileId,
	int? RootFolderId,
	bool? MoveFiles,
	TagChangeRequest? Tags);

/// <summary>Tag change in bulk requests.</summary>
/// <param name="Mode">Add, remove or replace.</param>
/// <param name="TagIds">Tag ids.</param>
public sealed record TagChangeRequest(string Mode, List<int> TagIds);

/// <summary>Bulk delete request.</summary>
/// <param name="Ids">Ids to delete.</param>
/// <param name="DeleteFiles">Whether files are deleted on disk.</param>
/// <param name="AddImportListExclusion">Whether an import list exclusion is created.</param>
public sealed record BulkDeleteRequest(List<int> Ids, bool? DeleteFiles, bool? AddImportListExclusion);

/// <summary>Season pass request: a monitored map per season or a monitor option.</summary>
/// <param name="Seasons">Explicit monitored map.</param>
/// <param name="MonitoringOption">Monitor option applied to the whole series.</param>
/// <param name="MonitorSpecials">Whether specials take part when using an option.</param>
public sealed record SeasonPassRequest(List<SeasonMonitoredRequest>? Seasons, AddMonitorOption? MonitoringOption, bool? MonitorSpecials);

/// <summary>One season of a season pass.</summary>
/// <param name="SeasonNumber">Season number.</param>
/// <param name="Monitored">Whether the season is monitored.</param>
public sealed record SeasonMonitoredRequest(int SeasonNumber, bool Monitored);

/// <summary>Series folder check response.</summary>
/// <param name="Folder">Full folder path of the first version.</param>
/// <param name="Exists">Whether the folder exists on disk.</param>
public sealed record SeriesFolderDto(string? Folder, bool Exists);

/// <summary>Validator for <see cref="AddSeriesRequest" />.</summary>
public sealed class AddSeriesRequestValidator : AbstractValidator<AddSeriesRequest>
{
	/// <inheritdoc />
	public AddSeriesRequestValidator()
	{
		RuleFor(x => x.MetadataProvider).NotNull().IsInEnum();
		RuleFor(x => x.RootFolderId).GreaterThan(0);
		RuleFor(x => x.Versions).NotNull().NotEmpty().WithMessage("At least one version is required");
		RuleForEach(x => x.Versions).ChildRules(version =>
		{
			version.RuleFor(x => x.QualityProfileId).GreaterThan(0);
			version.RuleFor(x => x.LanguageProfileId).GreaterThan(0);
			version.RuleFor(x => x.RootFolderId).GreaterThan(0).When(x => x.RootFolderId.HasValue);
		});
		RuleForEach(x => x.TagIds).GreaterThan(0);
		RuleFor(x => x)
			.Must(x => x.MetadataProvider switch
			{
				MetadataProvider.TVDB => x.TvdbId is > 0,
				MetadataProvider.TMDB => x.TmdbId is > 0,
				_ => false
			})
			.WithMessage("A matching tvdbId or tmdbId is required for the metadata provider");
	}
}

/// <summary>Validator for <see cref="UpdateSeriesRequest" />.</summary>
public sealed class UpdateSeriesRequestValidator : AbstractValidator<UpdateSeriesRequest>
{
	/// <inheritdoc />
	public UpdateSeriesRequestValidator()
	{
		RuleFor(x => x.SeriesType).IsInEnum().When(x => x.SeriesType.HasValue);
		RuleFor(x => x.Numbering).IsInEnum().When(x => x.Numbering.HasValue);
		RuleFor(x => x.MonitorNewItems).IsInEnum().When(x => x.MonitorNewItems.HasValue);
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

/// <summary>Validator for <see cref="SeriesEditorRequest" />.</summary>
public sealed class SeriesEditorRequestValidator : AbstractValidator<SeriesEditorRequest>
{
	/// <inheritdoc />
	public SeriesEditorRequestValidator()
	{
		RuleFor(x => x.Ids).NotNull().NotEmpty();
		RuleForEach(x => x.Ids).GreaterThan(0);
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

/// <summary>Validator for <see cref="BulkDeleteRequest" />.</summary>
public sealed class BulkDeleteRequestValidator : AbstractValidator<BulkDeleteRequest>
{
	/// <inheritdoc />
	public BulkDeleteRequestValidator()
	{
		RuleFor(x => x.Ids).NotNull().NotEmpty();
		RuleForEach(x => x.Ids).GreaterThan(0);
	}
}

/// <summary>Validator for <see cref="SeasonPassRequest" />.</summary>
public sealed class SeasonPassRequestValidator : AbstractValidator<SeasonPassRequest>
{
	/// <inheritdoc />
	public SeasonPassRequestValidator()
	{
		RuleFor(x => x.MonitoringOption).IsInEnum().When(x => x.MonitoringOption.HasValue);
		RuleFor(x => x)
			.Must(x => x.Seasons is { Count: > 0 } || x.MonitoringOption.HasValue)
			.WithMessage("Either seasons or a monitoring option is required");
	}
}

/// <summary>Validator for <see cref="EpisodeMonitorRequest" />.</summary>
public sealed class EpisodeMonitorRequestValidator : AbstractValidator<EpisodeMonitorRequest>
{
	/// <inheritdoc />
	public EpisodeMonitorRequestValidator()
	{
		RuleFor(x => x.EpisodeIds).NotNull().NotEmpty();
		RuleForEach(x => x.EpisodeIds).GreaterThan(0);
	}
}

/// <summary>Episode monitor request.</summary>
/// <param name="EpisodeIds">Episode ids.</param>
/// <param name="Monitored">New monitored flag.</param>
public sealed record EpisodeMonitorRequest(List<int> EpisodeIds, bool Monitored);
