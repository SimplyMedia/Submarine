namespace Submarine.Core.MediaFile.Naming;

/// <summary>
///     How multiple episodes sharing a single release should be rendered in a file name
/// </summary>
public enum MultiEpisodeStyle
{
	/// <summary>Each episode listed in full, e.g. "S01E01-02-03"</summary>
	EXTEND,

	/// <summary>Each episode listed with its own season/episode marker separated by a dot, e.g. "S01E01.S01E02"</summary>
	DUPLICATE,

	/// <summary>Each episode marker repeated after the season, e.g. "S01E01E02E03"</summary>
	REPEAT,

	/// <summary>Scene style numbering, e.g. "1x01x02"</summary>
	SCENE,

	/// <summary>A contiguous range, e.g. "S01E01-03"</summary>
	RANGE,

	/// <summary>A range with the episode marker repeated, e.g. "S01E01-E03"</summary>
	PREFIXED_RANGE
}
