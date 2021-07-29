using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Quality;

namespace Submarine.Core.Parser
{
	public class QualityParserService
	{
		private readonly ILogger<QualityParserService> _logger;

		public QualityParserService(ILogger<QualityParserService> logger) 
			=> _logger = logger;

		private static readonly Regex SourceRegex = new(@"\b(?:
                                                                (?<bluray>BluRay|Blu-Ray|HD-?DVD|BDMux|BD(?!$))|
                                                                (?<webdl>WEB[-_. ]DL|WEBDL|AmazonHD|iTunesHD|MaxdomeHD|NetflixU?HD|WebHD|[. ]WEB[. ](?:[xh]26[45]|DDP?5[. ]1)|[. ](?-i:WEB)$|\d+0p(?:[-. ]AMZN)?[-. ]WEB[-. ]|WEB-DLMux|\b\s\/\sWEB\s\/\s\b|AMZN[. ]WEB[. ])|
                                                                (?<webrip>WebRip|Web-Rip|WEBMux)|
                                                                (?<hdtv>HDTV)|
                                                                (?<bdrip>BDRip)|
                                                                (?<brrip>BRRip)|
																(?<dvdr>DVD-R|DVDR|DVD5|DVD9)|
                                                                (?<dvd>DVD|DVDRip|NTSC|PAL|xvidvd)|
                                                                (?<dsr>WS[-_. ]DSR|DSR)|	
                                                                (?<regional>R[0-9]{1}|REGIONAL)|
                                                                (?<scr>SCR|SCREENER|DVDSCR|DVDSCREENER)|
                                                                (?<ts>TS[-_. ]|TELESYNC|HD-TS|HDTS|PDVD|TSRip|HDTSRip)|
                                                                (?<tc>TC|TELECINE|HD-TC|HDTC)|
                                                                (?<cam>CAMRIP|CAM|HDCAM|HD-CAM)|
                                                                (?<wp>WORKPRINT|WP)|
                                                                (?<pdtv>PDTV)|
                                                                (?<sdtv>SDTV)|
                                                                (?<tvrip>TVRip)
                                                                )(?:\b|$|[ .])",
                                                                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex RawHDRegex = new(@"\b(?<rawhd>RawHD|Raw[-_. ]HD)\b",
                                                                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex MPEG2Regex = new(@"\b(?<mpeg2>MPEG[-_. ]?2)\b");

        private static readonly Regex ProperRegex = new(@"\b(?<proper>proper)\b",
                                                                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RepackRegex = new(@"\b(?<repack>repack|rerip)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex VersionRegex = new(@"\dv(?<version>\d)\b|\[v(?<version>\d)\]|[-_. ]v(?<version>\d)[-_. ]",
                                                                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ResolutionRegex = new(@"\b(?:(?<R360p>360p)|(?<R480p>480p|640x480|848x480)|(?<R540p>540p)|(?<R576p>576p)|(?<R720p>720p|1280x720)|(?<R1080p>1080p|1920x1080|1440p|FHD|1080i|4kto1080p)|(?<R2160p>2160p|4k[-_. ](?:UHD|HEVC|BD)|(?:UHD|HEVC|BD)[-_. ]4k))\b",

                                                                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        //Handle cases where no resolution is in the release name; assume if UHD then 4k
        private static readonly Regex ImpliedResolutionRegex = new(@"\b(?<R2160p>UHD)\b",
                                                                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex CodecRegex = new(@"\b(?:(?<x264>x264)|(?<h264>h264)|(?<xvidhd>XvidHD)|(?<xvid>Xvid)|(?<divx>divx))\b",
                                                                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex OtherSourceRegex = new(@"(?<hdtv>HD[-_. ]?TV)|(?<sdtv>SD[-_. ]?TV)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex AnimeBlurayRegex = new(@"bd(?:720|1080|2160)|(?<=[-_. (\[])bd(?=[-_. )\]])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex AnimeWebDlRegex = new(@"\[WEB\]|\(WEB[ .]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex HighDefPdtvRegex = new(@"hr[-_. ]ws", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RemuxRegex = new(@"\b(?<remux>(BD)?[-_. ]?Remux)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);


		public QualityModel ParseQuality(string input)
		{
			_logger.LogDebug($"Trying to parse quality for {input}");

			input = input.Trim();

			var result = ParseQualityName(input);

			return result;
		}

		private static QualityModel ParseQualityName(string name)
		{
			QualityResolutionModel? qualityResolution;
			
			var normalizedName = name.Replace('_', ' ').Trim();
			
			var revision = ParseRevisions(normalizedName);
			
			if (RawHDRegex.IsMatch(normalizedName))
			{
				qualityResolution = new QualityResolutionModel(QualitySource.RAW_HD, QualityResolution.R1080_P);

				return new QualityModel(qualityResolution, revision);
			}

			var sourceMatches = SourceRegex.Matches(normalizedName);
			var sourceMatch = sourceMatches.LastOrDefault();
			var resolution = ParseResolution(normalizedName);
			var codecRegex = CodecRegex.Match(normalizedName);
			var remuxMatch = RemuxRegex.IsMatch(normalizedName);

			if (sourceMatch is { Success: true })
			{
				if (sourceMatch.Groups["bluray"].Success)
				{
					if (codecRegex.Groups["xvid"].Success || codecRegex.Groups["divx"].Success)
					{
						qualityResolution = new QualityResolutionModel(QualitySource.BLURAY, QualityResolution.R480_P);
						return new QualityModel(qualityResolution, revision);
					}

					if (resolution != QualityResolution.UNKNOWN)
					{
						qualityResolution = new QualityResolutionModel(remuxMatch ? QualitySource.BLURAY_REMUX : QualitySource.BLURAY, resolution);
						return new QualityModel(qualityResolution, revision);
					}

					// Assume if only Remux matches that its 1080p 
					if (remuxMatch) {
							qualityResolution = new QualityResolutionModel(QualitySource.BLURAY_REMUX, QualityResolution.R1080_P);
							return new QualityModel(qualityResolution, revision);
					}

					qualityResolution = new QualityResolutionModel(QualitySource.BLURAY, QualityResolution.R720_P);
					return new QualityModel(qualityResolution, revision);
				}
				
				if (sourceMatch.Groups["webdl"].Success)
				{
					if (resolution != QualityResolution.UNKNOWN)
					{
						qualityResolution = new QualityResolutionModel(QualitySource.WEB_DL, resolution);
						return new QualityModel(qualityResolution, revision);
					}
					
					qualityResolution = new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R720_P);
					return new QualityModel(qualityResolution, revision);
				}
				
				if (sourceMatch.Groups["webrip"].Success)
				{
					if (resolution != QualityResolution.UNKNOWN)
					{
						qualityResolution = new QualityResolutionModel(QualitySource.WEB_RIP, resolution);
						return new QualityModel(qualityResolution, revision);
					}
					
					qualityResolution = new QualityResolutionModel(QualitySource.WEB_RIP, QualityResolution.R720_P);
					return new QualityModel(qualityResolution, revision);
				}
				
				if (sourceMatch.Groups["hdtv"].Success)
				{
					if (MPEG2Regex.IsMatch(normalizedName))
					{
						qualityResolution = new QualityResolutionModel(QualitySource.RAW_HD);
						return new QualityModel(qualityResolution, revision);
					}

					if (resolution != QualityResolution.UNKNOWN)
					{
						qualityResolution = new QualityResolutionModel(QualitySource.TV, resolution);
						return new QualityModel(qualityResolution, revision);
					}
				}

				if (sourceMatch.Groups["bdrip"].Success ||
				    sourceMatch.Groups["brrip"].Success)
				{
					if (resolution != QualityResolution.UNKNOWN)
					{
						qualityResolution = new QualityResolutionModel(QualitySource.BLURAY, resolution);
						return new QualityModel(qualityResolution, revision);
					}
				}
				
				if (sourceMatch.Groups["dvd"].Success)
				{
					qualityResolution = new QualityResolutionModel(QualitySource.DVD);
					return new QualityModel(qualityResolution, revision);
				}

				if (sourceMatch.Groups["pdtv"].Success ||
				    sourceMatch.Groups["sdtv"].Success ||
				    sourceMatch.Groups["dsr"].Success ||
				    sourceMatch.Groups["tvrip"].Success)
				{
					if (resolution == QualityResolution.R1080_P)
					{
						qualityResolution = new QualityResolutionModel(QualitySource.TV, QualityResolution.R1080_P);
						return new QualityModel(qualityResolution, revision);
					}

					if (resolution == QualityResolution.R720_P || HighDefPdtvRegex.IsMatch(normalizedName))
					{
						qualityResolution = new QualityResolutionModel(QualitySource.TV, QualityResolution.R720_P);
						return new QualityModel(qualityResolution, revision);
					}

					qualityResolution = new QualityResolutionModel(QualitySource.TV, QualityResolution.R480_P);
					return new QualityModel(qualityResolution, revision);
				}
			}
			
			// Anime Bluray matching
			if (AnimeBlurayRegex.Match(normalizedName).Success)
			{
				if (resolution != QualityResolution.UNKNOWN)
				{
					qualityResolution = new QualityResolutionModel(remuxMatch ? QualitySource.BLURAY_REMUX : QualitySource.BLURAY, resolution);
					return new QualityModel(qualityResolution, revision);
				}

				// Assume if only Remux matches that its 1080p 
				if (remuxMatch) {
					qualityResolution = new QualityResolutionModel(QualitySource.BLURAY_REMUX, QualityResolution.R1080_P);
					return new QualityModel(qualityResolution, revision);
				}

				qualityResolution = new QualityResolutionModel(QualitySource.BLURAY, QualityResolution.R720_P);
				return new QualityModel(qualityResolution, revision);
			}
			
			if (AnimeWebDlRegex.Match(normalizedName).Success)
			{
				if (resolution != QualityResolution.UNKNOWN)
				{
					qualityResolution = new QualityResolutionModel(QualitySource.WEB_DL, resolution);
					return new QualityModel(qualityResolution, revision);
				}

				qualityResolution = new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R720_P);
				return new QualityModel(qualityResolution, revision);
			}

			if (resolution != QualityResolution.UNKNOWN)
				return new QualityModel(new QualityResolutionModel(QualitySource.TV, resolution), revision);

			var otherSourceMatch = OtherSourceMatch(normalizedName);

			return new QualityModel(otherSourceMatch, revision);
		}

		private static QualityResolution ParseResolution(string normalizedName)
		{
			var match = ResolutionRegex.Match(normalizedName);

			var matchImplied = ImpliedResolutionRegex.Match(normalizedName);

			if (!match.Success & !matchImplied.Success) return QualityResolution.UNKNOWN;
			if (match.Groups["R360p"].Success) return QualityResolution.R360_P;
			if (match.Groups["R480p"].Success) return QualityResolution.R480_P;
			if (match.Groups["R540p"].Success) return QualityResolution.R540_P;
			if (match.Groups["R576p"].Success) return QualityResolution.R576_P;
			if (match.Groups["R720p"].Success) return QualityResolution.R720_P;
			if (match.Groups["R1080p"].Success) return QualityResolution.R1080_P;
			if (match.Groups["R2160p"].Success) return QualityResolution.R2160_P;

			return matchImplied.Groups["R2160p"].Success ? QualityResolution.R2160_P : QualityResolution.UNKNOWN;
		}

		private static Revision ParseRevisions(string name)
		{
			var version = 1;
			var isRepack = false;
			
			if (ProperRegex.IsMatch(name)) 
				version = 2;

			if (RepackRegex.IsMatch(name))
			{
				version = 2;
				isRepack = true;
			}

			var versionRegexResult = VersionRegex.Match(name);

			if (versionRegexResult.Success) 
				version = int.Parse(versionRegexResult.Groups["version"].Value);

			return new Revision(version, isRepack);
		}
		
		private static QualityResolutionModel OtherSourceMatch(string name)
		{
			var match = OtherSourceRegex.Match(name);

			if (!match.Success) return new QualityResolutionModel(QualitySource.UNKNOWN);
			if (match.Groups["sdtv"].Success) return new QualityResolutionModel(QualitySource.TV, QualityResolution.R480_P);
			return match.Groups["hdtv"].Success 
				? new QualityResolutionModel(QualitySource.TV, QualityResolution.R720_P) 
				: new QualityResolutionModel(QualitySource.UNKNOWN);
		}
	}
}