namespace Submarine.Core.Release;

/// <summary>
/// The Type of a Series Release
/// </summary>
public enum SeriesReleaseType
{
	/// <summary>
	/// Release is an episode of a Series
	/// </summary>
	EPISODE,
	
	/// <summary>
	/// Release is a special episode of a Series 
	/// </summary>
	SPECIAL,
	
	/// <summary>
	/// Release is a full season of a Series
	/// </summary>
	FULL_SEASON,
	
	/// <summary>
	/// Release is a partial season of this Series
	/// </summary>
	PARTIAL_SEASON,
	
	/// <summary>
	/// Release is containing multiple seasons of this Series
	/// </summary>
	MULTI_SEASON
}
