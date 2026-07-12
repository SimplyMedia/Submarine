using Submarine.Core.Library;
using Submarine.Metadata.Contracts;

namespace Submarine.Api.Services;

/// <summary>
///     Materializes an episode's season and episode number from its available orderings
///     according to a series' <see cref="EpisodeNumbering" />
/// </summary>
internal static class EpisodeNumberResolver
{
	public static (int SeasonNumber, int EpisodeNumber) Resolve(EpisodeResource resource, EpisodeNumbering numbering)
	{
		var aired = resource.Numbers.FirstOrDefault(n => n.Ordering == EpisodeOrdering.Aired);

		switch (numbering)
		{
			case EpisodeNumbering.DVD:
				var dvd = resource.Numbers.FirstOrDefault(n => n.Ordering == EpisodeOrdering.Dvd) ?? aired;

				return (dvd?.SeasonNumber ?? 0, dvd?.Number ?? 0);

			case EpisodeNumbering.ABSOLUTE:
				var absolute = resource.Numbers.FirstOrDefault(n => n.Ordering == EpisodeOrdering.Absolute);

				return absolute?.AbsoluteNumber != null
					? (1, absolute.AbsoluteNumber.Value)
					: (aired?.SeasonNumber ?? 0, aired?.Number ?? 0);

			default:
				return (aired?.SeasonNumber ?? 0, aired?.Number ?? 0);
		}
	}
}
