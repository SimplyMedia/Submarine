using Submarine.Core.Entities;
using Submarine.Core.Enums;

namespace Submarine.Core.DecisionEngine;

/// <summary>
///     Orders download decisions: approved first, then by score, then by seeders, then by age (older first), then by
///     closeness to the quality definition's preferred size.
/// </summary>
public interface IReleaseComparer : IComparer<DownloadDecision>;

/// <summary>
///     Default release ordering.
/// </summary>
public sealed class ReleaseComparer : IReleaseComparer
{
	/// <inheritdoc />
	public int Compare(DownloadDecision? x, DownloadDecision? y)
	{
		if (ReferenceEquals(x, y))
		{
			return 0;
		}

		if (x is null)
		{
			return 1;
		}

		if (y is null)
		{
			return -1;
		}

		var approved = y.Approved.CompareTo(x.Approved);
		if (approved != 0)
		{
			return approved;
		}

		var score = y.Score.CompareTo(x.Score);
		if (score != 0)
		{
			return score;
		}

		var seeders = (y.Candidate.Info.Seeders ?? -1).CompareTo(x.Candidate.Info.Seeders ?? -1);
		if (seeders != 0)
		{
			return seeders;
		}

		var age = (y.Candidate.Info.Age ?? TimeSpan.Zero).CompareTo(x.Candidate.Info.Age ?? TimeSpan.Zero);
		if (age != 0)
		{
			return age;
		}

		return y.SizePreferenceKey.CompareTo(x.SizePreferenceKey);
	}
}
