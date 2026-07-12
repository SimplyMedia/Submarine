using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Quality;

namespace Submarine.Core.Parser;

/// <summary>
///     Service which parses the Quality of a Release
/// </summary>
public class QualityParserService : IParser<QualityModel>
{
	private static readonly Regex SourceRegex = new(@"\b(?:
																(?<bluray>M?Blu[-_. ]?Ray|HD-?DVD|BDMux|BD(?!$)|UHD2?BD|BDISO|BD25|BD50|BR[-_. ]?DISK)|
																(?<webdl>WEB[-_. ]DL(?:mux)?|WEBDL|AmazonHD|AmazonSD|iTunesHD|MaxdomeHD|NetflixU?HD|WebHD|HBOMaxHD|DisneyHD|[. ]WEB[. ](?:[xh][ .]?26[45]|AVC|HEVC|DDP?5[. ]1)|[. ](?-i:WEB)$|\d+0p(?:[-. ]AMZN)?[-. ](?:Hybrid[-_. ]?)?WEB[-. ]|[-. ]WEB[-. ]\d{3,4}0p|\b\s\/\sWEB\s\/\s\b|(?:AMZN|NF|DP)[. -]WEB[. -](?!Rip))|
																(?<webrip>WebRip|Web-Rip|WEBMux)|
																(?<hdtv>HDTV)|
																(?<bdrip>BDRip|BDLight|HD[-_. ]?DVDRip|UHDBDRip)|
																(?<brrip>BRRip)|
																(?<dvdr>\d?x?M?DVD-?[R59])|
																(?<dvd>DVD(?!-R)|DVDRip|NTSC|PAL|xvidvd)|
																(?<dsr>WS[-_. ]DSR|DSR)|
																(?<regional>R[0-9]{1}|REGIONAL)|
																(?<scr>SCR|SCREENER|DVDSCR|DVDSCREENER)|
																(?<ts>TS[-_. ]|TELESYNCH?|HD-TS|HDTS|PDVD|TSRip|HDTSRip)|
																(?<tc>TC|TELECINE|HD-TC|HDTC)|
																(?<cam>CAMRIP|(?:NEW)?CAM|HD-?CAM(?:Rip)?|HQCAM)|
																(?<wp>WORKPRINT|WP)|
																(?<pdtv>PDTV)|
																(?<sdtv>SDTV)|
																(?<tvrip>TVRip)
																)(?:\b|$|[ .])",
		RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

	private static readonly Regex RawHDRegex = new(@"\b(?<rawhd>RawHD|Raw[-_. ]HD)\b",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex MPEG2Regex = new(@"\b(?<mpeg2>MPEG[-_. ]?2)\b");

	private static readonly Regex ProperRegex = new(@"\b(?<proper>proper)(?<version>[1-9])?\b",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex RepackRegex = new(@"\b(?<repack>repack|rerip)(?<version>[1-9])?\b",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex RealRegex = new(@"\b(?<real>real)\b",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex VersionRegex = new(
		@"\d[-._ ]?v(?<version>\d)[-._ ]|\[v(?<version>\d)\]|(?:480|576|720|1080|2160)p[._ ]v(?<version>\d)|\dv(?<version>\d)\b|[-_. ]v(?<version>\d)[-_. ]",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex ResolutionRegex = new(
		@"\b(?:(?<R360p>360p)|(?<R480p>480p|480i|640x480|848x480)|(?<R540p>540p)|(?<R576p>576p)|(?<R720p>720p|1280x720|960p)|(?<R1080p>1080p|1920x1080|1440p|FHD|1080i|4kto1080p)|(?<R2160p>2160p|3840x2160|4k[-_. ](?:UHD|HEVC|BD|H\.?265)|(?:UHD|HEVC|BD|H\.?265)[-_. ]4k))\b",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	//Handle cases where no resolution is in the release name (assume if UHD then 4k) or resolution is non-standard
	private static readonly Regex ImpliedResolutionRegex = new(@"\b(?<R2160p>UHD)\b|(?<R2160p>\[4K\])",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex CodecRegex = new(
		@"\b(?:(?<x264>x264)|(?<h264>h264)|(?<xvidhd>XvidHD)|(?<xvid>X-?vid)|(?<divx>divx))\b",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex OtherSourceRegex = new(@"(?<hdtv>HD[-_. ]?TV)|(?<sdtv>SD[-_. ]?TV)",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex AnimeBlurayRegex = new(@"bd(?:720|1080|2160)|(?<=[-_. (\[])bd(?=[-_. )\]])",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex AnimeWebDlRegex =
		new(@"\[WEB\]|\(WEB[ .]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex HighDefPdtvRegex =
		new(@"hr[-_. ]ws", RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex RemuxRegex = new(
		@"(?:[_. \[]|\d{4}p-|\bHybrid-)(?<remux>(?:(BD|UHD)[-_. ]?)?Remux)\b|(?<remux>(?:(BD|UHD)[-_. ]?)?Remux[_. ]\d{4}p)",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex HardcodedSubsRegex = new(
		@"\b((?<hcsub>(\w+(?<!SOFT|MULTI|HORRIBLE)SUBS?))|(?<hc>(HC|SUBBED)))\b",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex FullBlurayDiscRegex = new(@"\b(COMPLETE|ISO|BDISO|BD25|BD50|BR.?DISK)\b",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex FullBlurayCodecsRegex =
		new(@"[-. _](HEVC|AVC)[-. _]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private readonly ILogger<QualityParserService> _logger;

	/// <summary>
	///     Creates a new <see cref="QualityParserService" />
	/// </summary>
	/// <param name="logger">The Logger of this <see cref="QualityParserService" /></param>
	public QualityParserService(ILogger<QualityParserService> logger)
		=> _logger = logger;


	/// <summary>
	///     Parses the Quality of a Release
	/// </summary>
	/// <param name="input">The Release name</param>
	/// <returns>The QualityModel of that Release</returns>
	public QualityModel Parse(string input)
	{
		_logger.LogDebug("Trying to parse quality for {Input}", input);

		input = input.Trim();

		return ParseQualityName(input);
	}

	/// <summary>
	///     Parses whether a Release has Hardcoded Subs
	/// </summary>
	/// <param name="input">The Release name</param>
	/// <returns>Whether the Release has Hardcoded Subs</returns>
	public static bool ParseHardcodedSubs(string input)
		=> HardcodedSubsRegex.IsMatch(input);

	private QualityModel ParseQualityName(string name)
	{
		var normalizedName = name.Replace('_', ' ').Trim();

		var revision = ParseRevisions(normalizedName);

		return RawHDRegex.IsMatch(normalizedName)
			? new QualityModel(new QualityResolutionModel(QualitySource.RAW_HD, QualityResolution.R1080_P),
				revision)
			: new QualityModel(ParseResolutionModel(normalizedName), revision);
	}

	private QualityResolutionModel ParseResolutionModel(string normalizedName)
	{
		var resolution = ParseResolution(normalizedName);
		var sourceMatches = SourceRegex.Matches(normalizedName);
		var sourceMatch = sourceMatches.LastOrDefault();
		var codecRegex = CodecRegex.Match(normalizedName);
		var remuxMatch = RemuxRegex.IsMatch(normalizedName);
		var fullDiscMatch = FullBlurayDiscRegex.IsMatch(normalizedName);

		if (sourceMatch is { Success: true })
		{
			_logger.LogDebug("{Input} matched SourceRegex", normalizedName);

			if (sourceMatch.Groups["bluray"].Success)
			{
				_logger.LogDebug("{Input} matched SourceRegex with Bluray", normalizedName);

				if (codecRegex.Groups["xvid"].Success || codecRegex.Groups["divx"].Success)
				{
					_logger.LogDebug("{Input} matched Xvid/Divx in CodecRegex, setting resolution to 480p",
						normalizedName);
					return new QualityResolutionModel(QualitySource.BLURAY, QualityResolution.R480_P);
				}

				if (fullDiscMatch)
				{
					_logger.LogDebug("{Input} matched FullBlurayDiscRegex, setting source to bluray disc",
						normalizedName);

					return new QualityResolutionModel(QualitySource.BLURAY_DISK, resolution);
				}

				if (remuxMatch)
				{
					_logger.LogDebug("{Input} matched Remux", normalizedName);

					if (resolution == null)
						_logger.LogDebug("{Input} found no resolution, default to 1080p for Bluray Remux",
							normalizedName);

					return new QualityResolutionModel(QualitySource.BLURAY_REMUX,
						resolution ?? QualityResolution.R1080_P);
				}

				// Handle Edge cases where there is no indicator if its a full disc or not besides the codecs
				if (FullBlurayCodecsRegex.IsMatch(normalizedName))
				{
					_logger.LogDebug(
						"{Input} found no remux in name but AVC/HEVC as codec, setting source to bluray disc instead",
						normalizedName);

					return new QualityResolutionModel(QualitySource.BLURAY_DISK, resolution);
				}

				if (resolution == null)
					_logger.LogDebug("{Input} found no resolution, default to 720p for Bluray", normalizedName);

				return new QualityResolutionModel(QualitySource.BLURAY, resolution ?? QualityResolution.R720_P);
			}

			if (sourceMatch.Groups["webdl"].Success)
			{
				_logger.LogDebug("{Input} matched SourceRegex with WebDL", normalizedName);

				if (resolution == null)
					_logger.LogDebug("{Input} found no resolution, default to 720p for WebDL", normalizedName);

				return new QualityResolutionModel(QualitySource.WEB_DL, resolution ?? QualityResolution.R720_P);
			}

			if (sourceMatch.Groups["webrip"].Success)
			{
				_logger.LogDebug("{Input} matched SourceRegex with WebRip", normalizedName);

				if (resolution == null)
					_logger.LogDebug("{Input} found no resolution, default to 720p for WebRip", normalizedName);

				return new QualityResolutionModel(QualitySource.WEB_RIP, resolution ?? QualityResolution.R720_P);
			}

			if (sourceMatch.Groups["hdtv"].Success)
			{
				_logger.LogDebug("{Input} matched SourceRegex with HDTV", normalizedName);

				if (MPEG2Regex.IsMatch(normalizedName))
				{
					_logger.LogDebug("{Input} matched MPEG2Regex, setting QualitySource to RAWHD", normalizedName);
					return new QualityResolutionModel(QualitySource.RAW_HD);
				}

				if (resolution.HasValue)
					return new QualityResolutionModel(QualitySource.TV, resolution);
			}

			if (sourceMatch.Groups["bdrip"].Success || sourceMatch.Groups["brrip"].Success)
			{
				_logger.LogDebug("{Input} matched SourceRegex with Bluray", normalizedName);
				if (resolution.HasValue)
					return new QualityResolutionModel(QualitySource.BLURAY, resolution);
			}

			if (sourceMatch.Groups["dvd"].Success)
			{
				_logger.LogDebug("{Input} matched SourceRegex with DVD", normalizedName);
				return new QualityResolutionModel(QualitySource.DVD);
			}

			if (sourceMatch.Groups["cam"].Success)
			{
				_logger.LogDebug("{Input} matched SourceRegex with CAM", normalizedName);
				return new QualityResolutionModel(QualitySource.CAM, resolution);
			}

			if (sourceMatch.Groups["pdtv"].Success ||
			    sourceMatch.Groups["sdtv"].Success ||
			    sourceMatch.Groups["dsr"].Success ||
			    sourceMatch.Groups["tvrip"].Success)
			{
				_logger.LogDebug("{Input} matched SourceRegex with PDTV/SDTV/DSR/TVRIP", normalizedName);
				if (resolution == QualityResolution.R1080_P)
					return new QualityResolutionModel(QualitySource.TV, resolution);

				if (resolution == QualityResolution.R720_P || HighDefPdtvRegex.IsMatch(normalizedName))
					return new QualityResolutionModel(QualitySource.TV, QualityResolution.R720_P);

				return new QualityResolutionModel(QualitySource.TV, QualityResolution.R480_P);
			}
		}

		// Anime Bluray matching
		if (AnimeBlurayRegex.Match(normalizedName).Success)
		{
			_logger.LogDebug("{Input} matched Bluray with AnimeBlurayRegex", normalizedName);
			if (remuxMatch)
			{
				_logger.LogDebug("{Input} matched Remux", normalizedName);

				if (resolution == null)
					_logger.LogDebug("{Input} found no resolution, default to 1080p for Bluray Remux",
						normalizedName);

				return new QualityResolutionModel(QualitySource.BLURAY_REMUX, resolution ?? QualityResolution.R1080_P);
			}

			if (resolution == null)
				_logger.LogDebug("{Input} found no resolution, default to 720p for Bluray",
					normalizedName);

			return new QualityResolutionModel(QualitySource.BLURAY, resolution ?? QualityResolution.R720_P);
		}

		if (AnimeWebDlRegex.Match(normalizedName).Success)
		{
			_logger.LogDebug("{Input} matched Bluray with AnimeWebDlRegex", normalizedName);

			if (resolution == null)
				_logger.LogDebug("{Input} found no resolution, default to 720p for WebDL",
					normalizedName);

			return new QualityResolutionModel(QualitySource.WEB_DL, resolution ?? QualityResolution.R720_P);
		}

		if (remuxMatch)
		{
			_logger.LogDebug("{Input} matched Remux but no Source or Anime match", normalizedName);

			if (resolution == null)
				_logger.LogDebug("{Input} found no resolution, default to 1080p for Remux", normalizedName);

			return new QualityResolutionModel(QualitySource.BLURAY_REMUX, resolution ?? QualityResolution.R1080_P);
		}

		if (resolution.HasValue)
		{
			_logger.LogDebug("{Input} matched Resolution but no Source", normalizedName);

			return new QualityResolutionModel(QualitySource.UNKNOWN, resolution);
		}

		var match = OtherSourceRegex.Match(normalizedName);

		if (match.Groups["hdtv"].Success)
		{
			_logger.LogDebug("{Input} matched OtherSourceRegex with HDTV, default to 720p since no Resolution matched",
				normalizedName);

			return new QualityResolutionModel(QualitySource.TV, QualityResolution.R720_P);
		}

		_logger.LogDebug("{Input} matched nothing, unknown QualityResolution", normalizedName);

		return new QualityResolutionModel();
	}

	private QualityResolution? ParseResolution(string normalizedName)
	{
		var match = ResolutionRegex.Match(normalizedName);
		var matchImplied = ImpliedResolutionRegex.Match(normalizedName);

		if (!match.Success & !matchImplied.Success)
		{
			_logger.LogDebug("{Input} didn't match either Resolution regex or Implied Resolution Regex",
				normalizedName);
			return null;
		}

		if (match.Groups["R360p"].Success)
		{
			_logger.LogDebug("{Input} matched 360p Resolution regex", normalizedName);
			return QualityResolution.R360_P;
		}

		if (match.Groups["R480p"].Success)
		{
			_logger.LogDebug("{Input} matched 480p Resolution regex", normalizedName);
			return QualityResolution.R480_P;
		}

		if (match.Groups["R540p"].Success)
		{
			_logger.LogDebug("{Input} matched 540p Resolution regex", normalizedName);
			return QualityResolution.R540_P;
		}

		if (match.Groups["R576p"].Success)
		{
			_logger.LogDebug("{Input} matched 576p Resolution regex", normalizedName);
			return QualityResolution.R576_P;
		}

		if (match.Groups["R720p"].Success)
		{
			_logger.LogDebug("{Input} matched 720p Resolution regex", normalizedName);
			return QualityResolution.R720_P;
		}

		if (match.Groups["R1080p"].Success)
		{
			_logger.LogDebug("{Input} matched 1080p Resolution regex", normalizedName);
			return QualityResolution.R1080_P;
		}

		if (match.Groups["R2160p"].Success)
		{
			_logger.LogDebug("{Input} matched 2160p Resolution regex", normalizedName);
			return QualityResolution.R2160_P;
		}

		if (matchImplied.Groups["R2160p"].Success)
		{
			_logger.LogDebug("{Input} matched implicit 2160p (4K) regex", normalizedName);
			return QualityResolution.R2160_P;
		}

		_logger.LogDebug("{Input} didn't match any explicit or implicit resolution regex", normalizedName);

		return null;
	}

	private Revision ParseRevisions(string input)
	{
		var version = 1;
		var isRepack = false;
		var isProper = false;
		var isReal = false;

		var repackMatches = RepackRegex.Matches(input);
		var repackMatch = repackMatches.LastOrDefault();

		if (repackMatch is { Success: true })
		{
			isRepack = true;

			var versionGroup = repackMatch.Groups["version"];

			version = versionGroup.Success && int.TryParse(versionGroup.Value, out var repackVersion)
				? repackVersion + 1
				: version + 1;

			_logger.LogDebug("{Input} matched Repack regex, version {Version}", input, version);
		}

		var properMatches = ProperRegex.Matches(input);
		var properMatch = properMatches.LastOrDefault();

		if (properMatch is { Success: true })
		{
			isProper = true;

			var versionGroup = properMatch.Groups["version"];

			version = versionGroup.Success && int.TryParse(versionGroup.Value, out var properVersion)
				? properVersion + 1
				: version + 1;

			_logger.LogDebug("{Input} matched Proper regex, version {Version}", input, version);
		}

		if (RealRegex.IsMatch(input))
		{
			isReal = true;
			version += 1;

			_logger.LogDebug("{Input} matched REAL regex, version {Version}", input, version);
		}

		var versionRegexResult = VersionRegex.Match(input);

		if (versionRegexResult.Success)
		{
			version = int.Parse(versionRegexResult.Groups["version"].Value);

			_logger.LogDebug("{Input} matched version regex, version {Version}", input, version);
		}

		return new Revision(version, isRepack, isProper, isReal);
	}
}
