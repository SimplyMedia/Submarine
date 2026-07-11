namespace Submarine.Metadata.Contracts;

/// <summary>
///     A single season of a <see cref="SeriesResource" />
/// </summary>
/// <param name="SeasonNumber">number of this season</param>
/// <param name="Name">name of this season, if it has one</param>
/// <param name="EpisodeCount">amount of episodes in this season</param>
public record SeasonResource(int SeasonNumber, string? Name, int EpisodeCount);
