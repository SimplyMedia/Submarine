using Submarine.Core.Entities;

namespace Submarine.Core.Naming;

/// <summary>
///     Renders folder names for series and movies from the naming configuration templates.
/// </summary>
public interface IFolderNameRenderer
{
	/// <summary>
	///     Renders the folder name of a series.
	/// </summary>
	string RenderSeriesFolder(NamingConfig naming, Series series);

	/// <summary>
	///     Renders the folder name of one season of a series.
	/// </summary>
	string RenderSeasonFolder(NamingConfig naming, Series series, int seasonNumber);

	/// <summary>
	///     Renders the folder name of a movie.
	/// </summary>
	string RenderMovieFolder(NamingConfig naming, Movie movie);
}
