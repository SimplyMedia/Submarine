namespace Submarine.Core.Entities;

/// <summary>
///     A season of a series. Season 0 holds specials.
/// </summary>
public sealed class Season : Entity
{
	/// <summary>Id of the owning series.</summary>
	public int SeriesId { get; set; }

	/// <summary>Season number, 0 for specials.</summary>
	public int SeasonNumber { get; set; }

	/// <summary>Whether the season is monitored.</summary>
	public bool Monitored { get; set; } = true;

	/// <summary>Owning series.</summary>
	public Series Series { get; set; } = null!;
}
