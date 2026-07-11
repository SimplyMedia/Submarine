using Microsoft.EntityFrameworkCore;
using Submarine.Api.Models.Database;
using Submarine.Api.Services;

namespace Submarine.Api.Events;

/// <summary>
///     Renames the file of an episode whose title changed, when episode renaming is enabled
/// </summary>
public sealed class EpisodeTitleChangedHandler : IEventHandler<EpisodeTitleChangedEvent>
{
	private readonly SubmarineDatabaseContext _context;
	private readonly SettingsService _settingsService;
	private readonly RenameService _renameService;

	public EpisodeTitleChangedHandler(SubmarineDatabaseContext context, SettingsService settingsService,
		RenameService renameService)
	{
		_context = context;
		_settingsService = settingsService;
		_renameService = renameService;
	}

	public async Task HandleAsync(EpisodeTitleChangedEvent @event, CancellationToken cancellationToken)
	{
		var config = await _settingsService.GetNamingConfigAsync();

		if (!config.RenameEpisodes)
			return;

		var episode = await _context.Episodes.AsNoTracking()
			.FirstOrDefaultAsync(e => e.Id == @event.EpisodeId, cancellationToken);

		if (episode?.EpisodeFileId == null)
			return;

		await _renameService.RenameEpisodeFileAsync(episode.EpisodeFileId.Value, cancellationToken);
	}
}
