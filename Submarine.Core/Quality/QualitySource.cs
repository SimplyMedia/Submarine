using Submarine.Core.Quality.Attributes;

namespace Submarine.Core.Quality;

public enum QualitySource
{
	CAM,

	[Resolution(QualityResolution.R480_P, QualityResolution.R720_P, QualityResolution.R1080_P,
		QualityResolution.R2160_P)]
	TV,

	DVD,

	RAW_HD,

	[DisplayName("WEBRip")]
	[Resolution(QualityResolution.R480_P, QualityResolution.R720_P, QualityResolution.R1080_P,
		QualityResolution.R2160_P)]
	WEB_RIP,

	[DisplayName("WebDL")]
	[Resolution(QualityResolution.R480_P, QualityResolution.R720_P, QualityResolution.R1080_P,
		QualityResolution.R2160_P)]
	WEB_DL,

	[DisplayName("BluRay")]
	[Resolution(QualityResolution.R480_P, QualityResolution.R576_P, QualityResolution.R720_P, QualityResolution.R1080_P,
		QualityResolution.R2160_P)]
	BLURAY,

	[DisplayName("BluRay Remux")]
	[Resolution(QualityResolution.R720_P, QualityResolution.R1080_P, QualityResolution.R2160_P)]
	BLURAY_REMUX,

	[DisplayName("BluRay Disc")]
	[Resolution(QualityResolution.R720_P, QualityResolution.R1080_P, QualityResolution.R2160_P)]
	BLURAY_DISK
}
