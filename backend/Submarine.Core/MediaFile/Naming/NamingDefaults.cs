namespace Submarine.Core.MediaFile.Naming;

/// <summary>
///     Default naming templates
/// </summary>
public static class NamingDefaults
{
	/// <summary>
	///     Default template for Series episodes
	/// </summary>
	public const string SeriesFileName = "{Series Title} - S{Season:00}E{Episode:00} - {Episode Title}";

	/// <summary>
	///     Default template for Anime episodes
	/// </summary>
	public const string AnimeFileName = "{Series Title} - {Absolute:000} - {Episode Title}";

	/// <summary>
	///     Default template for Movies
	/// </summary>
	public const string MovieFileName = "{Movie Title} ({Year})";
}
