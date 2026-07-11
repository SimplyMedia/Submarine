using Submarine.Core.Quality.Attributes;

namespace Submarine.Core.Quality;

/// <summary>
///     Sources of a Release Quality
/// </summary>
public enum QualitySource
{
	/// <summary>
	///     The source is unknown
	/// </summary>
	UNKNOWN,

	/// <summary>
	///     The source is a Cam-rip <see href="https://en.wikipedia.org/wiki/Cam_(bootleg)" />
	/// </summary>
	CAM,

	/// <summary>
	///     The source is recording from a TV
	/// </summary>
	[Resolution(QualityResolution.R480_P, QualityResolution.R720_P, QualityResolution.R1080_P,
		QualityResolution.R2160_P)]
	TV,

	/// <summary>
	///     The source is a DVD
	/// </summary>
	DVD,

	/// <summary>
	///     The source is a raw and (mostly) uncompressed HD recording
	/// </summary>
	RAW_HD,

	/// <summary>
	///     The source is a WEBRip, which is sourced from a Streaming Service but not 100% untouched like a WebDL (can be
	///     re-encoded or transcode from 4k -> 1080p for better quality, very depending on the release group)
	/// </summary>
	[DisplayName("WEBRip")]
	[Resolution(QualityResolution.R480_P, QualityResolution.R720_P, QualityResolution.R1080_P,
		QualityResolution.R2160_P)]
	WEB_RIP,

	/// <summary>
	///     The source is a WebDL, which is sourced from a Streaming Service and released as in (almost) untouched quality
	/// </summary>
	[DisplayName("WebDL")]
	[Resolution(QualityResolution.R480_P, QualityResolution.R720_P, QualityResolution.R1080_P,
		QualityResolution.R2160_P)]
	WEB_DL,

	/// <summary>
	///     The source is a BluRay, which is sourced from a BluRay Disc but encoded
	/// </summary>
	[DisplayName("BluRay")]
	[Resolution(QualityResolution.R480_P, QualityResolution.R576_P, QualityResolution.R720_P, QualityResolution.R1080_P,
		QualityResolution.R2160_P)]
	BLURAY,

	/// <summary>
	///     The source is a BluRay Remux, which is sourced from a BluRay Disc but does not encode the video
	/// </summary>
	[DisplayName("BluRay Remux")]
	[Resolution(QualityResolution.R720_P, QualityResolution.R1080_P, QualityResolution.R2160_P)]
	BLURAY_REMUX,

	/// <summary>
	///     The source is an untouched BluRay Disk in its raw format (M2TS or ISO)
	/// </summary>
	[DisplayName("BluRay Disc")]
	[Resolution(QualityResolution.R720_P, QualityResolution.R1080_P, QualityResolution.R2160_P)]
	BLURAY_DISK
}
