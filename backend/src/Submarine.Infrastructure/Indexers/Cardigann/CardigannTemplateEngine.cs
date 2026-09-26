using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Submarine.Infrastructure.Indexers.Cardigann;

/// <summary>
///     The variable context available to Cardigann templates
/// </summary>
/// <param name="Config">The Config values, including the implicit sitelink</param>
/// <param name="Query">The Query values, see CardigannTemplateContext.BuildQuery</param>
/// <param name="Keywords">The processed search keywords</param>
/// <param name="Categories">The mapped tracker categories of the current request</param>
public sealed record CardigannTemplateContext(
	IReadOnlyDictionary<string, string> Config,
	IReadOnlyDictionary<string, object?> Query,
	string Keywords,
	IReadOnlyList<string> Categories)
{
	/// <summary>
	///     Creates the Query dictionary of a search request
	/// </summary>
	/// <param name="type">The search mode, e.g. tvsearch</param>
	/// <param name="query">The raw query term</param>
	/// <param name="keywords">The processed keywords</param>
	/// <param name="season">The season number</param>
	/// <param name="episode">The episode number</param>
	/// <param name="tvdbId">The TVDB id</param>
	/// <param name="tmdbId">The TMDB id</param>
	/// <param name="imdbId">The IMDb id with tt prefix</param>
	/// <param name="year">The release year</param>
	/// <param name="limit">The result limit</param>
	/// <param name="offset">The result offset</param>
	/// <returns>The Query dictionary</returns>
	public static IReadOnlyDictionary<string, object?> BuildQuery(
		string type,
		string? query,
		string keywords,
		int? season = null,
		int? episode = null,
		int? tvdbId = null,
		int? tmdbId = null,
		string? imdbId = null,
		int? year = null,
		int? limit = null,
		int? offset = null)
		=> new Dictionary<string, object?>
		{
			["Type"] = type,
			["Q"] = query,
			["Keywords"] = keywords,
			["Season"] = season,
			["Episode"] = episode,
			["Ep"] = episode,
			["TVDBID"] = tvdbId,
			["TMDBID"] = tmdbId,
			["IMDBID"] = imdbId,
			["IMDBIDShort"] = imdbId?.StartsWith("tt", StringComparison.OrdinalIgnoreCase) == true ? imdbId[2..] : imdbId,
			["Year"] = year,
			["Limit"] = limit,
			["Offset"] = offset
		};
}

/// <summary>
///     Renders the subset of Go templates that Cardigann definitions use:
///     values, if/else/end, range/end and the functions eq, ne, and, or, not, join, re_replace and urlquery
/// </summary>
public sealed partial class CardigannTemplateEngine
{
	private const string TrueLiteral = "true";
	private const string FalseLiteral = "false";

	private static readonly HashSet<string> Functions = new(StringComparer.Ordinal)
	{
		"eq", "ne", "and", "or", "not", "join", "re_replace", "urlquery"
	};

	/// <summary>
	///     Renders a template
	/// </summary>
	/// <param name="template">The template text</param>
	/// <param name="context">The variable context</param>
	/// <param name="result">The Result dictionary of the current row, if any</param>
	/// <param name="rangeValue">The current range element, if inside a range block</param>
	/// <returns>The rendered text, empty for a null result</returns>
	public string Render(
		string template,
		CardigannTemplateContext context,
		IReadOnlyDictionary<string, object?>? result = null,
		object? rangeValue = null)
	{
		var state = new ParserState(template);
		var output = new StringBuilder();
		RenderSequence(state, context, result, rangeValue, output, endKeywords: null);
		return output.ToString();
	}

	/// <summary>
	///     Evaluates a template as a boolean, for conditions
	/// </summary>
	/// <param name="template">The template text</param>
	/// <param name="context">The variable context</param>
	/// <param name="result">The Result dictionary of the current row</param>
	/// <returns>Whether the rendered value is truthy</returns>
	public bool RenderCondition(
		string template,
		CardigannTemplateContext context,
		IReadOnlyDictionary<string, object?> result)
		=> IsTruthy(EvaluatePipeline(ParsePipeline(template.Trim()), context, result));

	/// <summary>
	///     Renders every value of a dictionary, entries rendering to empty are omitted
	/// </summary>
	/// <param name="values">The templates keyed by output name</param>
	/// <param name="context">The variable context</param>
	/// <param name="result">The Result dictionary of the current row, if any</param>
	/// <returns>The rendered values</returns>
	public IReadOnlyDictionary<string, string> RenderAll(
		IReadOnlyDictionary<string, string> values,
		CardigannTemplateContext context,
		IReadOnlyDictionary<string, object?>? result = null)
		=> values
			.Select(kv => (kv.Key, Value: Render(kv.Value, context, result)))
			.Where(kv => kv.Value.Length > 0)
			.ToDictionary(kv => kv.Key, kv => kv.Value);

	private string RenderSequence(
		ParserState state,
		CardigannTemplateContext context,
		IReadOnlyDictionary<string, object?>? result,
		object? rangeValue,
		StringBuilder output,
		string? endKeywords)
	{
		var sequenceStart = state.Position;
		while (!state.AtEnd)
		{
			var start = state.Position;
			if (!state.TryFindActionStart(out var actionStart))
			{
				output.Append(state.Rest(start));
				state.ConsumeToEnd();
				break;
			}

			output.Append(state.Text[start..actionStart]);
			state.Position = actionStart + 2;
			if (!state.TryFindActionEnd(out var actionEnd))
				throw new CardigannTemplateException($"Unbalanced {{{{ in template near {state.Text[actionStart..]}");

			var content = state.Text[(actionStart + 2)..actionEnd].Trim();
			state.Position = actionEnd + 2;

			if (endKeywords is not null && state.MatchesKeyword(content, endKeywords))
				return state.Text[sequenceStart..actionStart];

			switch (content)
			{
				case "end" or "else" when endKeywords is null:
					throw new CardigannTemplateException($"Unexpected {{{{{content}}}}} in template");
				default:
					RenderAction(content, state, context, result, rangeValue, output);
					break;
			}
		}

		return state.Text[sequenceStart..state.Position];
	}

	private void RenderAction(
		string content,
		ParserState state,
		CardigannTemplateContext context,
		IReadOnlyDictionary<string, object?>? result,
		object? rangeValue,
		StringBuilder output)
	{
		if (content.StartsWith("if ", StringComparison.Ordinal) || content.StartsWith("if\t", StringComparison.Ordinal))
		{
			var condition = ParsePipeline(content[3..]);
			var bodyRaw = RenderSequence(state, context, result, rangeValue, new StringBuilder(), "else");

			string? elseRaw = null;
			if (state.LastKeyword == "else")
				elseRaw = RenderSequence(state, context, result, rangeValue, new StringBuilder(), "end");

			if (IsTruthy(EvaluatePipeline(condition, context, result, rangeValue)))
				output.Append(RenderRaw(bodyRaw, context, result, rangeValue));
			else if (elseRaw is not null)
				output.Append(RenderRaw(elseRaw, context, result, rangeValue));

			return;
		}

		if (content.StartsWith("range ", StringComparison.Ordinal))
		{
			var pipeline = ParsePipeline(content[6..]);
			var bodyRaw = RenderSequence(state, context, result, rangeValue, new StringBuilder(), "end");

			var rangeItems = EvaluatePipeline(pipeline, context, result, rangeValue);
			foreach (var item in Enumerate(rangeItems))
				output.Append(RenderRaw(bodyRaw, context, result, item));

			return;
		}

		var rendered = EvaluatePipeline(ParsePipeline(content), context, result, rangeValue);
		if (rendered is { } value)
			output.Append(Format(value));
	}

	private string RenderRaw(
		string raw,
		CardigannTemplateContext context,
		IReadOnlyDictionary<string, object?>? result,
		object? rangeValue)
	{
		var nested = new ParserState(raw);
		var output = new StringBuilder();
		RenderSequence(nested, context, result, rangeValue, output, endKeywords: null);
		return output.ToString();
	}

	private static IEnumerable<object> Enumerate(object? value)
		=> value switch
		{
			IReadOnlyList<object> list => list,
			IEnumerable<object> enumerable => enumerable.ToList(),
			string text when text.Length > 0 => [text],
			_ => []
		};

	private static object? EvaluatePipeline(
		Pipeline pipeline,
		CardigannTemplateContext context,
		IReadOnlyDictionary<string, object?>? result,
		object? rangeValue = null)
	{
		if (pipeline.Function is null)
			return ResolveOperand(pipeline.Operands[0], context, result, rangeValue);

		var arguments = pipeline.Operands
			.Select(operand => ResolveOperand(operand, context, result, rangeValue))
			.ToList();
		return ApplyFunction(pipeline.Function, arguments);
	}

	private static object? ResolveOperand(
		Operand operand,
		CardigannTemplateContext context,
		IReadOnlyDictionary<string, object?>? result,
		object? rangeValue)
		=> operand switch
		{
			{ Literal: { } literal } => literal,
			{ NestedPipeline: { } nested } => EvaluatePipeline(nested, context, result, rangeValue),
			{ Path: { } path } => ResolvePath(path, context, result, rangeValue),
			_ => null
		};

	private static object? ResolvePath(
		string path,
		CardigannTemplateContext context,
		IReadOnlyDictionary<string, object?>? result,
		object? rangeValue)
	{
		var segments = path.Split('.');
		if (segments[0] == "")
		{
			return segments.Length == 1
				? rangeValue
				: ResolvePath(string.Join('.', segments.Skip(1)), context, result, rangeValue);
		}

		object? current;
		switch (segments[0])
		{
			case "Config":
				current = segments.Length == 1 ? context.Config : Lookup(segments[1], context.Config);
				break;
			case "Query" when segments.Length > 1:
				current = Lookup(segments[1], context.Query);
				break;
			case "Result" when segments.Length > 1:
				current = result is null ? null : Lookup(segments[1], result);
				break;
			case "Keywords":
				current = context.Keywords;
				break;
			case "Categories":
				current = context.Categories;
				break;
			case "True":
				current = TrueLiteral;
				break;
			case "False":
				current = FalseLiteral;
				break;
			case "Today" when segments.Length > 1:
				current = TodayPart(segments[1]);
				break;
			default:
				return null;
		}

		// the switch consumed segment 0 and, for container roots, segment 1; walk the rest
		foreach (var segment in segments.Skip(current is IReadOnlyDictionary<string, object?> || segments[0] is "Keywords" or "Categories" or "True" or "False" ? 1 : 2))
		{
			if (current is not IReadOnlyDictionary<string, object?> dict)
				return null;
			current = Lookup(segment, dict);
		}

		return current;
	}

	private static object? TodayPart(string part)
		=> part switch
		{
			"Year" => (double)DateTime.UtcNow.Year,
			"Month" => (double)DateTime.UtcNow.Month,
			"Day" => (double)DateTime.UtcNow.Day,
			_ => null
		};

	private static object? Lookup(string key, IReadOnlyDictionary<string, object?> source)
		=> source.TryGetValue(key, out var value) ? value : null;

	private static object? Lookup(string key, IReadOnlyDictionary<string, string> source)
		=> source.TryGetValue(key, out var value) ? value : null;

	private static object? ApplyFunction(string function, IReadOnlyList<object?> arguments)
		=> function switch
		{
			"eq" => Equals(Comparable(arguments, 0), Comparable(arguments, 1)),
			"ne" => !Equals(Comparable(arguments, 0), Comparable(arguments, 1)),
			"and" => arguments.FirstOrDefault(argument => !IsTruthy(argument)) ?? arguments.LastOrDefault(),
			"or" => arguments.FirstOrDefault(IsTruthy) ?? arguments.LastOrDefault(),
			"not" => !IsTruthy(arguments.ElementAtOrDefault(0)),
			"join" => Join(arguments),
			"re_replace" => ReReplace(arguments),
			"urlquery" => Uri.EscapeDataString(Format(arguments.ElementAtOrDefault(0))),
			_ => null
		};

	private static object? Comparable(IReadOnlyList<object?> arguments, int index)
		=> arguments.ElementAtOrDefault(index) is { } value ? Normalize(value) : null;

	private static object Normalize(object value)
		=> value switch
		{
			IReadOnlyList<object> list => string.Join(",", list.Select(Format)),
			_ when double.TryParse(Format(value), NumberStyles.Any, CultureInfo.InvariantCulture, out var number) => number,
			_ => value
		};

	private static string Join(IReadOnlyList<object?> arguments)
	{
		if (arguments.Count < 2)
			return string.Empty;

		var separator = Format(arguments[^1]);
		var items = arguments[0] switch
		{
			IReadOnlyList<object> list => list.Select(Format),
			IEnumerable<object> enumerable => enumerable.Select(Format),
			null => [],
			_ => [Format(arguments[0])]
		};
		return string.Join(separator, items);
	}

	private static string ReReplace(IReadOnlyList<object?> arguments)
		=> arguments.Count >= 3
			? Regex.Replace(Format(arguments[0]), Format(arguments[1]), Format(arguments[2]), RegexOptions.None, TimeSpan.FromSeconds(5))
			: string.Empty;

	private static bool IsTruthy(object? value)
		=> value switch
		{
			null => false,
			bool b => b,
			string s => s.Length > 0,
			double d => d != 0,
			int i => i != 0,
			long l => l != 0,
			IReadOnlyList<object> list => list.Count > 0,
			IEnumerable<object> enumerable => enumerable.Any(),
			_ => true
		};

	private static string Format(object? value)
		=> value switch
		{
			null => string.Empty,
			string s => s,
			bool b => b ? TrueLiteral : FalseLiteral,
			double d => d.ToString("0.###############", CultureInfo.InvariantCulture),
			IReadOnlyList<object> list => string.Join(",", list.Select(Format)),
			_ => value.ToString() ?? string.Empty
		};

	private static Pipeline ParsePipeline(string text)
	{
		var tokens = Tokenize(text);
		if (tokens.Count == 0)
			return new Pipeline(null, [new Operand(string.Empty)]);

		var first = tokens[0];
		if (first.Path is { } path && Functions.Contains(path))
			return new Pipeline(path, [.. tokens.Skip(1)]);

		if (first.Path is not (null or { Length: 0 }) && !first.Path.StartsWith('.'))
			return new Pipeline(first.Path, [.. tokens.Skip(1)]);

		return new Pipeline(null, tokens);
	}

	private static List<Operand> Tokenize(string text)
	{
		var operands = new List<Operand>();
		var index = 0;
		while (index < text.Length)
		{
			SkipWhitespace(text, ref index);
			if (index >= text.Length)
				break;

			switch (text[index])
			{
				case '"':
					operands.Add(ReadString(text, ref index));
					break;
				case '(':
				{
					index++;
					var start = index;
					var depth = 1;
					while (index < text.Length && depth > 0)
					{
						if (text[index] == '(')
							depth++;
						else if (text[index] == ')')
							depth--;
						if (depth > 0)
							index++;
					}

					operands.Add(new Operand(NestedPipeline: ParsePipeline(text[start..index])));
					index++;
					break;
				}
				case '.':
					operands.Add(new Operand(ReadPath(text, ref index)));
					break;
				default:
				{
					var start = index;
					while (index < text.Length && !char.IsWhiteSpace(text[index]))
						index++;
					var token = text[start..index];
					operands.Add(
						char.IsDigit(token[0]) || (token[0] == '-' && token.Length > 1 && char.IsDigit(token[1]))
							? new Operand(Literal: token)
							: new Operand(token));
					break;
				}
			}
		}

		return operands;
	}

	private static Operand ReadString(string text, ref int index)
	{
		index++;
		var builder = new StringBuilder();
		while (index < text.Length && text[index] != '"')
		{
			if (text[index] == '\\' && index + 1 < text.Length)
			{
				index++;
				builder.Append(text[index] switch
				{
					'n' => '\n',
					't' => '\t',
					'r' => '\r',
					_ => text[index]
				});
			}
			else
			{
				builder.Append(text[index]);
			}

			index++;
		}

		index++;
		return new Operand(Literal: builder.ToString());
	}

	private static string ReadPath(string text, ref int index)
	{
		var start = index;
		index++;
		while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '_' || text[index] == '.'))
			index++;
		return text[start..index];
	}

	private static void SkipWhitespace(string text, ref int index)
	{
		while (index < text.Length && char.IsWhiteSpace(text[index]))
			index++;
	}

	private sealed record Operand(string? Path = null, string? Literal = null, Pipeline? NestedPipeline = null);

	private sealed record Pipeline(string? Function, List<Operand> Operands);

	private sealed class ParserState(string text)
	{
		public string Text { get; } = text;

		public int Position { get; set; }

		public string? LastKeyword { get; private set; }

		public bool AtEnd => Position >= Text.Length;

		public string Rest(int from) => Text[from..];

		public void ConsumeToEnd() => Position = Text.Length;

		public bool TryFindActionStart(out int position)
		{
			position = Text.IndexOf("{{", Position, StringComparison.Ordinal);
			return position >= 0;
		}

		public bool TryFindActionEnd(out int position)
		{
			position = Text.IndexOf("}}", Position, StringComparison.Ordinal);
			return position >= 0;
		}

		public bool MatchesKeyword(string content, string keywords)
		{
			if (content == keywords || content == "end")
			{
				LastKeyword = content;
				return true;
			}

			return false;
		}
	}
}

/// <summary>
///     A Cardigann template could not be parsed
/// </summary>
/// <param name="message">The error message</param>
public class CardigannTemplateException(string message) : Exception(message);
