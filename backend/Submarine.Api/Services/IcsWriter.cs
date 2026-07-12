using System.Text;
using Submarine.Api.Models.Response;

namespace Submarine.Api.Services;

/// <summary>
///     Builds an RFC 5545 VCALENDAR feed from calendar items
/// </summary>
public static class IcsWriter
{
	public static string Write(IReadOnlyList<CalendarItemResponse> items)
	{
		var builder = new StringBuilder();

		WriteLine(builder, "BEGIN:VCALENDAR");
		WriteLine(builder, "VERSION:2.0");
		WriteLine(builder, "PRODID:-//Submarine//Calendar//EN");
		WriteLine(builder, "CALSCALE:GREGORIAN");

		foreach (var item in items)
			WriteEvent(builder, item);

		WriteLine(builder, "END:VCALENDAR");

		return builder.ToString();
	}

	private static void WriteEvent(StringBuilder builder, CalendarItemResponse item)
	{
		var uid = item.Type == "episode" ? $"submarine-episode-{item.Id}" : $"submarine-movie-{item.Id}";
		var summary = item.EpisodeInfo != null
			? $"{item.Title} - S{item.EpisodeInfo.SeasonNumber:D2}E{item.EpisodeInfo.EpisodeNumber:D2} - {item.EpisodeInfo.EpisodeTitle}"
			: item.Title;

		WriteLine(builder, "BEGIN:VEVENT");
		WriteLine(builder, $"UID:{uid}");
		WriteLine(builder, $"DTSTAMP:{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}");
		WriteLine(builder, $"DTSTART;VALUE=DATE:{item.Date:yyyyMMdd}");
		WriteLine(builder, $"SUMMARY:{Escape(summary)}");
		WriteLine(builder, "END:VEVENT");
	}

	private static void WriteLine(StringBuilder builder, string line)
		=> builder.Append(line).Append("\r\n");

	// RFC 5545 escaping: backslash first so the following escapes aren't double-escaped
	private static string Escape(string value)
		=> value
			.Replace("\\", "\\\\")
			.Replace(",", "\\,")
			.Replace(";", "\\;")
			.Replace("\r\n", "\\n")
			.Replace("\n", "\\n");
}
