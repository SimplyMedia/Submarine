using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Languages;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Import;

namespace Submarine.Api.Features.ManualImport;

/// <summary>
///     Analyzes candidate files for manual import and imports them with the user's explicit choices.
/// </summary>
public sealed class ManualImportModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/manual-import");
		group.MapGet("/", AnalyzeAsync);
		group.MapPost("/", ImportAsync);
	}

	private static async Task<Ok<IReadOnlyList<ManualImportCandidateDto>>> AnalyzeAsync(
		string folder,
		int? seriesId,
		int? movieId,
		string? downloadId,
		bool? filterExisting,
		IImportService importService,
		CancellationToken cancellationToken)
	{
		var candidates = await importService.AnalyzeAsync(folder, seriesId, movieId, downloadId, cancellationToken);
		var dtos = candidates.Select(ToDto);
		if (filterExisting ?? false)
		{
			dtos = dtos.Where(x => x.Rejection is null);
		}

		return TypedResults.Ok<IReadOnlyList<ManualImportCandidateDto>>([.. dtos]);
	}

	private static async Task<Ok<ManualImportResultDto>> ImportAsync(
		IImportService importService,
		IValidator<ManualImportRequest> validator,
		[FromBody] ManualImportRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);

		var mode = request.ImportMode.Equals("copy", StringComparison.OrdinalIgnoreCase) ? ImportMode.COPY : ImportMode.MOVE;
		var selections = request.Items
			.Select(item => new ManualImportSelection(
				item.Path,
				item.SeriesId,
				item.EpisodeIds,
				item.MovieId,
				item.MediaVersionId,
				item.Quality,
				item.Languages,
				item.ReleaseGroup,
				item.DownloadId))
			.ToList();

		var summary = await importService.ImportManualAsync(selections, mode, cancellationToken);
		return TypedResults.Ok(new ManualImportResultDto(
			summary.AnyImported,
			[.. summary.Files.Select(x => new ManualImportFileResultDto(x.SourcePath, x.Imported, x.DestinationPath, x.IsUpgrade, x.Rejection?.ToString(), x.Message))]));
	}

	private static ManualImportCandidateDto ToDto(ManualImportCandidate candidate)
		=> new(
			candidate.Path,
			candidate.Size,
			candidate.SeriesId,
			candidate.EpisodeIds,
			candidate.MovieId,
			candidate.MediaVersionId,
			candidate.Quality,
			candidate.Languages,
			candidate.ReleaseGroup,
			candidate.Rejection?.ToString());
}

/// <summary>A candidate file found for manual import.</summary>
/// <param name="Path">Absolute path.</param>
/// <param name="Size">File size in bytes.</param>
/// <param name="SeriesId">Guessed series.</param>
/// <param name="EpisodeIds">Guessed episodes.</param>
/// <param name="MovieId">Guessed movie.</param>
/// <param name="MediaVersionId">Guessed version.</param>
/// <param name="Quality">Parsed quality.</param>
/// <param name="Languages">Parsed languages.</param>
/// <param name="ReleaseGroup">Parsed release group.</param>
/// <param name="Rejection">Why the automated pipeline would reject the file, if it would.</param>
public sealed record ManualImportCandidateDto(
	string Path,
	long Size,
	int? SeriesId,
	IReadOnlyList<int> EpisodeIds,
	int? MovieId,
	int? MediaVersionId,
	QualityModel Quality,
	IReadOnlyList<Language> Languages,
	string? ReleaseGroup,
	string? Rejection);

/// <summary>One user choice for a manual import file.</summary>
/// <param name="Path">Absolute path.</param>
/// <param name="SeriesId">Target series.</param>
/// <param name="EpisodeIds">Target episodes.</param>
/// <param name="MovieId">Target movie.</param>
/// <param name="MediaVersionId">Target version.</param>
/// <param name="Quality">Quality override.</param>
/// <param name="Languages">Language override.</param>
/// <param name="ReleaseGroup">Release group override.</param>
/// <param name="DownloadId">Originating tracked download id, if any.</param>
public sealed record ManualImportRequestItem(
	string Path,
	int? SeriesId,
	List<int>? EpisodeIds,
	int? MovieId,
	int MediaVersionId,
	QualityModel? Quality,
	List<Language>? Languages,
	string? ReleaseGroup,
	string? DownloadId);

/// <summary>Manual import request.</summary>
/// <param name="Items">Files to import.</param>
/// <param name="ImportMode">move or copy.</param>
public sealed record ManualImportRequest(List<ManualImportRequestItem> Items, string ImportMode);

/// <summary>Result of a manual import.</summary>
/// <param name="AnyImported">Whether at least one file was imported.</param>
/// <param name="Files">Per-file outcomes.</param>
public sealed record ManualImportResultDto(bool AnyImported, IReadOnlyList<ManualImportFileResultDto> Files);

/// <summary>Outcome of one manual import file.</summary>
/// <param name="Path">Absolute path.</param>
/// <param name="Imported">Whether the file was imported.</param>
/// <param name="DestinationPath">Final path, when imported.</param>
/// <param name="IsUpgrade">Whether an existing file was replaced.</param>
/// <param name="Rejection">Rejection reason, when not imported.</param>
/// <param name="Message">Outcome message.</param>
public sealed record ManualImportFileResultDto(string Path, bool Imported, string? DestinationPath, bool IsUpgrade, string? Rejection, string? Message);

/// <summary>Validator for <see cref="ManualImportRequest" />.</summary>
public sealed class ManualImportRequestValidator : AbstractValidator<ManualImportRequest>
{
	/// <inheritdoc />
	public ManualImportRequestValidator()
	{
		RuleFor(x => x.Items).NotNull().NotEmpty();
		RuleFor(x => x.ImportMode).Must(x => x is "move" or "copy").WithMessage("importMode must be move or copy");
		RuleForEach(x => x.Items).ChildRules(item =>
		{
			item.RuleFor(x => x.Path).NotEmpty();
			item.RuleFor(x => x.MediaVersionId).GreaterThan(0);
			item.RuleFor(x => x)
				.Must(x => x.SeriesId is not null || x.MovieId is not null)
				.WithMessage("Either seriesId or movieId is required");
		});
	}
}
