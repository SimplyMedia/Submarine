using Submarine.Core.Entities;

namespace Submarine.Core.Profiles;

/// <summary>
///     Checks a release size against the per minute limits of a <see cref="QualityDefinition" />.
///     A missing definition, size, runtime or limit means the size is unlimited.
/// </summary>
public static class QualityDefinitionSizeChecker
{
	/// <summary>
	///     Whether the release size fits between the definition's minimum and maximum size, scaled by the runtime in minutes
	///     and the number of episodes the release covers.
	/// </summary>
	/// <param name="definition">The quality definition of the release's quality, if any.</param>
	/// <param name="sizeBytes">The release size in bytes, if reported.</param>
	/// <param name="runtimeMinutes">Runtime of one episode or the movie in minutes, if known.</param>
	/// <param name="episodeCount">How many episodes the release covers, one for movies and single episodes.</param>
	public static bool IsWithinSize(
		this QualityDefinition? definition,
		long? sizeBytes,
		int? runtimeMinutes,
		int episodeCount = 1)
	{
		if (definition is null || (definition.MinSizeMbPerMinute is null && definition.MaxSizeMbPerMinute is null))
		{
			return true;
		}

		if (sizeBytes is not { } bytes || runtimeMinutes is not { } runtime)
		{
			return true;
		}

		var sizeMb = bytes / 1024d / 1024d;
		var minutes = Math.Max(runtime, 1) * Math.Max(episodeCount, 1);

		if (definition.MinSizeMbPerMinute is { } min && sizeMb < min * minutes)
		{
			return false;
		}

		if (definition.MaxSizeMbPerMinute is { } max && sizeMb > max * minutes)
		{
			return false;
		}

		return true;
	}
}
