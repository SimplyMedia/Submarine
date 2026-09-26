namespace Submarine.Core.Quality;

/// <summary>
///     Standardized Resolution enum
/// </summary>
public enum QualityResolution
{
	/// <summary>
	///     360p <see href="https://en.wikipedia.org/wiki/Low-definition_television" />
	/// </summary>
	R360_P,

	/// <summary>
	///     480p <see href="https://en.wikipedia.org/wiki/Enhanced-definition_television" />
	/// </summary>
	R480_P,

	/// <summary>
	///     540p <see href="https://en.wikipedia.org/wiki/Enhanced-definition_television" />
	/// </summary>
	R540_P,

	/// <summary>
	///     576p <see href="https://en.wikipedia.org/wiki/576p" />, quite uncommon but used on some Streaming Services
	/// </summary>
	R576_P,

	/// <summary>
	///     720p <see href="https://en.wikipedia.org/wiki/720p" />
	/// </summary>
	R720_P,

	/// <summary>
	///     1080p <see href="https://en.wikipedia.org/wiki/1080p" />
	/// </summary>
	R1080_P,

	/// <summary>
	///     2160p (also known as 4K) <see href="https://en.wikipedia.org/wiki/4K_resolution" />
	/// </summary>
	R2160_P
}
