using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Contracts.Metadata;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Import;

namespace Submarine.Api.Features.LibraryImport;

/// <summary>
///     Adopts existing library folders into Submarine: scans a root folder for unmapped subfolders and,
///     once matched, adds them with the folder as the version's path.
/// </summary>
public sealed class LibraryImportModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/library-import");
		group.MapPost("/scan", ScanAsync);
		group.MapPost("/", AddAsync);
	}

	private static async Task<Ok<IReadOnlyList<LibraryImportFolderDto>>> ScanAsync(
		ILibraryImportService service,
		IValidator<LibraryImportScanRequest> validator,
		[FromBody] LibraryImportScanRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var folders = await service.ScanAsync(request.RootFolderId, cancellationToken);
		return TypedResults.Ok<IReadOnlyList<LibraryImportFolderDto>>(
			[.. folders.Select(x => new LibraryImportFolderDto(x.Folder, x.GuessedTitle, x.GuessedYear, x.Proposals))]);
	}

	private static async Task<Ok<LibraryImportAddResult>> AddAsync(
		ILibraryImportService service,
		IValidator<LibraryImportAddRequest> validator,
		[FromBody] LibraryImportAddRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var items = request.Items
			.Select(x => new LibraryImportItem(x.RootFolderId, x.Folder, x.TvdbId, x.TmdbId, x.QualityProfileId, x.LanguageProfileId, x.SeriesType, x.Monitor))
			.ToList();

		await service.AddAsync(items, cancellationToken);
		return TypedResults.Ok(new LibraryImportAddResult(items.Count));
	}
}

/// <summary>Scan request.</summary>
/// <param name="RootFolderId">Root folder to scan.</param>
public sealed record LibraryImportScanRequest(int RootFolderId);

/// <summary>An unmapped folder found by the scan.</summary>
/// <param name="Folder">Folder name relative to the root folder.</param>
/// <param name="GuessedTitle">Title guessed from the folder name.</param>
/// <param name="GuessedYear">Year guessed from the folder name, if any.</param>
/// <param name="Proposals">Metadata search results for the guessed title.</param>
public sealed record LibraryImportFolderDto(string Folder, string GuessedTitle, int? GuessedYear, IReadOnlyList<SearchResultResource> Proposals);

/// <summary>One folder to adopt.</summary>
/// <param name="RootFolderId">Root folder the folder lives under.</param>
/// <param name="Folder">Folder name relative to the root folder.</param>
/// <param name="TvdbId">TVDB id, for a series.</param>
/// <param name="TmdbId">TMDB id, for a movie.</param>
/// <param name="QualityProfileId">Quality profile for the new version.</param>
/// <param name="LanguageProfileId">Language profile for the new version.</param>
/// <param name="SeriesType">Series type, defaults to standard.</param>
/// <param name="Monitor">Whether to monitor the added media, defaults to true.</param>
public sealed record LibraryImportAddItem(
	int RootFolderId,
	string Folder,
	int? TvdbId,
	int? TmdbId,
	int QualityProfileId,
	int LanguageProfileId,
	SeriesType? SeriesType,
	bool? Monitor);

/// <summary>Add request.</summary>
/// <param name="Items">Folders to adopt.</param>
public sealed record LibraryImportAddRequest(List<LibraryImportAddItem> Items);

/// <summary>Result of an add.</summary>
/// <param name="Added">Number of items added.</param>
public sealed record LibraryImportAddResult(int Added);

/// <summary>Validator for <see cref="LibraryImportScanRequest" />.</summary>
public sealed class LibraryImportScanRequestValidator : AbstractValidator<LibraryImportScanRequest>
{
	/// <inheritdoc />
	public LibraryImportScanRequestValidator()
		=> RuleFor(x => x.RootFolderId).GreaterThan(0);
}

/// <summary>Validator for <see cref="LibraryImportAddRequest" />.</summary>
public sealed class LibraryImportAddRequestValidator : AbstractValidator<LibraryImportAddRequest>
{
	/// <inheritdoc />
	public LibraryImportAddRequestValidator()
	{
		RuleFor(x => x.Items).NotNull().NotEmpty();
		RuleForEach(x => x.Items).ChildRules(item =>
		{
			item.RuleFor(x => x.RootFolderId).GreaterThan(0);
			item.RuleFor(x => x.Folder).NotEmpty();
			item.RuleFor(x => x.QualityProfileId).GreaterThan(0);
			item.RuleFor(x => x.LanguageProfileId).GreaterThan(0);
			item.RuleFor(x => x)
				.Must(x => x.TvdbId is not null || x.TmdbId is not null)
				.WithMessage("Either tvdbId or tmdbId is required");
		});
	}
}
