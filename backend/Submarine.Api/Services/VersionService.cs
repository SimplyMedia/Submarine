using Microsoft.EntityFrameworkCore;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Database;
using Submarine.Api.Models.Request;
using Submarine.Core.Library;

namespace Submarine.Api.Services;

/// <summary>
///     Manages the versions of a Series or Movie, each kept in its own library folder
/// </summary>
public class VersionService
{
	private readonly SubmarineDatabaseContext _context;

	public VersionService(SubmarineDatabaseContext context)
		=> _context = context;

	public async Task<IReadOnlyList<MediaVersion>> GetForSeriesAsync(int seriesId)
	{
		if (!await _context.Series.AsNoTracking().AnyAsync(s => s.Id == seriesId))
			throw new NotFoundException();

		return await QueryVersions(seriesId, movieId: null).ToListAsync();
	}

	public async Task<IReadOnlyList<MediaVersion>> GetForMovieAsync(int movieId)
	{
		if (!await _context.Movies.AsNoTracking().AnyAsync(m => m.Id == movieId))
			throw new NotFoundException();

		return await QueryVersions(seriesId: null, movieId).ToListAsync();
	}

	public async Task<MediaVersion> CreateForSeriesAsync(int seriesId, AddMediaVersionRequest request)
	{
		var series = await _context.Series.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seriesId);

		if (series == null)
			throw new NotFoundException();

		await ValidateProfilesAsync(request.QualityProfileId, request.LanguageProfileId);

		var path = await ResolvePathAsync(request.Path, request.RootFolderId, series.Title);

		await EnsureUniquePathAsync(seriesId, movieId: null, path, excludeVersionId: null);

		var version = new MediaVersion
		{
			SeriesId = seriesId,
			Name = request.Name,
			QualityProfileId = request.QualityProfileId,
			LanguageProfileId = request.LanguageProfileId,
			Path = path,
			Monitored = request.Monitored
		};

		_context.Versions.Add(version);
		await _context.SaveChangesAsync();

		return version;
	}

	public async Task<MediaVersion> CreateForMovieAsync(int movieId, AddMediaVersionRequest request)
	{
		var movie = await _context.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Id == movieId);

		if (movie == null)
			throw new NotFoundException();

		await ValidateProfilesAsync(request.QualityProfileId, request.LanguageProfileId);

		var path = await ResolvePathAsync(request.Path, request.RootFolderId, MovieFolderName(movie.Title, movie.Year));

		await EnsureUniquePathAsync(seriesId: null, movieId, path, excludeVersionId: null);

		var version = new MediaVersion
		{
			MovieId = movieId,
			Name = request.Name,
			QualityProfileId = request.QualityProfileId,
			LanguageProfileId = request.LanguageProfileId,
			Path = path,
			Monitored = request.Monitored
		};

		_context.Versions.Add(version);
		await _context.SaveChangesAsync();

		return version;
	}

	/// <summary>
	///     Applies a partial update to a version. A path change does not move existing files on disk yet.
	/// </summary>
	public async Task<MediaVersion> UpdateAsync(int id, UpdateVersionRequest request)
	{
		var version = await _context.Versions.FirstOrDefaultAsync(v => v.Id == id);

		if (version == null)
			throw new NotFoundException();

		if (request.Name != null)
			version.Name = request.Name;

		if (request.QualityProfileId != null || request.LanguageProfileId != null)
			await ValidateProfilesAsync(request.QualityProfileId ?? version.QualityProfileId,
				request.LanguageProfileId ?? version.LanguageProfileId);

		if (request.QualityProfileId != null)
			version.QualityProfileId = request.QualityProfileId.Value;
		if (request.LanguageProfileId != null)
			version.LanguageProfileId = request.LanguageProfileId.Value;
		if (request.Monitored != null)
			version.Monitored = request.Monitored.Value;

		if (request.RootFolderId != null || request.Path != null)
		{
			var path = request.RootFolderId != null
				? await ResolvePathAsync(null, request.RootFolderId, await MediaFolderNameAsync(version))
				: request.Path!;

			await EnsureUniquePathAsync(version.SeriesId, version.MovieId, path, version.Id);

			version.Path = path;
		}

		await _context.SaveChangesAsync();

		return version;
	}

	public async Task DeleteAsync(int id, bool deleteFiles)
	{
		var version = await _context.Versions.FirstOrDefaultAsync(v => v.Id == id);

		if (version == null)
			throw new NotFoundException();

		var siblingCount = await QueryVersions(version.SeriesId, version.MovieId).CountAsync();

		if (siblingCount <= 1)
			throw new BadRequestException("cannot delete the last version of a media");

		if (deleteFiles)
			await DeleteVersionFilesAsync(version);

		_context.Versions.Remove(version);
		await _context.SaveChangesAsync();
	}

	private async Task DeleteVersionFilesAsync(MediaVersion version)
	{
		if (version.SeriesId != null)
		{
			var files = await _context.EpisodeFiles.Where(f => f.MediaVersionId == version.Id).ToListAsync();

			foreach (var file in files)
				DeleteFromDisk(Path.Combine(version.Path, file.RelativePath));
		}
		else
		{
			var files = await _context.MovieFiles.Where(f => f.MediaVersionId == version.Id).ToListAsync();

			foreach (var file in files)
				DeleteFromDisk(Path.Combine(version.Path, file.RelativePath));
		}
	}

	private IQueryable<MediaVersion> QueryVersions(int? seriesId, int? movieId)
	{
		var query = _context.Versions.AsNoTracking();

		query = seriesId != null
			? query.Where(v => v.SeriesId == seriesId)
			: query.Where(v => v.MovieId == movieId);

		return query.OrderBy(v => v.Id);
	}

	private async Task<string> MediaFolderNameAsync(MediaVersion version)
	{
		if (version.SeriesId != null)
			return await _context.Series.AsNoTracking().Where(s => s.Id == version.SeriesId).Select(s => s.Title)
				.FirstAsync();

		var movie = await _context.Movies.AsNoTracking().Where(m => m.Id == version.MovieId)
			.Select(m => new { m.Title, m.Year }).FirstAsync();

		return MovieFolderName(movie.Title, movie.Year);
	}

	private async Task ValidateProfilesAsync(int qualityProfileId, int languageProfileId)
	{
		if (!await _context.QualityProfiles.AsNoTracking().AnyAsync(p => p.Id == qualityProfileId))
			throw new BadRequestException($"Quality profile with id '{qualityProfileId}' does not exist");

		if (!await _context.LanguageProfiles.AsNoTracking().AnyAsync(p => p.Id == languageProfileId))
			throw new BadRequestException($"Language profile with id '{languageProfileId}' does not exist");
	}

	private async Task<string> ResolvePathAsync(string? requestPath, int? rootFolderId, string folderName)
	{
		if (rootFolderId != null)
		{
			var rootFolder = await _context.RootFolders.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rootFolderId);

			if (rootFolder == null)
				throw new BadRequestException($"Root folder with id '{rootFolderId}' does not exist");

			return Path.Combine(rootFolder.Path, SanitizeFolderName(folderName));
		}

		if (!string.IsNullOrWhiteSpace(requestPath))
			return requestPath;

		throw new BadRequestException("either Path or RootFolderId must be provided");
	}

	private async Task EnsureUniquePathAsync(int? seriesId, int? movieId, string path, int? excludeVersionId)
	{
		var query = QueryVersions(seriesId, movieId).Where(v => v.Path == path);

		if (excludeVersionId != null)
			query = query.Where(v => v.Id != excludeVersionId.Value);

		if (await query.AnyAsync())
			throw new BadRequestException("a version with this path already exists for this media");
	}

	private static string MovieFolderName(string title, int? year)
		=> year != null ? $"{title} ({year})" : title;

	private static string SanitizeFolderName(string name)
		=> string.Concat(name.Split(Path.GetInvalidFileNameChars())).Trim();

	private static void DeleteFromDisk(string path)
	{
		if (File.Exists(path))
			File.Delete(path);
	}
}
