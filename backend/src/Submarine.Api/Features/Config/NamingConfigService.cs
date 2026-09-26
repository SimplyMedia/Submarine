using Microsoft.EntityFrameworkCore;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Config;

/// <summary>Shared native naming configuration mutation used by native and compatibility APIs.</summary>
public static class NamingConfigService
{
	public static async Task<NamingConfigResource> UpdateAsync(
		SubmarineDbContext db,
		NamingConfigResource request,
		CancellationToken cancellationToken)
	{
		var config = await db.NamingConfig.SingleAsync(cancellationToken);
		config.RenameEpisodes = request.RenameEpisodes;
		config.RenameMovies = request.RenameMovies;
		config.ReplaceIllegalCharacters = request.ReplaceIllegalCharacters;
		config.ColonReplacement = request.ColonReplacement;
		config.StandardEpisodeFormat = request.StandardEpisodeFormat;
		config.DailyEpisodeFormat = request.DailyEpisodeFormat;
		config.AnimeEpisodeFormat = request.AnimeEpisodeFormat;
		config.SeriesFolderFormat = request.SeriesFolderFormat;
		config.SeasonFolderFormat = request.SeasonFolderFormat;
		config.SpecialsFolderFormat = request.SpecialsFolderFormat;
		config.MovieFormat = request.MovieFormat;
		config.MovieFolderFormat = request.MovieFolderFormat;
		config.MultiEpisodeStyle = request.MultiEpisodeStyle;
		await db.SaveChangesAsync(cancellationToken);
		return NamingConfigResource.FromEntity(config);
	}
}
