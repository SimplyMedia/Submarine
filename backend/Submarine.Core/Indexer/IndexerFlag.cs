namespace Submarine.Core.Indexer;

/// <summary>
///     Indexer specific flags of a <see cref="ReleaseInfo" />
/// </summary>
public enum IndexerFlag
{
	/// <summary>
	///     Download does not count towards the download volume (downloadvolumefactor 0)
	/// </summary>
	FREELEECH,

	/// <summary>
	///     Download counts half towards the download volume (downloadvolumefactor 0.5)
	/// </summary>
	HALFLEECH,

	/// <summary>
	///     Upload counts double towards the upload volume (uploadvolumefactor 2)
	/// </summary>
	DOUBLE_UPLOAD
}
