using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Enums;
using Submarine.Core.Naming;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Config;

/// <summary>
///     Naming configuration with example renderings.
/// </summary>
public sealed class NamingConfigModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/config/naming");
		group.MapGet("/", GetAsync);
		group.MapPut("/", PutAsync);
		group.MapGet("/examples", GetExamplesAsync);
	}

	private static async Task<Ok<NamingConfigResource>> GetAsync(SubmarineDbContext db, CancellationToken cancellationToken)
		=> TypedResults.Ok(NamingConfigResource.FromEntity(await db.NamingConfig.AsNoTracking().SingleAsync(cancellationToken)));

	private static async Task<Ok<NamingConfigResource>> PutAsync(
		SubmarineDbContext db,
		IValidator<NamingConfigResource> validator,
		NamingConfigResource request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		return TypedResults.Ok(await NamingConfigService.UpdateAsync(db, request, cancellationToken));
	}

	private static async Task<Ok<IReadOnlyList<NamingSample>>> GetExamplesAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var config = await db.NamingConfig.AsNoTracking().SingleAsync(cancellationToken);
		var customFormatNames = await db.CustomFormats
			.Select(format => format.Name)
			.ToListAsync(cancellationToken);

		return TypedResults.Ok(NamingPreview.Samples(config, new NamingService(), customFormatNames));
	}
}

/// <summary>Naming configuration resource.</summary>
/// <param name="RenameEpisodes">Rename episode files on import.</param>
/// <param name="RenameMovies">Rename movie files on import.</param>
/// <param name="ReplaceIllegalCharacters">Replace characters that are illegal on the target file system.</param>
/// <param name="ColonReplacement">How colons are replaced in names.</param>
/// <param name="StandardEpisodeFormat">Standard episode file name format.</param>
/// <param name="DailyEpisodeFormat">Daily episode file name format.</param>
/// <param name="AnimeEpisodeFormat">Anime episode file name format.</param>
/// <param name="SeriesFolderFormat">Series folder format.</param>
/// <param name="SeasonFolderFormat">Season folder format.</param>
/// <param name="SpecialsFolderFormat">Specials folder format.</param>
/// <param name="MovieFormat">Movie file name format.</param>
/// <param name="MovieFolderFormat">Movie folder format.</param>
/// <param name="MultiEpisodeStyle">How multi-episode files are named.</param>
public sealed record NamingConfigResource(
	bool RenameEpisodes,
	bool RenameMovies,
	bool ReplaceIllegalCharacters,
	ColonReplacement ColonReplacement,
	string StandardEpisodeFormat,
	string DailyEpisodeFormat,
	string AnimeEpisodeFormat,
	string SeriesFolderFormat,
	string SeasonFolderFormat,
	string SpecialsFolderFormat,
	string MovieFormat,
	string MovieFolderFormat,
	MultiEpisodeStyle MultiEpisodeStyle)
{
	/// <summary>Maps the singleton to the resource.</summary>
	public static NamingConfigResource FromEntity(Core.Entities.NamingConfig config)
		=> new(
			config.RenameEpisodes,
			config.RenameMovies,
			config.ReplaceIllegalCharacters,
			config.ColonReplacement,
			config.StandardEpisodeFormat,
			config.DailyEpisodeFormat,
			config.AnimeEpisodeFormat,
			config.SeriesFolderFormat,
			config.SeasonFolderFormat,
			config.SpecialsFolderFormat,
			config.MovieFormat,
			config.MovieFolderFormat,
			config.MultiEpisodeStyle);
}

/// <summary>Validator for <see cref="NamingConfigResource" /> PUT requests.</summary>
public sealed class NamingConfigResourceValidator : AbstractValidator<NamingConfigResource>
{
	/// <inheritdoc />
	public NamingConfigResourceValidator()
	{
		RuleFor(x => x.ColonReplacement).IsInEnum();
		RuleFor(x => x.MultiEpisodeStyle).IsInEnum();
	}
}
