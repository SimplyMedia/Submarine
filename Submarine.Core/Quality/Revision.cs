using System;

namespace Submarine.Core.Quality
{
	public record Revision(int Version = 1, bool IsRepack = false, bool IsProper = false) : IComparable<Revision>
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
	}
}
