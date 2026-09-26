using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Submarine.Infrastructure.Indexers.Cardigann;

/// <summary>
///     Parses Jackett style date format tokens like "ddd, dd MMM yyyy HH:mm:ss zzz" or "htt MMM. d"
/// </summary>
public static partial class CardigannDateParser
{
	/// <summary>
	///     Parses a value with the given token format, missing parts default to now
	/// </summary>
	/// <param name="value">The date text</param>
	/// <param name="format">The token format</param>
	/// <param name="now">The reference point for missing parts</param>
	/// <returns>The parsed utc date, or null when the value does not match</returns>
	public static DateTime? Parse(string value, string format, DateTime now)
	{
		var pattern = BuildRegex(format);
		var match = pattern.Match(value.Trim());
		if (!match.Success)
			return null;

		var year = Group(match, "yyyy") is { } yyyy
			? int.Parse(yyyy, CultureInfo.InvariantCulture)
			: Group(match, "yy") is { } yy
				? 2000 + int.Parse(yy, CultureInfo.InvariantCulture)
				: now.Year;
		var month = Group(match, "MMM") is { } mmm ? (int?)MonthNumber(mmm) : null;
		month ??= Group(match, "MM") is { } mm ? int.Parse(mm, CultureInfo.InvariantCulture) : now.Month;
		var day = Group(match, "dd") is { } d ? int.Parse(d, CultureInfo.InvariantCulture) : now.Day;
		var hour = 0;
		var minute = 0;
		var second = 0;

		if (Group(match, "htt") is { } htt)
		{
			// hour immediately followed by am or pm, e.g. 7am
			var httMatch = HttRegex().Match(htt);
			hour = httMatch.Groups["h"].Value.Length > 0 ? int.Parse(httMatch.Groups["h"].Value, CultureInfo.InvariantCulture) % 12 : hour;
			if (httMatch.Groups["p"].Value.Equals("pm", StringComparison.OrdinalIgnoreCase))
				hour += 12;
		}
		else
		{
			if (Group(match, "HH") is { } hh24)
				hour = int.Parse(hh24, CultureInfo.InvariantCulture);
			else if (Group(match, "H") is { } h24)
				hour = int.Parse(h24, CultureInfo.InvariantCulture);
			else if (Group(match, "hh") is { } hh12)
				hour = int.Parse(hh12, CultureInfo.InvariantCulture) % 12;
			else if (Group(match, "h") is { } h12)
				hour = int.Parse(h12, CultureInfo.InvariantCulture) % 12;

			if (Group(match, "tt") is { } tt && tt.Equals("pm", StringComparison.OrdinalIgnoreCase))
				hour += 12;
		}

		if (Group(match, "mm") is { } minuteText)
			minute = int.Parse(minuteText, CultureInfo.InvariantCulture);
		if (Group(match, "ss") is { } secondText)
			second = int.Parse(secondText, CultureInfo.InvariantCulture);

		DateTime local;
		try
		{
			local = new DateTime(year, month ?? now.Month, day, hour, minute, second, DateTimeKind.Unspecified);
		}
		catch (ArgumentOutOfRangeException)
		{
			return null;
		}

		var offsetText = Group(match, "zzz") ?? Group(match, "z");
		if (offsetText is null)
			return DateTime.SpecifyKind(local, DateTimeKind.Utc);

		var offset = ParseOffset(offsetText);
		if (offset is null)
			return null;

		return DateTime.SpecifyKind(local - offset.Value, DateTimeKind.Utc);
	}

	private static string? Group(Match match, string name)
		=> match.Groups[name] is { Success: true, Value.Length: > 0 } group_ ? group_.Value : null;

	private static int MonthNumber(string text)
	{
		var normalized = text.TrimEnd('.').ToLowerInvariant();
		return normalized switch
		{
			"jan" or "january" => 1,
			"feb" or "february" => 2,
			"mar" or "march" => 3,
			"apr" or "april" => 4,
			"may" => 5,
			"jun" or "june" => 6,
			"jul" or "july" => 7,
			"aug" or "august" => 8,
			"sep" or "sept" or "september" => 9,
			"oct" or "october" => 10,
			"nov" or "november" => 11,
			"dec" or "december" => 12,
			_ => throw new FormatException($"Unknown month {text}")
		};
	}

	private static TimeSpan? ParseOffset(string text)
	{
		if (text is "Z" or "z")
			return TimeSpan.Zero;

		var match = OffsetRegex().Match(text);
		if (!match.Success)
			return null;

		var sign = match.Groups["sign"].Value == "-" ? -1 : 1;
		var hours = int.Parse(match.Groups["oh"].Value, CultureInfo.InvariantCulture);
		var minutes = match.Groups["om"].Success ? int.Parse(match.Groups["om"].Value, CultureInfo.InvariantCulture) : 0;
		return new TimeSpan(hours * sign, minutes * sign, 0);
	}

	private static Regex BuildRegex(string format)
	{
		var builder = new StringBuilder("^");
		for (var index = 0; index < format.Length;)
		{
			var remaining = format[index..];
			if (remaining.StartsWith("ddd", StringComparison.Ordinal))
			{
				builder.Append(@"(?:[A-Za-z]+,?\s*)?");
				index += 3;
			}
			else if (remaining.StartsWith("yyyy", StringComparison.Ordinal))
			{
				builder.Append(@"(?<yyyy>\d{4})");
				index += 4;
			}
			else if (remaining.StartsWith("htt", StringComparison.Ordinal))
			{
				builder.Append(@"(?<htt>\s*\d{1,2}\s*[AaPp][Mm])");
				index += 3;
			}
			else if (remaining.StartsWith("zzz", StringComparison.Ordinal))
			{
				builder.Append(@"(?<zzz>(?:[+-]\d{2}:?\d{2})|[Zz])");
				index += 3;
			}
			else if (remaining.StartsWith("zz", StringComparison.Ordinal))
			{
				builder.Append(@"(?<z>[+-]\d{2}:?\d{2}?|[Zz])");
				index += 2;
			}
			else if (remaining.StartsWith('z'))
			{
				builder.Append(@"(?<z>[+-]\d{2}:?\d{2}?|[Zz])");
				index += 1;
			}
			else if (remaining.StartsWith("MMM", StringComparison.Ordinal))
			{
				builder.Append(@"(?<MMM>[A-Za-z]{3,})\.?");
				index += remaining.Length > 3 && remaining[3] == '.' ? 4 : 3;
			}
			else if (remaining.StartsWith("MM", StringComparison.Ordinal))
			{
				builder.Append(@"(?<MM>\d{2})");
				index += 2;
			}
			else if (remaining.StartsWith('M'))
			{
				builder.Append(@"(?<MM>\d{1,2})");
				index += 1;
			}
			else if (remaining.StartsWith("dd", StringComparison.Ordinal))
			{
				builder.Append(@"(?<dd>\d{1,2})");
				index += 2;
			}
			else if (remaining.StartsWith('d'))
			{
				builder.Append(@"(?<dd>\d{1,2})");
				index += 1;
			}
			else if (remaining.StartsWith("yy", StringComparison.Ordinal))
			{
				builder.Append(@"(?<yy>\d{2})");
				index += 2;
			}
			else if (remaining.StartsWith("HH", StringComparison.Ordinal))
			{
				builder.Append(@"(?<HH>\d{2})");
				index += 2;
			}
			else if (remaining.StartsWith('H'))
			{
				builder.Append(@"(?<H>\d{1,2})");
				index += 1;
			}
			else if (remaining.StartsWith("hh", StringComparison.Ordinal))
			{
				builder.Append(@"(?<hh>\d{1,2})");
				index += 2;
			}
			else if (remaining.StartsWith('h'))
			{
				builder.Append(@"(?<h>\d{1,2})");
				index += 1;
			}
			else if (remaining.StartsWith("mm", StringComparison.Ordinal))
			{
				builder.Append(@"(?<mm>\d{2})");
				index += 2;
			}
			else if (remaining.StartsWith('m'))
			{
				builder.Append(@"(?<mm>\d{1,2})");
				index += 1;
			}
			else if (remaining.StartsWith("ss", StringComparison.Ordinal))
			{
				builder.Append(@"(?<ss>\d{2})");
				index += 2;
			}
			else if (remaining.StartsWith("tt", StringComparison.Ordinal))
			{
				builder.Append(@"(?<tt>[AaPp][Mm])");
				index += 2;
			}
			else
			{
				builder.Append(Regex.Escape(format[index..(index + 1)]));
				index += 1;
			}
		}

		builder.Append(@"\s*.*$");
		return new Regex(builder.ToString(), RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(2));
	}

	[GeneratedRegex(@"^(?<h>\d{1,2})\s*(?<p>[AaPp][Mm])$")]
	private static partial Regex HttRegex();

	[GeneratedRegex(@"^(?<sign>[+-])(?<oh>\d{2}):?(?<om>\d{2})?$")]
	private static partial Regex OffsetRegex();
}
