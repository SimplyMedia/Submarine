using System;

namespace Submarine.Core.Quality;

/// <summary>
///     A Revision of a Quality
/// </summary>
/// <param name="Version">The version of this revision, default is 1</param>
/// <param name="IsRepack">If this revision is a repack</param>
/// <param name="IsProper">If this revision is a proper</param>
/// <param name="IsReal">If this revision is a real</param>
public record Revision(
		int Version = 1,
		bool IsRepack = false,
		bool IsProper = false,
		bool IsReal = false)
	: IComparable<Revision>
{
	/// <summary>
	///     Compares this Revision to another Revision
	/// </summary>
	/// <param name="other">The other Revision</param>
	/// <returns>1 if this Revision is higher, 0 if they are the same, -1 if the other one is higher</returns>
	public int CompareTo(Revision? other)
	{
		if (other == null)
			return 1;

		if (Version > other.Version)
			return 1;

		if (Version < other.Version)
			return -1;

		return 0;
	}

	/// <summary>
	///     If this Revision is equal to another Revision
	/// </summary>
	/// <param name="other">The other Revision</param>
	/// <returns>bool indicating if the two Revisions are identical</returns>
	public virtual bool Equals(Revision? other)
	{
		if (ReferenceEquals(null, other)) return false;
		if (ReferenceEquals(this, other)) return true;
		return Version == other.Version
		       && IsRepack == other.IsRepack
		       && IsProper == other.IsProper
		       && IsReal == other.IsReal;
	}


	/// <inheritdoc />
	public override int GetHashCode()
		=> HashCode.Combine(Version, IsRepack, IsProper, IsReal);

	/// <summary>
	///     Convenience method to compare two Revisions
	/// </summary>
	/// <param name="left">First Revision</param>
	/// <param name="right">Second Revision</param>
	/// <returns>bool</returns>
	public static bool operator >(Revision? left, Revision? right)
	{
		if (ReferenceEquals(null, left)) return false;
		if (ReferenceEquals(null, right)) return true;

		return left.CompareTo(right) > 0;
	}

	/// <summary>
	///     Convenience method to compare two Revisions
	/// </summary>
	/// <param name="left">First Revision</param>
	/// <param name="right">Second Revision</param>
	/// <returns>bool</returns>
	public static bool operator <(Revision? left, Revision? right)
	{
		if (ReferenceEquals(null, left)) return true;
		if (ReferenceEquals(null, right)) return false;

		return left.CompareTo(right) < 0;
	}

	/// <summary>
	///     Convenience method to compare two Revisions
	/// </summary>
	/// <param name="left">First Revision</param>
	/// <param name="right">Second Revision</param>
	/// <returns>bool</returns>
	public static bool operator >=(Revision? left, Revision? right)
	{
		if (ReferenceEquals(null, left)) return false;
		if (ReferenceEquals(null, right)) return true;

		return left.CompareTo(right) >= 0;
	}

	/// <summary>
	///     Convenience method to compare two Revisions
	/// </summary>
	/// <param name="left">First Revision</param>
	/// <param name="right">Second Revision</param>
	/// <returns>bool</returns>
	public static bool operator <=(Revision? left, Revision? right)
	{
		if (ReferenceEquals(null, left)) return true;
		if (ReferenceEquals(null, right)) return false;

		return left.CompareTo(right) <= 0;
	}
}
