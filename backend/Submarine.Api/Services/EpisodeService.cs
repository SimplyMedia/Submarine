using Submarine.Api.Models.Request;
using Submarine.Api.Repository;
using Submarine.Core.Library;

namespace Submarine.Api.Services;

public class EpisodeService
{
	private readonly ISeriesRepository _repository;

	public EpisodeService(ISeriesRepository repository)
		=> _repository = repository;

	public async Task<List<Episode>> BatchMonitorAsync(BatchMonitorEpisodesRequest request)
	{
		var episodes = await _repository.FindEpisodesByIdsAsync(request.EpisodeIds);

		foreach (var episode in episodes)
			episode.Monitored = request.Monitored;

		await _repository.SaveEpisodesAsync(episodes);

		return episodes;
	}
}
