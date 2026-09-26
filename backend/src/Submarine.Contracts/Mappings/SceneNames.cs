namespace Submarine.Contracts.Mappings;

/// <summary>
/// An alternate scene title for a TVDB series, used for release title matching.
/// </summary>
public sealed record SceneNameResource(
	int Id,
	int TvdbId,
	string SceneName,
	int? SeasonNumber);

public sealed record CreateSceneNameRequest(
	int TvdbId,
	string SceneName,
	int? SeasonNumber);

public sealed record UpdateSceneNameRequest(
	int TvdbId,
	string SceneName,
	int? SeasonNumber);
