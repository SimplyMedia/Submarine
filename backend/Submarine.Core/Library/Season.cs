using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Submarine.Core.Library;

/// <summary>
///     A Season of a <see cref="Library.Series" />
/// </summary>
public class Season
{
	/// <summary>
	///     Id of the season
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Id of the series this season belongs to
	/// </summary>
	public int SeriesId { get; set; }

	/// <summary>
	///     Number of this season
	/// </summary>
	public int SeasonNumber { get; set; }

	/// <summary>
	///     Whether this season is monitored for new Episodes
	/// </summary>
	public bool Monitored { get; set; }
}
