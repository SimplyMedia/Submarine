using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Submarine.Contracts.Metadata;
using Submarine.Core.Commands;
using Submarine.Core.Enums;
using Submarine.Core.Library;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Library;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Import;

/// <summary>
///     One unmapped folder found while scanning a root folder for library import, with metadata proposals
///     guessed from the folder name.
/// </summary>
/// <param name="Folder">Folder name relative to the root folder.</param>
/// <param name="GuessedTitle">Title guessed from the folder name.</param>
/// <param name="GuessedYear">Year guessed from the folder name, if any.</param>
/// <param name="Proposals">Metadata search results for the guessed title.</param>
public sealed record LibraryImportFolder(string Folder, string GuessedTitle, int? GuessedYear, IReadOnlyList<SearchResultResource> Proposals);

/// <summary>
///     One folder to adopt into the library.
/// </summary>
/// <param name="RootFolderId">Root folder the folder lives under.</param>
/// <param name="Folder">Folder name relative to the root folder.</param>
/// <param name="TvdbId">TVDB id, for a series.</param>
/// <param name="TmdbId">TMDB id, for a movie.</param>
/// <param name="QualityProfileId">Quality profile for the new version.</param>
/// <param name="LanguageProfileId">Language profile for the new version.</param>
/// <param name="SeriesType">Series type, defaults to standard.</param>
/// <param name="Monitor">Whether to monitor the added media, defaults to true.</param>
public sealed record LibraryImportItem(
	int RootFolderId,
	string Folder,
	int? TvdbId,
	int? TmdbId,
	int QualityProfileId,
	int LanguageProfileId,
	SeriesType? SeriesType,
	bool? Monitor);

/// <summary>
///     Adopts existing library folders: scans a root folder for subfolders not yet mapped to a version,
///     proposes metadata matches, and adds the series or movie with the folder as its version path.
/// </summary>
public interface ILibraryImportService
{
	/// <summary>Scans a root folder for subfolders not yet used by a version, with metadata proposals.</summary>
	Task<IReadOnlyList<LibraryImportFolder>> ScanAsync(int rootFolderId, CancellationToken cancellationToken = default);

	/// <summary>Adds the given folders to the library and queues a rescan to import their files in place.</summary>
	Task AddAsync(IReadOnlyList<LibraryImportItem> items, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ILibraryImportService" />
public sealed partial class LibraryImportService(
	SubmarineDbContext db,
	IMetadataClient metadata,
	LibraryAdder libraryAdder,
	ICommandQueue commandQueue) : ILibraryImportService
{
	/// <inheritdoc />
	public async Task<IReadOnlyList<LibraryImportFolder>> ScanAsync(int rootFolderId, CancellationToken cancellationToken = default)
	{
		var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == rootFolderId, cancellationToken)
			?? throw new KeyNotFoundException($"Root folder {rootFolderId} not found");

		if (!Directory.Exists(root.Path))
		{
			return [];
		}

		var used = await db.MediaVersions
			.Where(x => x.RootFolderId == root.Id)
			.Select(x => x.Path)
			.ToListAsync(cancellationToken);
		var usedSet = used.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var result = new List<LibraryImportFolder>();
		foreach (var directory in Directory.EnumerateDirectories(root.Path))
		{
			var folderName = Path.GetFileName(directory);
			if (usedSet.Contains(folderName))
			{
				continue;
			}

			var (title, year) = GuessTitleAndYear(folderName);
			IReadOnlyList<SearchResultResource> proposals;
			try
			{
				proposals = root.MediaKind == MediaKind.SERIES
					? await metadata.SearchSeriesAsync(title, MetadataProvider.TVDB, cancellationToken)
					: await metadata.SearchMoviesAsync(title, year, cancellationToken);
			}
			catch (HttpRequestException)
			{
				proposals = [];
			}

			result.Add(new LibraryImportFolder(folderName, title, year, proposals));
		}

		return result;
	}

	/// <inheritdoc />
	public async Task AddAsync(IReadOnlyList<LibraryImportItem> items, CancellationToken cancellationToken = default)
	{
		foreach (var item in items)
		{
			var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == item.RootFolderId, cancellationToken)
				?? throw new KeyNotFoundException($"Root folder {item.RootFolderId} not found");

			if (root.MediaKind == MediaKind.SERIES)
			{
				if (item.TvdbId is not { } tvdbId)
				{
					throw new FluentValidation.ValidationException("tvdbId is required to import a series folder");
				}

				var series = await libraryAdder.AddSeriesAsync(
					new AddSeriesOptions(
						tvdbId,
						null,
						MetadataProvider.TVDB,
						item.Folder,
						root.Id,
						item.SeriesType ?? SeriesType.STANDARD,
						SeriesNumbering.AIRED,
						SeasonFolder: true,
						item.Monitor ?? true,
						AddMonitorOption.ALL,
						MonitorSpecials: true,
						MonitorNewItems.ALL,
						TagIds: [],
						Versions: [new VersionOptions("Import", item.QualityProfileId, item.LanguageProfileId, root.Id)],
						SearchOnAdd: false),
					cancellationToken);

				var version = series.Versions.First();
				version.Path = item.Folder;
				await db.SaveChangesAsync(cancellationToken);
				await commandQueue.EnqueueAsync(new RescanSeriesCommand(series.Id), CommandTrigger.SYSTEM, cancellationToken: cancellationToken);
			}
			else
			{
				if (item.TmdbId is not { } tmdbId)
				{
					throw new FluentValidation.ValidationException("tmdbId is required to import a movie folder");
				}

				var movie = await libraryAdder.AddMovieAsync(
					new AddMovieOptions(
						tmdbId,
						item.Folder,
						root.Id,
						IsAnime: false,
						item.Monitor ?? true,
						MinimumAvailability.RELEASED,
						TagIds: [],
						Versions: [new VersionOptions("Import", item.QualityProfileId, item.LanguageProfileId, root.Id)],
						SearchOnAdd: false),
					cancellationToken);

				var version = movie.Versions.First();
				version.Path = item.Folder;
				await db.SaveChangesAsync(cancellationToken);
				await commandQueue.EnqueueAsync(new RescanMovieCommand(movie.Id), CommandTrigger.SYSTEM, cancellationToken: cancellationToken);
			}
		}
	}

	private static (string Title, int? Year) GuessTitleAndYear(string folderName)
	{
		var normalized = folderName.Replace('.', ' ').Replace('_', ' ');
		var match = YearRegex().Match(normalized);
		if (!match.Success || !int.TryParse(match.Groups["year"].Value, out var year))
		{
			return (normalized.Trim(), null);
		}

		var title = normalized[..match.Index].Trim(' ', '-', '(', '[');
		return (title.Length > 0 ? title : normalized.Trim(), year);
	}

	[GeneratedRegex(@"\(?(?<year>(19|20)\d{2})\)?")]
	private static partial Regex YearRegex();
}
