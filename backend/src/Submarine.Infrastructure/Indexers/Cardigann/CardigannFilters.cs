using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Submarine.Core.Indexers;

namespace Submarine.Infrastructure.Indexers.Cardigann;

/// <summary>
///     The Cardigann value filters, applied after template expansion of their arguments
/// </summary>
public static class CardigannFilters
{
	/// <summary>
	///     Applies a filter by name
	/// </summary>
	/// <param name="name">The filter name</param>
	/// <param name="input">The value to transform</param>
	/// <param name="args">The expanded filter arguments</param>
	/// <param name="now">The reference point for date filters</param>
	/// <returns>The transformed value, empty when a date filter did not match</returns>
	/// <exception cref="IndexerException">When the filter name is unknown or validate fails</exception>
	public static string Apply(string name, string input, IReadOnlyList<string> args, DateTime now)
		=> name.ToLowerInvariant() switch
		{
			"querystring" => QueryString(input, Arg(args, 0)),
			"prepend" => Arg(args, 0) + input,
			"append" => input + Arg(args, 0),
			"tolower" => input.ToLowerInvariant(),
			"toupper" => input.ToUpperInvariant(),
			"replace" => input.Replace(Arg(args, 0), Arg(args, 1), StringComparison.Ordinal),
			"split" => Split(input, Arg(args, 0), args.Count > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 0),
			"trim" => args.Count > 0 ? input.Trim(args[0].ToCharArray()) : input.Trim(),
			"regexp" => Regexp(input, Arg(args, 0)),
			"re_replace" => Regex.Replace(input, Arg(args, 0), Arg(args, 1), RegexOptions.None, TimeSpan.FromSeconds(5)),
			"dateparse" => Date(input, Arg(args, 0), now),
			"timeparse" => Time(input, Arg(args, 0), now),
			"timeago" => Date(CardigannTimeAgoParser.Parse(input, now), now),
			"reltime" => Date(CardigannTimeAgoParser.Parse(input, now), now),
			"fuzzytime" => Date(CardigannTimeAgoParser.ParseFuzzy(input, now), now),
			"validfilename" => ValidFilename(input, args.Count > 0 ? args[0] : string.Empty),
			"diacritics" => RemoveDiacritics(input),
			"jsonjoinarray" => JsonJoinArray(input, Arg(args, 0), args.Count > 1 ? args[1] : ","),
			"hexdump" => HexDump(input),
			"strdump" => StrDump(input),
			"validate" => Validate(input, Arg(args, 0)),
			"htmldecode" => HttpUtility.HtmlDecode(input),
			"urldecode" => HttpUtility.UrlDecode(input),
			"urlencode" => HttpUtility.UrlEncode(input),
			"andmatch" => input,
			_ => throw new IndexerException($"Unknown Cardigann filter {name}")
		};

	private static string QueryString(string input, string key)
	{
		if (Uri.TryCreate(input, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
			return HttpUtility.ParseQueryString(uri.Query)[key] ?? string.Empty;
		if (Uri.TryCreate("http://localhost" + (input.StartsWith('/') ? input : "/" + input), UriKind.Absolute, out var relative))
			return HttpUtility.ParseQueryString(relative.Query)[key] ?? string.Empty;
		return string.Empty;
	}

	private static string Split(string input, string separator, int index)
	{
		var parts = separator.Length == 0 ? [input] : input.Split(separator);
		return index >= 0 && index < parts.Length ? parts[index] : string.Empty;
	}

	private static string Regexp(string input, string pattern)
	{
		var match = Regex.Match(input, pattern, RegexOptions.None, TimeSpan.FromSeconds(5));
		if (!match.Success)
			return string.Empty;

		return match.Groups.Count > 1 && match.Groups[1].Success ? match.Groups[1].Value : match.Value;
	}

	private static string Date(string input, string format, DateTime now)
		=> Date(CardigannDateParser.Parse(input, format, now), now);

	private static string Time(string input, string format, DateTime now)
		=> Date(CardigannDateParser.Parse(input, format, now), now);

	private static string Date(DateTime? date, DateTime now)
	{
		if (date is { } parsed)
			return parsed.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

		// signal a failed date parse with an empty value, the field may have a default
		return string.Empty;
	}

	private static string ValidFilename(string input, string replacement)
	{
		var invalid = Path.GetInvalidFileNameChars();
		var builder = new StringBuilder(input.Length);
		foreach (var character in input)
			builder.Append(Array.IndexOf(invalid, character) >= 0 ? replacement : character);
		return builder.ToString();
	}

	private static string RemoveDiacritics(string input)
	{
		var normalized = input.Normalize(NormalizationForm.FormD);
		var builder = new StringBuilder(normalized.Length);
		foreach (var character in normalized.Where(c => char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark))
			builder.Append(character);
		return builder.ToString().Normalize(NormalizationForm.FormC);
	}

	private static string JsonJoinArray(string input, string property, string separator)
	{
		try
		{
			using var document = JsonDocument.Parse(input);
			var values = document.RootElement.ValueKind == JsonValueKind.Array
				? document.RootElement.EnumerateArray()
					.Select(element => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var found) ? found.ToString() : element.ToString())
				: [document.RootElement.TryGetProperty(property, out var found) ? found.ToString() : string.Empty];
			return string.Join(separator, values);
		}
		catch (JsonException)
		{
			return string.Empty;
		}
	}

	private static string HexDump(string input)
		=> Convert.ToHexString(Encoding.UTF8.GetBytes(input));

	private static string StrDump(string input)
	{
		var builder = new StringBuilder("\"");
		foreach (var character in input)
		{
			builder.Append(character switch
			{
				'"' => "\\\"",
				'\\' => "\\\\",
				'\n' => "\\n",
				'\t' => "\\t",
				'\r' => "\\r",
				_ when character < 32 => $"\\x{(int)character:x2}",
				_ => character
			});
		}

		return builder.Append('"').ToString();
	}

	private static string Validate(string input, string expected)
	{
		if (!string.Equals(input, expected, StringComparison.Ordinal))
			throw new IndexerException($"Validation failed: expected {expected}, got {input}");
		return input;
	}

	private static string Arg(IReadOnlyList<string> args, int index)
		=> index < args.Count ? args[index] : string.Empty;
}
