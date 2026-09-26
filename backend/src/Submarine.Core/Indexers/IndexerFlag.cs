namespace Submarine.Core.Indexers;

/// <summary>
///     Indexer specific flags of a release
/// </summary>
public enum IndexerFlag
{
	/// <summary>
	///     Download does not count towards the download volume
	/// </summary>
	FREELEECH,

	/// <summary>
	///     Download counts half towards the download volume
	/// </summary>
	HALFLEECH,

	/// <summary>
	///     Upload counts double towards the upload volume
	/// </summary>
	DOUBLE_UPLOAD,

	/// <summary>
	///     Release was uploaded by the internal release group of the indexer
	/// </summary>
	INTERNAL,

	/// <summary>
	///     Release is a scene release
	/// </summary>
	SCENE,

	/// <summary>
	///     Release is exclusive to the indexer
	/// </summary>
	EXCLUSIVE,

	/// <summary>
	///     Download counts a quarter towards the download volume
	/// </summary>
	G_FREELEECH
}
