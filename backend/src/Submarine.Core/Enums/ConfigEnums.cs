namespace Submarine.Core.Enums;

/// <summary>
///     Color theme of the web UI.
/// </summary>
public enum Theme
{
	/// <summary>Follow the operating system preference.</summary>
	AUTO,

	/// <summary>Always light.</summary>
	LIGHT,

	/// <summary>Always dark.</summary>
	DARK
}

/// <summary>
///     How colons in release titles are replaced when renaming files.
/// </summary>
public enum ColonReplacement
{
	/// <summary>Remove the colon entirely.</summary>
	DELETE,

	/// <summary>Replace with a dash.</summary>
	DASH,

	/// <summary>Replace with a spaced dash.</summary>
	SPACE_DASH,

	/// <summary>Replace with a spaced dash surrounded by spaces.</summary>
	SPACE_DASH_SPACE,

	/// <summary>Use smart replacement depending on position.</summary>
	SMART
}

/// <summary>
///     How multi-episode files are formatted.
/// </summary>
public enum MultiEpisodeStyle
{
	/// <summary>S01E01-02-03</summary>
	EXTEND,

	/// <summary>S01E01.S01E02</summary>
	DUPLICATE,

	/// <summary>S01E01E02</summary>
	REPEAT,

	/// <summary>1x01x02</summary>
	SCENE,

	/// <summary>S01E01-03</summary>
	RANGE,

	/// <summary>S01E01-E03</summary>
	PREFIXED_RANGE
}

/// <summary>
///     How propers and repacks are treated during quality decisions.
/// </summary>
public enum DownloadPropersAndRepacks
{
	/// <summary>Prefer propers and repacks and allow upgrades to them.</summary>
	PREFER_AND_UPGRADE,

	/// <summary>Never upgrade to propers and repacks.</summary>
	DO_NOT_UPGRADE,

	/// <summary>Do not rank propers and repacks differently.</summary>
	DO_NOT_PREFER
}
