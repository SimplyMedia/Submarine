using System.Collections.Immutable;
using Submarine.Core.Parser;
using Submarine.Core.Quality;

namespace Submarine.Api.Services;

/// <summary>
///     Merged view of the built-in edge case quality mappings and user-defined overrides, user overrides win on the
///     same release group
/// </summary>
public class QualityOverrideStore : IQualityOverrideSource
{
	private ImmutableDictionary<string, QualitySource> _overrides =
		Build(Enumerable.Empty<ReleaseGroupQualityOverride>());

	/// <inheritdoc />
	public IReadOnlyDictionary<string, QualitySource> Overrides
		=> _overrides;

	/// <summary>
	///     Rebuilds the merged view from the built-in defaults and the given user-defined overrides
	/// </summary>
	/// <param name="overrides">user-defined overrides</param>
	public void Reload(IEnumerable<ReleaseGroupQualityOverride> overrides)
		=> _overrides = Build(overrides);

	private static ImmutableDictionary<string, QualitySource> Build(IEnumerable<ReleaseGroupQualityOverride> overrides)
	{
		var builder = ImmutableDictionary.CreateBuilder<string, QualitySource>(StringComparer.OrdinalIgnoreCase);

		foreach (var (group, source) in QualityEdgeCasesConstants.EdgeCaseReleaseGroupQualitySourceMapping)
			builder[group] = source;

		foreach (var qualityOverride in overrides)
			builder[qualityOverride.ReleaseGroup] = qualityOverride.Source;

		return builder.ToImmutable();
	}
}
