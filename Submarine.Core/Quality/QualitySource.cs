using System.Collections.Generic;
using Submarine.Core.Quality.Attributes;

namespace Submarine.Core.Quality
{
	public enum QualitySource
	{
		UNKNOWN,
		
		CAM,
		
		[Resolution(QualityResolution.R480_P, QualityResolution.R720_P, QualityResolution.R1080_P, QualityResolution.R2160_P)]
		TV,
		
		DVD,
		
		RAW_HD,
		
		[Name("WEBRip")]
		[Resolution(QualityResolution.R480_P, QualityResolution.R720_P, QualityResolution.R1080_P, QualityResolution.R2160_P)]
		WEB_RIP,
		
		[Name("WebDL")]
		[Resolution(QualityResolution.R480_P, QualityResolution.R720_P, QualityResolution.R1080_P, QualityResolution.R2160_P)]
		WEB_DL,

		[Name("BluRay")]
		[Resolution(QualityResolution.R480_P, Quality.QualityResolution.R576_P, QualityResolution.R720_P, QualityResolution.R1080_P, QualityResolution.R2160_P)]
		BLURAY,
		
		[Name("BluRay Remux")]
		[Resolution(QualityResolution.R1080_P, QualityResolution.R2160_P)]
		BLURAY_REMUX,
		
		[Name("BluRay Disc")]
		[Resolution(QualityResolution.R1080_P, QualityResolution.R2160_P)]
		BLURAY_DISK
	}
}