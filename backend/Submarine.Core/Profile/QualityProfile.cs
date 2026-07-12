using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;
using Submarine.Core.Quality;

namespace Submarine.Core.Profile;

/// <summary>
///     A Quality Profile defines which Qualities are wanted for a Series or Movie and in which order they should be
///     upgraded
/// </summary>
public class QualityProfile : ICreatable, IUpdatable
{
	/// <summary>
	///     Id of the quality profile
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Name of the quality profile
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	///     Whether Releases of this profile should be upgraded to a better Quality once a lower Quality was already
	///     downloaded
	/// </summary>
	public bool UpgradeAllowed { get; set; }

	/// <summary>
	///     Index into <see cref="Items" /> at (or above) which a Quality meets this Profile's Cutoff, i.e. no further
	///     upgrade is wanted
	/// </summary>
	public int Cutoff { get; set; }

	/// <summary>
	///     Ordered list of Qualities of this profile, from lowest to highest wanted Quality
	/// </summary>
	public List<QualityProfileItem> Items { get; set; } = new();

	/// <summary>
	///     Score of each Custom Format by its Id, added to the score of a Release matching the Custom Format
	/// </summary>
	public Dictionary<int, int> FormatScores { get; set; } = new();

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }

	/// <summary>
	///     Checks if the given Quality already meets this Profile's Cutoff
	/// </summary>
	/// <param name="quality">The Quality to check</param>
	/// <returns>true if the Quality meets or exceeds the Cutoff</returns>
	public bool MeetsCutoff(QualityModel quality)
	{
		var index = IndexOf(quality.Resolution);

		return index >= 0 && index >= Cutoff;
	}

	/// <summary>
	///     Checks if a candidate Quality is an upgrade over the current Quality, honoring the order of
	///     <see cref="Items" /> and, for equal tiers, the <see cref="Revision" /> of both Qualities
	/// </summary>
	/// <param name="current">The currently held Quality</param>
	/// <param name="candidate">The candidate Quality to compare against</param>
	/// <returns>true if candidate is an upgrade over current</returns>
	public bool IsUpgrade(QualityModel current, QualityModel candidate)
	{
		if (!UpgradeAllowed || MeetsCutoff(current))
			return false;

		var candidateIndex = IndexOf(candidate.Resolution);

		if (candidateIndex < 0 || !Items[candidateIndex].Allowed)
			return false;

		var currentIndex = IndexOf(current.Resolution);

		if (candidateIndex > currentIndex)
			return true;

		return candidateIndex == currentIndex && candidate.Revision > current.Revision;
	}

	private int IndexOf(QualityResolutionModel quality)
		=> Items.FindIndex(i => i.Quality.Source == quality.Source && i.Quality.Resolution == quality.Resolution);
}
