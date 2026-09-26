using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Submarine.Infrastructure.Indexers.Cardigann;

/// <summary>
///     Parses human size strings like "1.4 GB", "512 MiB" or raw byte counts
/// </summary>
public static partial class CardigannSizeParser
{
	/// <summary>
	///     Parses a size value to bytes
	/// </summary>
	/// <param name="value">The size text</param>
	/// <returns>The size in bytes, or null when unparsable</returns>
	public static long? ParseBytes(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var trimmed = value.Trim().Replace("\u00a0", " ");
		if (long.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var bytes))
			return bytes;

		var match = SizeRegex().Match(trimmed);
		if (!match.Success)
			return null;

		var number = double.Parse(match.Groups["num"].Value.Replace(",", "."), CultureInfo.InvariantCulture);
		var unit = match.Groups["unit"].Value.ToUpperInvariant();
		var multiplier = unit[0] switch
		{
			'T' => 1L << 40,
			'G' => 1L << 30,
			'M' => 1L << 20,
			'K' => 1L << 10,
			'B' => 1,
			_ => 1
		};
		return (long)Math.Round(number * multiplier);
	}

	[GeneratedRegex(@"^(?<num>\d+(?:[.,]\d+)?)\s*(?<unit>[TGMK])?[Ii]?[Bb]?\s*$")]
	private static partial Regex SizeRegex();
}

/// <summary>
///     Parses relative time texts like "2 days ago", "1h 30m" or "8m"
/// </summary>
public static partial class CardigannTimeAgoParser
{
	/// <summary>
	///     Parses a relative time text into a utc date
	/// </summary>
	/// <param name="value">The relative time text</param>
	/// <param name="now">The reference point</param>
	/// <returns>The parsed date, or null when unparsable</returns>
	public static DateTime? Parse(string? value, DateTime now)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var total = TimeSpan.Zero;
		var matched = false;
		foreach (var match in UnitRegex().Matches(value).Cast<Match>())
		{
			if (!double.TryParse(match.Groups["num"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
				continue;

			total += match.Groups["unit"].Value.ToLowerInvariant() switch
			{
				"d" or "day" or "days" => TimeSpan.FromDays(amount),
				"w" or "wk" or "wks" or "week" or "weeks" => TimeSpan.FromDays(amount * 7),
				"mo" or "mon" or "month" or "months" => TimeSpan.FromDays(amount * 30),
				"y" or "yr" or "yrs" or "year" or "years" => TimeSpan.FromDays(amount * 365),
				"h" or "hr" or "hrs" or "hour" or "hours" => TimeSpan.FromHours(amount),
				"m" or "min" or "mins" or "minute" or "minutes" => TimeSpan.FromMinutes(amount),
				"s" or "sec" or "secs" or "second" or "seconds" => TimeSpan.FromSeconds(amount),
				_ => TimeSpan.Zero
			};
			matched = true;
		}

		return matched ? now - total : null;
	}

	/// <summary>
	///     Parses "yesterday", "today at 7:25am" or "2 days" style texts
	/// </summary>
	/// <param name="value">The text</param>
	/// <param name="now">The reference point</param>
	/// <returns>The parsed date, or null</returns>
	public static DateTime? ParseFuzzy(string? value, DateTime now)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var normalized = value.Trim().ToLowerInvariant();
		if (normalized.Contains("yesterday"))
			return now - TimeSpan.FromDays(1);
		if (normalized.Contains("today") || normalized.Contains("now"))
			return now;

		var clock = ClockRegex().Match(normalized);
		if (clock.Success)
		{
			var hour = int.Parse(clock.Groups["h"].Value, CultureInfo.InvariantCulture) % 12;
			if (clock.Groups["p"] is { Success: true } period && period.Value.StartsWith('p'))
				hour += 12;
			return now.Date.AddHours(hour).AddMinutes(int.Parse(clock.Groups["m"].Value, CultureInfo.InvariantCulture));
		}

		return Parse(value, now);
	}

	[GeneratedRegex(@"(?<num>\d+(?:\.\d+)?)\s*(?<unit>days|day|d|weeks|week|wks|wk|w|years|year|yrs|yr|y|months|month|mon|mo|hours|hour|hrs|hr|h|minutes|minute|mins|min|m|seconds|second|secs|sec|s)\b")]
	private static partial Regex UnitRegex();

	[GeneratedRegex(@"(?<h>\d{1,2}):(?<m>\d{2})\s*(?<p>[ap])\.?m\.?")]
	private static partial Regex ClockRegex();
}
