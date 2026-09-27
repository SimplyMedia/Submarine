using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Features.Compat.Shared;
using Submarine.Api.Features.Compat.Shared.Realtime;
using Submarine.Api.Features.MediaFiles;
using Submarine.Api.Features.Rename;
using Submarine.Api.Modules;
using Submarine.Api.Features.Releases;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Naming;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Library;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Search;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Submarine.Api.Features.Compat.Radarr;

public sealed class RadarrModule : IEndpointModule
{
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = CompatRoutes.CreateRestGroup(endpoints, "radarr", "v3");
		group.MapGet("/movie", ListAsync);
		group.MapGet("/movie/lookup", LookupAsync);
		group.MapGet("/movie/{id:int}", GetAsync);
		group.MapPost("/movie", AddAsync);
		group.MapPut("/movie/{id:int}", UpdateAsync);
		group.MapPut("/movie", UpdateCollectionAsync);
		group.MapDelete("/movie/{id:int}", DeleteAsync);
		group.MapDelete("/movie", DeleteCollectionAsync);
		group.MapPut("/movie/editor", EditorAsync);
		group.MapGet("/moviefile", MovieFilesAsync);
		group.MapGet("/moviefile/{id:int}", MovieFileAsync);
		group.MapDelete("/moviefile/{id:int}", DeleteMovieFileAsync);
		group.MapGet("/parse", ParseAsync);
		group.MapGet("/rename", RenamePreviewAsync);
		group.MapPost("/rename", RenameAsync);
		group.MapGet("/release", ReleaseSearchAsync);
		group.MapPost("/release", GrabReleaseAsync);
		group.MapGet("/exclusions", ExclusionsAsync);
		group.MapGet("/exclusions/{id:int}", GetExclusionAsync);
		group.MapGet("/importlistexclusion", ExclusionsAsync);
		group.MapGet("/importlistexclusion/{id:int}", GetExclusionAsync);
		group.MapGet("/credits", CreditsAsync);
		group.MapGet("/extrafile", ExtraFilesAsync);
		group.MapGet("/importlist/movie", ImportListMoviesAsync);
	}

	private static async Task<IResult> ListAsync(HttpRequest request, SubmarineDbContext db, CompatVersionSelection versions, CancellationToken ct)
	{
		var query = db.Movies.AsNoTracking().Include(x => x.Tags).AsQueryable();
		if (int.TryParse(request.Query["tmdbId"], out var tmdbId)) query = query.Where(x => x.TmdbId == tmdbId);
		if (!string.IsNullOrWhiteSpace(request.Query["imdbId"])) query = query.Where(x => x.ImdbId == request.Query["imdbId"].ToString());
		if (bool.TryParse(request.Query["monitored"], out var monitored)) query = query.Where(x => x.Monitored == monitored);
		var movies = await query.OrderBy(x => x.Id).ToListAsync(ct);
		var result = new List<RadarrMovieResource>(movies.Count);
		foreach (var movie in movies)
		{
			if (await IsExcludedAsync(db, movie.Id, ct)) continue;
			result.Add(await ProjectAsync(db, versions, movie, ct));
		}
		return Results.Json(result, CompatJson.Options);
	}

	private static async Task<IResult> GetAsync(int id, SubmarineDbContext db, CompatVersionSelection versions, CancellationToken ct)
	{
		var movie = await db.Movies.AsNoTracking().Include(x => x.Tags).SingleOrDefaultAsync(x => x.Id == id, ct);
		if (movie is null || await IsExcludedAsync(db, id, ct)) return CompatErrors.Message("Movie not found", StatusCodes.Status404NotFound);
		return Results.Json(await ProjectAsync(db, versions, movie, ct), CompatJson.Options);
	}

	private static async Task<IResult> LookupAsync(string term, SubmarineDbContext db, IMetadataClient metadata, CompatVersionSelection versions, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(term)) return CompatErrors.Validation("term", "A search term is required.");
		IReadOnlyList<Submarine.Contracts.Metadata.SearchResultResource> hits;
		if (term.StartsWith("tmdb:", StringComparison.OrdinalIgnoreCase) && int.TryParse(term.AsSpan(5), out var tmdb))
		{
			var movie = await metadata.GetMovieAsync(tmdb, ct);
			hits = movie is null ? [] : [ToSearchResult(movie)];
		}
		else if (term.StartsWith("imdb:", StringComparison.OrdinalIgnoreCase))
		{
			var movie = await metadata.GetMovieByImdbAsync(term[5..], ct);
			hits = movie is null ? [] : [ToSearchResult(movie)];
		}
		else
		{
			int? year = null;
			var name = term;
			var last = term.LastIndexOf(' ');
			if (last > 0 && int.TryParse(term.AsSpan(last + 1), out var parsedYear)) { year = parsedYear; name = term[..last]; }
			hits = await metadata.SearchMoviesAsync(name, year, ct);
		}
		var ids = hits.Select(x => x.TmdbId).OfType<int>().ToArray();
		var existing = await db.Movies.Where(x => ids.Contains(x.TmdbId)).ToDictionaryAsync(x => x.TmdbId, ct);
		var output = new List<RadarrMovieResource>(hits.Count);
		foreach (var hit in hits)
		{
			if (hit.TmdbId is { } id && existing.TryGetValue(id, out var movie))
			{
				if (await IsExcludedAsync(db, movie.Id, ct)) continue;
				output.Add(await ProjectAsync(db, versions, movie, ct));
			}
			else output.Add(RadarrMovieResource.FromLookup(hit));
		}
		return Results.Json(output, CompatJson.Options);
	}

	private static async Task<IResult> AddAsync(HttpRequest request, SubmarineDbContext db, LibraryAdder adder, CompatVersionSelection versions, CancellationToken ct)
	{
		var payload = await JsonSerializer.DeserializeAsync<RadarrMovieResource>(request.Body, CompatJson.Options, ct);
		if (payload is null || payload.TmdbId <= 0 || string.IsNullOrWhiteSpace(payload.RootFolderPath)) return CompatErrors.Validation("tmdbId", "tmdbId and rootFolderPath are required.");
		var requestedAvailability = ParseMinimumAvailability(payload.MinimumAvailability);
		if (payload.MinimumAvailability is not null && requestedAvailability is null) return CompatErrors.Validation("minimumAvailability", "Unsupported minimum availability value.");
		var root = await db.RootFolders.SingleOrDefaultAsync(x => x.Path == payload.RootFolderPath && x.MediaKind == MediaKind.MOVIES, ct);
		if (root is null) return CompatErrors.Validation("rootFolderPath", "The movie root folder does not exist.");
		var profileId = payload.QualityProfileId ?? payload.ProfileId;
		if (profileId is null || (payload.QualityProfileId is not null && payload.ProfileId is not null && payload.QualityProfileId != payload.ProfileId)) return CompatErrors.Validation("qualityProfileId", "A valid qualityProfileId is required.");
		var languageId = payload.LanguageProfileId ?? await db.LanguageProfiles.OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
		if (languageId is null) return CompatErrors.Validation("languageProfileId", "A language profile is required.");
		var tags = payload.Tags ?? [];
		var movie = await adder.AddMovieAsync(new AddMovieOptions(payload.TmdbId, payload.Title, root.Id, false, payload.Monitored ?? true,
			requestedAvailability ?? MinimumAvailability.RELEASED, tags, [new VersionOptions("main", profileId.Value, languageId.Value, root.Id)], payload.AddOptions?.SearchForMovie ?? false), ct);
		var selected = movie.Versions.OrderBy(x => x.Id).First();
		await versions.BindMovieVersionAsync(movie.Id, selected.Id, ct);
		var saved = await db.Movies.AsNoTracking().Include(x => x.Tags).SingleAsync(x => x.Id == movie.Id, ct);
		var response = await ProjectAsync(db, versions, saved, ct);
		return Results.Json(response, CompatJson.Options, statusCode: StatusCodes.Status201Created);
	}

	private static async Task<IResult> UpdateAsync(int id, HttpRequest request, SubmarineDbContext db, LibraryMutator mutator, CompatVersionSelection versions, Submarine.Api.Features.MediaVersions.MediaVersionMover mover, CancellationToken ct, bool moveFiles = false)
	{
		var payload = await JsonSerializer.DeserializeAsync<RadarrMovieResource>(request.Body, CompatJson.Options, ct);
		if (payload is null) return CompatErrors.Validation("movie", "A movie resource is required.");
		if (payload.Id != 0 && payload.Id != id) return CompatErrors.Validation("id", "The URL id must match the body id.");
		var movie = await db.Movies.Include(x => x.Tags).Include(x => x.Versions).SingleOrDefaultAsync(x => x.Id == id, ct);
		if (movie is null || await IsExcludedAsync(db, id, ct)) return CompatErrors.Message("Movie not found", StatusCodes.Status404NotFound);
		var requestedAvailability = ParseMinimumAvailability(payload.MinimumAvailability);
		if (payload.MinimumAvailability is not null && requestedAvailability is null) return CompatErrors.Validation("minimumAvailability", "Unsupported minimum availability value.");
		var binding = await versions.GetForMovieAsync(id, ct);
		var version = binding?.MediaVersionId is { } selectedId ? movie.Versions.Single(x => x.Id == selectedId) : null;
		if (payload.RootFolderPath is not null || payload.QualityProfileId is not null || payload.ProfileId is not null || payload.LanguageProfileId is not null)
		{
			if (version is null) return CompatErrors.Message("Movie has no selected media version.", StatusCodes.Status409Conflict);
			int? newRootId = null;
			if (payload.RootFolderPath is not null)
			{
				var root = await db.RootFolders.SingleOrDefaultAsync(x => x.Path == payload.RootFolderPath && x.MediaKind == MediaKind.MOVIES, ct);
				if (root is null) return CompatErrors.Validation("rootFolderPath", "The movie root folder does not exist.");
				newRootId = root.Id;
			}
			if (payload.QualityProfileId is not null && payload.ProfileId is not null && payload.QualityProfileId != payload.ProfileId) return CompatErrors.Validation("qualityProfileId", "Conflicting profile ids.");
			var profile = payload.QualityProfileId ?? payload.ProfileId ?? version.QualityProfileId;
			await mutator.UpdateMovieAsync(id, new UpdateMovieOptions(payload.Monitored, requestedAvailability, null,
				payload.Tags, null, false, [new UpdateVersionOptions(version.Id, version.Name, profile, payload.LanguageProfileId ?? version.LanguageProfileId, version.Path)]), ct);
			if (newRootId is { } rootId && rootId != version.RootFolderId)
			{
				await mover.ChangeRootFolderAsync(version.Id, rootId, moveFiles, ct);
			}
		}
		else if (payload.Monitored is not null || payload.MinimumAvailability is not null || payload.Tags is not null)
		{
			await mutator.UpdateMovieAsync(id, new UpdateMovieOptions(payload.Monitored, requestedAvailability, null, payload.Tags, null, false, []), ct);
		}
		var saved = await db.Movies.AsNoTracking().Include(x => x.Tags).SingleAsync(x => x.Id == id, ct);
		return Results.Json(await ProjectAsync(db, versions, saved, ct), CompatJson.Options);
	}

	private static async Task<IResult> UpdateCollectionAsync(HttpRequest request, SubmarineDbContext db, LibraryMutator mutator, CompatVersionSelection versions, Submarine.Api.Features.MediaVersions.MediaVersionMover mover, CancellationToken ct)
	{
		var resources = await JsonSerializer.DeserializeAsync<RadarrMovieResource[]>(request.Body, CompatJson.Options, ct);
		if (resources is null) return CompatErrors.Validation("movies", "A movie array is required.");
		var results = new List<RadarrMovieResource>(resources.Length);
		foreach (var item in resources)
		{
			if (item.Id <= 0) return CompatErrors.Validation("id", "Movie ids must be local ids.");
			var result = await UpdateAsync(item.Id, JsonRequest(item), db, mutator, versions, mover, ct);
			if (result is IStatusCodeHttpResult { StatusCode: >= 400 }) return result;
			results.Add((await db.Movies.AsNoTracking().Include(x => x.Tags).SingleAsync(x => x.Id == item.Id, ct)) is { } m ? await ProjectAsync(db, versions, m, ct) : item);
		}
		return Results.Json(results, CompatJson.Options);
	}

	private static async Task<IResult> DeleteAsync(int id, bool? addImportExclusion, bool? addImportListExclusion, LibraryMutator mutator, SubmarineDbContext db, CompatVersionSelection versions, SelectedMovieDeletionService selectedDeletion, CancellationToken ct, bool deleteFiles = false)
	{
		if (addImportExclusion is not null && addImportListExclusion is not null && addImportExclusion != addImportListExclusion) return CompatErrors.Validation("addImportExclusion", "Conflicting import exclusion flags.");
		var movie = await db.Movies.Include(x => x.Versions).SingleOrDefaultAsync(x => x.Id == id, ct);
		if (movie is null) return CompatErrors.Message("Movie not found", StatusCodes.Status404NotFound);
		var binding = await versions.GetForMovieAsync(id, ct);
		var addExclusion = addImportExclusion ?? addImportListExclusion ?? false;
		if (binding?.MediaVersionId is { } selected)
		{
			if (movie.Versions.Count > 1) await versions.ExcludeMovieAsync(id, ct);
			await selectedDeletion.DeleteAsync(id, selected, deleteFiles, addExclusion, ct);
		}
		else
		{
			await mutator.DeleteMovieAsync(id, deleteFiles, addExclusion, ct);
		}
		return Results.NoContent();
	}

	private static async Task<IResult> DeleteCollectionAsync(HttpRequest request, LibraryMutator mutator, SubmarineDbContext db, CompatVersionSelection versions, SelectedMovieDeletionService selectedDeletion, CancellationToken ct)
	{
		if (!int.TryParse(request.Query["id"], out var id)) return CompatErrors.Validation("id", "Movie id is required.");
		return await DeleteAsync(id, bool.TryParse(request.Query["addImportExclusion"], out var exclusion) ? exclusion : null, null, mutator, db, versions, selectedDeletion, ct, bool.TryParse(request.Query["deleteFiles"], out var files) && files);
	}

	private static async Task<IResult> EditorAsync(HttpRequest request, SubmarineDbContext db, LibraryMutator mutator, CompatVersionSelection versions, Submarine.Api.Features.MediaVersions.MediaVersionMover mover, CancellationToken ct)
	{
		using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
		var root = document.RootElement;
		var ids = root.TryGetProperty("movieIds", out var movieIds) ? movieIds.EnumerateArray().Select(x => x.GetInt32()).ToArray() : [];
		if (ids.Length == 0 || ids.Distinct().Count() != ids.Length) return CompatErrors.Validation("movieIds", "Movie ids must be a nonempty, duplicate-free list.");
		if (await db.Movies.CountAsync(x => ids.Contains(x.Id), ct) != ids.Length) return CompatErrors.Validation("movieIds", "One or more movies do not exist.");
		var rootFolderPath = root.TryGetProperty("rootFolderPath", out var rootFolderPathElement) ? rootFolderPathElement.GetString() : null;
		Submarine.Core.Entities.RootFolder? newRoot = null;
		if (rootFolderPath is not null)
		{
			newRoot = await db.RootFolders.SingleOrDefaultAsync(x => x.Path == rootFolderPath && x.MediaKind == MediaKind.MOVIES, ct);
			if (newRoot is null) return CompatErrors.Validation("rootFolderPath", "The movie root folder does not exist.");
		}
		var moveFiles = TryBool(root, "moveFiles") ?? false;
		var requestedAvailability = ParseMinimumAvailability(root.TryGetProperty("minimumAvailability", out var avail) ? avail.GetString() : null);
		if (root.TryGetProperty("minimumAvailability", out _) && requestedAvailability is null) return CompatErrors.Validation("minimumAvailability", "Unsupported minimum availability value.");
		var profileId = TryInt(root, "qualityProfileId") ?? TryInt(root, "profileId");
		var languageProfileId = TryInt(root, "languageProfileId");
		if (root.TryGetProperty("qualityProfileId", out _) && root.TryGetProperty("profileId", out _) && TryInt(root, "qualityProfileId") != TryInt(root, "profileId"))
			return CompatErrors.Validation("qualityProfileId", "Conflicting profile ids.");
		var selected = new Dictionary<int, MediaVersion>();
		if (profileId is not null || languageProfileId is not null || newRoot is not null)
		{
			foreach (var id in ids)
			{
				var binding = await versions.GetForMovieAsync(id, ct);
				if (binding?.MediaVersionId is not { } versionId) return CompatErrors.Message($"Movie {id} has no selected media version.", StatusCodes.Status409Conflict);
				selected[id] = await db.MediaVersions.AsNoTracking().SingleAsync(x => x.Id == versionId, ct);
			}
		}
		var tagOperation = ReadTagOperation(root);
		foreach (var id in ids)
		{
			var versionUpdates = selected.TryGetValue(id, out var version)
				? new[] { new UpdateVersionOptions(version.Id, version.Name, profileId ?? version.QualityProfileId, languageProfileId ?? version.LanguageProfileId, version.Path) }
				: [];
			await mutator.UpdateMovieAsync(id, new UpdateMovieOptions(TryBool(root, "monitored"), requestedAvailability, null, null, null, false, versionUpdates), ct);
			if (tagOperation is not null)
				await mutator.BulkUpdateMoviesAsync(new BulkUpdateMovieOptions([id], null, null, null, null, null, false, tagOperation), ct);
			if (newRoot is not null && selected.TryGetValue(id, out var movedVersion) && newRoot.Id != movedVersion.RootFolderId)
				await mover.ChangeRootFolderAsync(movedVersion.Id, newRoot.Id, moveFiles, ct);
		}
		return Results.Json(new { movies = ids.Length }, CompatJson.Options);
	}

	private static TagOperation? ReadTagOperation(JsonElement root)
	{
		if (!root.TryGetProperty("tags", out var tags)) return null;
		var ids = tags.EnumerateArray().Select(x => x.GetInt32()).ToArray();
		var mode = root.TryGetProperty("applyTags", out var applyTags) ? applyTags.GetString()?.ToLowerInvariant() : "replace";
		return new TagOperation(mode switch
		{
			"add" => TagOperationMode.ADD,
			"remove" => TagOperationMode.REMOVE,
			"replace" => TagOperationMode.REPLACE,
			_ => throw new ArgumentException("applyTags must be add, remove, or replace.")
		}, ids);
	}

	private static async Task<IResult> MovieFilesAsync(int? movieId, int[]? movieFileIds, SubmarineDbContext db, CompatVersionSelection versions, CancellationToken ct)
	{
		var query = db.MovieFiles.AsNoTracking().Include(x => x.MediaVersion).AsQueryable();
		if (movieId is not null) query = query.Where(x => x.MovieId == movieId);
		if (movieFileIds is { Length: > 0 }) query = query.Where(x => movieFileIds.Contains(x.Id));
		var files = await query.OrderByDescending(x => x.DateAdded).ThenByDescending(x => x.Id).ToListAsync(ct);
		var output = new List<RadarrMovieFileResource>();
		foreach (var file in files)
		{
			var binding = await versions.GetForMovieAsync(file.MovieId, ct);
			if (binding?.MediaVersionId != file.MediaVersionId) continue;
			output.Add(await ProjectFileAsync(db, file, ct));
		}
		return Results.Json(output, CompatJson.Options);
	}

	private static async Task<IResult> MovieFileAsync(int id, SubmarineDbContext db, CompatVersionSelection versions, CancellationToken ct)
	{
		var file = await db.MovieFiles.AsNoTracking().Include(x => x.MediaVersion).SingleOrDefaultAsync(x => x.Id == id, ct);
		if (file is null || (await versions.GetForMovieAsync(file.MovieId, ct))?.MediaVersionId != file.MediaVersionId) return CompatErrors.Message("Movie file not found", StatusCodes.Status404NotFound);
		return Results.Json(await ProjectFileAsync(db, file, ct), CompatJson.Options);
	}

	private static async Task<IResult> DeleteMovieFileAsync(int id, SubmarineDbContext db, CompatVersionSelection versions, Submarine.Infrastructure.Import.MovieFileOperationService operations, CancellationToken ct)
	{
		var file = await db.MovieFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
		if (file is null || (await versions.GetForMovieAsync(file.MovieId, ct))?.MediaVersionId != file.MediaVersionId) return CompatErrors.Message("Movie file not found", StatusCodes.Status404NotFound);
		await operations.DeleteAsync(file.Id, ct);
		return Results.NoContent();
	}

	private static async Task<IResult> ParseAsync(string title, SubmarineDbContext db, Submarine.Core.Parser.IParser<Submarine.Core.Release.BaseRelease> parser, CancellationToken ct)
	{
		Submarine.Core.Release.BaseRelease parsed;
		try { parsed = parser.Parse(title); } catch (Exception) { return CompatErrors.Validation("title", "Release title could not be parsed."); }
		var releaseTitle = parsed.Title;
		var clean = new string(releaseTitle.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
		var candidates = await db.Movies.AsNoTracking().Select(x => new { x.Id, x.CleanTitle }).ToListAsync(ct);
		var movie = candidates.FirstOrDefault(x => new string(x.CleanTitle.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray()) == clean);
		var parsedInfo = new { title = parsed.Title, year = parsed.Year, quality = parsed.Quality.Resolution.Source?.ToString(), resolution = parsed.Quality.Resolution.Resolution?.ToString() };
		return Results.Json(new { parsedMovieInfo = parsedInfo, movie = movie is null ? null : new { id = movie.Id } }, CompatJson.Options);
	}

	private static async Task<IResult> RenamePreviewAsync(int movieId, SubmarineDbContext db, CompatVersionSelection versions, NamingService namingService, CancellationToken ct)
	{
		var binding = await versions.GetForMovieAsync(movieId, ct);
		if (binding?.MediaVersionId is null) return CompatErrors.Message("Movie not found", StatusCodes.Status404NotFound);
		var movie = await db.Movies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == movieId, ct);
		if (movie is null) return CompatErrors.Message("Movie not found", StatusCodes.Status404NotFound);
		var naming = await db.NamingConfig.AsNoTracking().SingleAsync(ct);
		var files = await db.MovieFiles.AsNoTracking().Where(x => x.MovieId == movieId && x.MediaVersionId == binding.MediaVersionId).OrderBy(x => x.Id).ToListAsync(ct);
		return Results.Json(files.Select(x => new { movieFileId = x.Id, existingPath = x.RelativePath, newPath = namingService.RenderMovieFileName(movie, x, naming) + Path.GetExtension(x.RelativePath) }), CompatJson.Options);
	}

	private static async Task<IResult> RenameAsync(int movieId, SubmarineDbContext db, CompatVersionSelection versions, ICommandQueue queue, CancellationToken ct)
	{
		var binding = await versions.GetForMovieAsync(movieId, ct);
		if (binding?.MediaVersionId is null) return CompatErrors.Message("Movie not found", StatusCodes.Status404NotFound);
		var fileIds = await db.MovieFiles.Where(x => x.MovieId == movieId && x.MediaVersionId == binding.MediaVersionId).Select(x => x.Id).ToListAsync(ct);
		var command = await queue.EnqueueAsync(new RenameMovieCommand(movieId, fileIds), CommandTrigger.MANUAL, CommandPriority.HIGH, ct);
		return Results.Json(command, CompatJson.Options);
	}

	private static async Task<IResult> CreditsAsync(int movieId, SubmarineDbContext db, IMetadataClient metadata, CancellationToken ct)
	{
		var tmdbId = await db.Movies.AsNoTracking().Where(x => x.Id == movieId).Select(x => (int?)x.TmdbId).SingleOrDefaultAsync(ct);
		if (tmdbId is null) return CompatErrors.Message("Movie not found", StatusCodes.Status404NotFound);
		var movie = await metadata.GetMovieAsync(tmdbId.Value, ct);
		var credits = movie?.Credits ?? [];
		return Results.Json(credits.Select(x => new { id = x.TmdbPersonId, movieId, personName = x.Name, personTmdbId = x.TmdbPersonId,
			character = x.Character, department = x.Department, job = x.Job, order = x.Order,
			images = x.ProfileUrl is null ? Array.Empty<RadarrImageResource>() : [new RadarrImageResource("person", x.ProfileUrl)] }), CompatJson.Options);
	}

	private static async Task<IResult> ExtraFilesAsync(int movieId, SubmarineDbContext db, CompatVersionSelection versions, CancellationToken ct)
	{
		var binding = await versions.GetForMovieAsync(movieId, ct);
		if (binding?.MediaVersionId is null) return CompatErrors.Message("Movie not found", StatusCodes.Status404NotFound);
		var version = await db.MediaVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == binding.MediaVersionId, ct);
		if (version is null) return Results.Json(Array.Empty<object>(), CompatJson.Options);
		var root = await db.RootFolders.AsNoTracking().SingleAsync(x => x.Id == version.RootFolderId, ct);
		var configured = (await db.MediaManagementConfig.AsNoTracking().Select(x => x.ExtraFileExtensions).SingleAsync(ct))
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(x => x.StartsWith('.') ? x : "." + x).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var versionPath = Path.GetFullPath(Path.Combine(root.Path, version.Path));
		var videoFiles = await db.MovieFiles.AsNoTracking().Where(x => x.MovieId == movieId && x.MediaVersionId == version.Id).ToListAsync(ct);
		var entries = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		foreach (var video in videoFiles)
		{
			var videoPath = Path.GetFullPath(Path.Combine(versionPath, video.RelativePath));
			var folder = Path.GetDirectoryName(videoPath);
			if (folder is null || !Directory.Exists(folder)) continue;
			var stem = Path.GetFileNameWithoutExtension(videoPath);
			foreach (var candidate in Directory.EnumerateFiles(folder))
			{
				var extension = Path.GetExtension(candidate);
				var sidecarStem = Path.GetFileNameWithoutExtension(candidate);
				if (!configured.Contains(extension) || !(sidecarStem.Equals(stem, StringComparison.OrdinalIgnoreCase) || sidecarStem.StartsWith(stem + ".", StringComparison.OrdinalIgnoreCase))) continue;
				var full = Path.GetFullPath(candidate);
				var relative = Path.GetRelativePath(versionPath, full);
				if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) continue;
				var idBytes = SHA256.HashData(Encoding.UTF8.GetBytes(full));
				var sidecarId = BitConverter.ToInt32(idBytes, 0) & int.MaxValue;
				entries[full] = new { id = sidecarId, movieId, movieFileId = video.Id, relativePath = relative, extension, type = "subtitle" };
			}
		}
		return Results.Json(entries.Values, CompatJson.Options);
	}
	private static async Task<IResult> ReleaseSearchAsync(int movieId, CompatVersionSelection versions, InteractiveSearchService search, CancellationToken ct)
	{
		var binding = await versions.GetForMovieAsync(movieId, ct);
		if (binding?.MediaVersionId is null) return CompatErrors.Message("Movie not found", StatusCodes.Status404NotFound);
		var found = await search.SearchAsync(null, null, null, movieId, null, binding.MediaVersionId, cancellationToken: ct);
		var releases = found.Select(result =>
		{
			var parsed = result.Candidate.Parsed;
			var quality = parsed.Quality.Resolution;
			var decision = result.Decisions.SingleOrDefault(x => x.MediaVersionId == binding.MediaVersionId);
			return new
			{
				guid = result.Candidate.Info.Guid,
				indexerId = result.Candidate.Info.IndexerId,
				title = result.Candidate.Info.Title,
				quality = new { quality = new { id = CompatQualityMap.ToUpstreamQualityId(quality, "radarr") ?? 0, name = quality.Name, source = quality.Source?.ToString().ToLowerInvariant(), resolution = quality.Resolution is { } resolution ? (int)resolution : (int?)null }, revision = new { version = parsed.Quality.Revision.Version, real = parsed.Quality.Revision.IsReal, isRepack = parsed.Quality.Revision.IsRepack } },
				languages = parsed.Languages.Select(language => new { id = CompatLanguageMap.ToUpstreamId(language, "radarr"), name = CompatLanguageMap.Name(language) }).ToArray(),
				size = result.Candidate.Info.Size,
				protocol = result.Candidate.Info.Protocol.ToString().ToLowerInvariant(),
				publishDate = result.Candidate.Info.PublishDate,
				infoUrl = result.Candidate.Info.InfoUrl,
				downloadUrl = result.Candidate.Info.DownloadUrl ?? result.Candidate.Info.MagnetUrl,
				approved = decision?.Approved ?? false,
				rejections = decision?.Rejections.Select(x => new { reason = x.Reason.ToString() }).ToArray() ?? [],
				seeders = result.Candidate.Info.Seeders,
				leechers = result.Candidate.Info.Leechers
			};
		}).ToArray();
		return Results.Json(releases, CompatJson.Options);
	}

	private static async Task<IResult> GrabReleaseAsync(int? movieId, HttpRequest httpRequest, CompatVersionSelection versions, ReleaseGrabOperation operation,
		ReleaseResultCache cache, FluentValidation.IValidator<GrabReleaseRequest> validator, CancellationToken ct)
	{
		var request = await JsonSerializer.DeserializeAsync<RadarrReleaseGrabRequest>(httpRequest.Body, CompatJson.Options, ct);
		if (request is null) return CompatErrors.Validation("release", "A release resource is required.");
		var localMovieId = movieId ?? request.MovieId;
		if (localMovieId is null && cache.TryGet(request.Guid, request.IndexerId, out var cached))
		{
			localMovieId = cached.MatchedMovieId;
		}
		if (localMovieId is null || localMovieId <= 0) return CompatErrors.Validation("movieId", "The release is not associated with a known movie.");
		var binding = await versions.GetForMovieAsync(localMovieId.Value, ct);
		if (binding?.MediaVersionId is not { } versionId) return CompatErrors.Message("Movie not found", StatusCodes.Status404NotFound);
		var grab = new GrabReleaseRequest(request.Guid, request.IndexerId, versionId, null, null, localMovieId.Value,
			request.QualitySource, request.QualityResolution, request.Languages?.ToList(), request.Override);
		await validator.ValidateOrThrowAsync(grab, ct);
		var result = await operation.ExecuteAsync(grab, ct);
		return result is null ? CompatErrors.Message("Release not found", StatusCodes.Status404NotFound) : Results.Json(result, CompatJson.Options);
	}

	private static async Task<IResult> CollectionsAsync(SubmarineDbContext db, IMetadataClient metadata, CancellationToken ct)
	{
		var collections = await db.Collections.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
		var results = new List<object>(collections.Count);
		foreach (var collection in collections)
		{
			var upstream = await metadata.GetCollectionAsync(collection.TmdbCollectionId, ct);
			var movies = upstream?.Movies.Select(movie => new
			{
				tmdbId = movie.TmdbId,
				imdbId = movie.ImdbId,
				title = movie.Title,
				year = movie.Year,
				overview = movie.Overview,
				images = movie.PosterUrl is null ? Array.Empty<RadarrImageResource>() : [new RadarrImageResource("poster", movie.PosterUrl)]
			}).ToArray() ?? [];
			results.Add(new
			{
				id = collection.Id,
				tmdbCollectionId = collection.TmdbCollectionId,
				title = collection.Title,
				overview = collection.Overview,
				poster = collection.PosterUrl,
				monitored = collection.Monitored,
				rootFolderId = collection.RootFolderId,
				qualityProfileId = collection.QualityProfileId,
				languageProfileId = collection.LanguageProfileId,
				minimumAvailability = collection.MinimumAvailability.ToString().ToLowerInvariant(),
				searchOnAdd = collection.SearchOnAdd,
				movies
			});
		}
		return Results.Json(results, CompatJson.Options);
	}
	private static async Task<IResult> ExclusionsAsync(SubmarineDbContext db, CancellationToken ct)
		=> Results.Json(await db.ImportListExclusions.AsNoTracking().Where(x => x.TmdbId != null).OrderBy(x => x.Id)
			.Select(x => new { id = x.Id, tmdbId = x.TmdbId, movieTitle = x.Title, movieYear = x.Year }).ToListAsync(ct), CompatJson.Options);

	private static async Task<IResult> GetExclusionAsync(int id, SubmarineDbContext db, CancellationToken ct)
	{
		var exclusion = await db.ImportListExclusions.AsNoTracking().Where(x => x.Id == id && x.TmdbId != null)
			.Select(x => new { id = x.Id, tmdbId = x.TmdbId, movieTitle = x.Title, movieYear = x.Year }).SingleOrDefaultAsync(ct);
		return exclusion is null ? CompatErrors.Message("Movie exclusion not found", StatusCodes.Status404NotFound) : Results.Json(exclusion, CompatJson.Options);
	}

	private static async Task<IResult> ImportListMoviesAsync(SubmarineDbContext db, IEnumerable<Submarine.Infrastructure.ImportLists.IImportList> implementations, IMetadataClient metadata, CancellationToken ct, bool includeRecommendations = false)
	{
		if (includeRecommendations) return CompatErrors.Message("TMDB recommendation sourcing is not available in the native import-list service.", StatusCodes.Status501NotImplemented);
		var lists = await db.ImportLists.AsNoTracking().Where(x => x.MediaKind == MediaKind.MOVIES && x.Enable).OrderBy(x => x.Id).ToListAsync(ct);
		var output = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		foreach (var list in lists)
		{
			var implementation = implementations.FirstOrDefault(x => x.Handles(list.Type));
			if (implementation is null) continue;
			foreach (var item in await implementation.FetchAsync(list, ct))
			{
				Submarine.Contracts.Metadata.MovieResource? movie = item.TmdbId is { } tmdb
					? await metadata.GetMovieAsync(tmdb, ct)
					: item.ImdbId is { Length: > 0 } imdb ? await metadata.GetMovieByImdbAsync(imdb, ct) : null;
				var key = item.TmdbId?.ToString() ?? item.ImdbId ?? $"{item.Title}:{item.Year}";
				output.TryAdd(key, movie is null
					? new { tmdbId = item.TmdbId, imdbId = item.ImdbId, title = item.Title, year = item.Year, overview = (string?)null, images = Array.Empty<RadarrImageResource>() }
					: new { tmdbId = (int?)movie.TmdbId, imdbId = movie.ImdbId, title = movie.Title, year = movie.Year, overview = movie.Overview,
						images = movie.PosterUrl is null ? Array.Empty<RadarrImageResource>() : [new RadarrImageResource("poster", movie.PosterUrl)] });
			}
		}
		return Results.Json(output.Values, CompatJson.Options);
	}

	public Task<RadarrMovieResource> ProjectForRealtimeAsync(SubmarineDbContext db, CompatVersionSelection versions, Movie movie, CancellationToken ct)
		=> ProjectAsync(db, versions, movie, ct);

	private static async Task<RadarrMovieResource> ProjectAsync(SubmarineDbContext db, CompatVersionSelection versions, Movie movie, CancellationToken ct)
	{
		var binding = await versions.GetForMovieAsync(movie.Id, ct);
		var version = binding?.MediaVersionId is { } id ? await db.MediaVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) : null;
		var versionRoot = version is null ? null : await db.RootFolders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == version.RootFolderId, ct);
		var file = version is null ? null : await db.MovieFiles.AsNoTracking().Include(x => x.MediaVersion).Where(x => x.MovieId == movie.Id && x.MediaVersionId == version.Id).OrderByDescending(x => x.DateAdded).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
		var fileStats = version is null ? null : await db.MovieFiles.AsNoTracking().Where(x => x.MovieId == movie.Id && x.MediaVersionId == version.Id)
			.GroupBy(_ => 1).Select(g => new { Count = g.Count(), Size = g.Sum(f => f.Size) }).FirstOrDefaultAsync(ct);
		var config = await db.IndexerConfig.AsNoTracking().SingleAsync(ct);
		var availableDate = movie.MinimumAvailability == MinimumAvailability.IN_CINEMAS
			? movie.InCinemasDate
			: movie.PhysicalReleaseDate ?? movie.DigitalReleaseDate ?? movie.InCinemasDate;
		var isAvailable = availableDate is { } release && release.AddDays(config.AvailabilityDelayDays) <= DateTime.UtcNow;
		var path = version is null || versionRoot is null ? null : Path.GetFullPath(Path.Combine(versionRoot.Path, version.Path));
		return new RadarrMovieResource
		{
			Id = movie.Id, TmdbId = movie.TmdbId, ImdbId = movie.ImdbId, Title = movie.Title, OriginalTitle = movie.OriginalTitle, SortTitle = movie.SortTitle,
			TitleSlug = string.Join("-", movie.Title.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)) + (movie.Year is null ? "" : $"-{movie.Year}"), Year = movie.Year,
			Overview = movie.Overview, Status = StatusName(movie.Status), MinimumAvailability = MinimumAvailabilityName(movie.MinimumAvailability), Monitored = movie.Monitored,
			InCinemas = movie.InCinemasDate, DigitalRelease = movie.DigitalReleaseDate, PhysicalRelease = movie.PhysicalReleaseDate, Added = movie.CreatedAt, RootFolderPath = versionRoot?.Path,
			Path = path, QualityProfileId = version?.QualityProfileId, LanguageProfileId = version?.LanguageProfileId, Tags = [.. movie.Tags.Select(x => x.Id)],
			Images = [new RadarrImageResource("poster", movie.PosterUrl), new RadarrImageResource("fanart", movie.BackdropUrl)],
			Genres = movie.Genres, Runtime = movie.Runtime, Studio = movie.Studio, HasFile = fileStats?.Count > 0, IsAvailable = isAvailable, SizeOnDisk = fileStats?.Size ?? 0,
			MovieFile = file is null ? null : await ProjectFileAsync(db, file, ct), Statistics = new RadarrStatistics(fileStats?.Count ?? 0, fileStats?.Size ?? 0)
		};
	}

	private static async Task<RadarrMovieFileResource> ProjectFileAsync(SubmarineDbContext db, MovieFile file, CancellationToken ct)
	{
		var root = await db.RootFolders.AsNoTracking().SingleAsync(x => x.Id == file.MediaVersion.RootFolderId, ct);
		return new RadarrMovieFileResource(file.Id, file.MovieId, file.RelativePath, Path.GetFullPath(Path.Combine(root.Path, file.MediaVersion.Path, file.RelativePath)),
			file.Size, file.DateAdded, new { quality = new { id = CompatQualityMap.ToUpstreamQualityId(file.Quality.Resolution, "radarr") ?? 0, name = QualityName(file.Quality.Resolution), source = file.Quality.Resolution.Source?.ToString().ToLowerInvariant(), resolution = file.Quality.Resolution.Resolution is { } r ? (int)r : (int?)null }, revision = new { version = file.Quality.Revision.Version, real = file.Quality.Revision.IsReal, isRepack = file.Quality.Revision.IsRepack } },
			file.Languages.Select(x => x.ToString()).ToArray(), file.ReleaseGroup, file.SceneName, file.Edition, MediaInfoMapper.ToDto(file.MediaInfo));
	}

	private static Task<bool> IsExcludedAsync(SubmarineDbContext db, int id, CancellationToken ct)
		=> db.CompatLibraryBindings.AnyAsync(x => x.Facade == "radarr" && x.MovieId == id && x.Excluded, ct);

	private static Submarine.Contracts.Metadata.SearchResultResource ToSearchResult(Submarine.Contracts.Metadata.MovieResource movie)
		=> new(null, movie.TmdbId, movie.ImdbId, movie.Title, movie.Year, movie.Overview, movie.PosterUrl, movie.Status.ToString(), "tmdb");

	private static MinimumAvailability? ParseMinimumAvailability(string? value) => value?.ToLowerInvariant() switch
	{
		null => null,
		"announced" => MinimumAvailability.ANNOUNCED,
		"incinemas" => MinimumAvailability.IN_CINEMAS,
		"released" => MinimumAvailability.RELEASED,
		_ => null
	};

	private static string StatusName(MovieStatus status) => status switch
	{
		MovieStatus.IN_CINEMAS => "inCinemas",
		_ => status.ToString().ToLowerInvariant()
	};

	private static string MinimumAvailabilityName(MinimumAvailability availability) => availability switch
	{
		MinimumAvailability.IN_CINEMAS => "inCinemas",
		_ => availability.ToString().ToLowerInvariant()
	};

	private static string QualityName(Submarine.Core.Quality.QualityResolutionModel quality)
	{
		var source = quality.Source?.ToString() switch
		{
			"WEB_DL" => "WEBDL",
			"WEB_RIP" => "WEBRip",
			"BLURAY" => "Bluray",
			"BLURAY_REMUX" => "Bluray Remux",
			"BLURAY_DISK" => "BR-DISK",
			"RAW_HD" => "Raw-HD",
			_ => quality.Source?.ToString() ?? "Unknown"
		};
		return quality.Resolution is { } resolution ? $"{source}-{(int)resolution}p" : source;
	}

	private static bool? TryBool(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
	private static int? TryInt(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;
	private static HttpRequest JsonRequest<T>(T value)
	{
		var request = new DefaultHttpContext().Request;
		request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(value, CompatJson.Options));
		return request;
	}
}
