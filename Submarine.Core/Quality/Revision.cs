using System;

namespace Submarine.Core.Quality;

public record Revision(
		int Version = 1,
		bool IsRepack = false,
		bool IsProper = false,
		bool IsReal = false)
	: IComparable<Revision>
{
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

	public virtual bool Equals(Revision? other)
	{
		if (ReferenceEquals(null, other)) return false;
		if (ReferenceEquals(this, other)) return true;
		return Version == other.Version
		       && IsRepack == other.IsRepack
		       && IsProper == other.IsProper
		       && IsReal == other.IsReal;
	}

	public override int GetHashCode()
		=> HashCode.Combine(Version, IsRepack, IsProper, IsReal);

	public static bool operator >(Revision? left, Revision? right)
	{
		if (ReferenceEquals(null, left)) return false;
		if (ReferenceEquals(null, right)) return true;

		return left.CompareTo(right) > 0;
	}

	public static bool operator <(Revision? left, Revision? right)
	{
		if (ReferenceEquals(null, left)) return true;
		if (ReferenceEquals(null, right)) return false;

		return left.CompareTo(right) < 0;
	}

	public static bool operator >=(Revision? left, Revision? right)
	{
		if (ReferenceEquals(null, left)) return false;
		if (ReferenceEquals(null, right)) return true;

		return left.CompareTo(right) >= 0;
	}

	public static bool operator <=(Revision? left, Revision? right)
	{
		if (ReferenceEquals(null, left)) return true;
		if (ReferenceEquals(null, right)) return false;

		return left.CompareTo(right) <= 0;
	}
}
