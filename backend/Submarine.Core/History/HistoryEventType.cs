namespace Submarine.Core.History;

/// <summary>
///     The type of a <see cref="HistoryEvent" />
/// </summary>
public enum HistoryEventType
{
	/// <summary>A release was grabbed and sent to a download client</summary>
	GRABBED,

	/// <summary>A downloaded file was imported into the library</summary>
	IMPORTED,

	/// <summary>An imported file was renamed</summary>
	RENAMED,

	/// <summary>A file was deleted from the library</summary>
	DELETED,

	/// <summary>A grab or import failed</summary>
	FAILED
}
