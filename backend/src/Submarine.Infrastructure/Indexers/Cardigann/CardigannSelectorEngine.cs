using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Submarine.Core.Indexers;

namespace Submarine.Infrastructure.Indexers.Cardigann;

/// <summary>
///     The kind of a response body
/// </summary>
public enum CardigannResponseType
{
	/// <summary>
	///     Html documents
	/// </summary>
	Html,

	/// <summary>
	///     Json documents
	/// </summary>
	Json,

	/// <summary>
	///     Xml documents
	/// </summary>
	Xml
}

/// <summary>
///     Extracts rows and fields from html, json and xml responses
/// </summary>
public sealed partial class CardigannSelectorEngine
{
	private static readonly IReadOnlyDictionary<string, object?> EmptyResult = new Dictionary<string, object?>();

	private readonly HtmlParser _htmlParser = new();

	/// <summary>
	///     Queries a document with Cardigann selector semantics (including :contains)
	/// </summary>
	/// <param name="document">The parsed html document</param>
	/// <param name="selector">The css selector</param>
	/// <returns>The matching elements</returns>
	public IReadOnlyList<IElement> Query(IDocument document, string selector)
		=> QueryHtml(document, selector);

	/// <summary>
	///     Parses a response body
	/// </summary>
	/// <param name="type">The response kind</param>
	/// <param name="body">The response text</param>
	/// <returns>The parsed document</returns>
	public object ParseResponse(CardigannResponseType type, string body)
		=> type switch
		{
			CardigannResponseType.Html => _htmlParser.ParseDocument(body),
			CardigannResponseType.Json => JsonDocument.Parse(body),
			CardigannResponseType.Xml => ToXmlDocument(body),
			_ => throw new ArgumentOutOfRangeException(nameof(type))
		};

	/// <summary>
	///     Selects the result rows of a parsed document
	/// </summary>
	/// <param name="document">The parsed document</param>
	/// <param name="type">The response kind</param>
	/// <param name="rows">The row definition</param>
	/// <returns>The row objects, each with its parent, empty lists for json rows without parent</returns>
	public IReadOnlyList<CardigannRow> SelectRows(object document, CardigannResponseType type, CardigannRows rows)
	{
		var selected = type switch
		{
			CardigannResponseType.Html => SelectHtmlRows((IDocument)document, rows.Selector, rows.Filters.Any(filter => filter.Name.Equals("andmatch", StringComparison.OrdinalIgnoreCase))),
			CardigannResponseType.Json => SelectJsonRows((JsonDocument)document, rows),
			CardigannResponseType.Xml => SelectXmlRows((XmlDocument)document, rows.Selector),
			_ => []
		};

		if (rows.After is { } after && after > 0)
			selected = selected.Skip(after).ToList();

		return selected;
	}

	/// <summary>
	///     Evaluates a single field on a row
	/// </summary>
	/// <param name="row">The row</param>
	/// <param name="type">The response kind</param>
	/// <param name="field">The field definition</param>
	/// <param name="renderTemplate">Template renderer for text, default and filter arguments</param>
	/// <param name="now">The reference point for date filters</param>
	/// <returns>The extracted value, or null when the field is missing</returns>
	public string? EvaluateField(
		CardigannRow row,
		CardigannResponseType type,
		CardigannField field,
		Func<string, IReadOnlyDictionary<string, object?>, string> renderTemplate,
		DateTime now)
	{
		if (field.Text is { } text)
			return renderTemplate(text, EmptyResult);

		if (string.IsNullOrWhiteSpace(field.Selector))
			return null;

		var alternatives = SplitAlternatives(field.Selector);
		foreach (var alternative in alternatives)
		{
			var value = type switch
			{
				CardigannResponseType.Html => EvaluateHtmlField(row.Html!, alternative, field),
				CardigannResponseType.Json => EvaluateJsonField(row.Json, row.JsonParent, alternative, field.Attribute),
				CardigannResponseType.Xml => EvaluateXmlField(row.Xml!, alternative, field.Attribute),
				_ => null
			};

			if (value is null)
				continue;

			value = ApplyFilters(value, field.Filters, renderTemplate, now);

			if (field.Case is { } caseMap && caseMap.Count > 0)
			{
				if (caseMap.TryGetValue(value, out var mapped))
					value = mapped;
				else if (caseMap.TryGetValue("*", out var fallback))
					value = fallback;
				else
					continue;
			}

			if (value.Length == 0 && field.Default is { } fallbackTemplate)
				value = renderTemplate(fallbackTemplate, EmptyResult);

			return value;
		}

		if (field.Default is { } defaultTemplate)
			return renderTemplate(defaultTemplate, EmptyResult);

		return null;
	}

	private string ApplyFilters(
		string value,
		IReadOnlyList<CardigannFilter> filters,
		Func<string, IReadOnlyDictionary<string, object?>, string> renderTemplate,
		DateTime now)
	{
		foreach (var filter in filters)
		{
			var args = (filter.Args ?? [])
				.Select(arg => renderTemplate(arg, EmptyResult))
				.ToList();
			value = CardigannFilters.Apply(filter.Name, value, args, now);
		}

		return value;
	}

	private IReadOnlyList<CardigannRow> SelectHtmlRows(IDocument document, string selector, bool andMatch)
	{
		var alternatives = SplitAlternatives(selector);
		var perAlternative = alternatives
			.Select(alternative => QueryHtml(document, alternative))
			.ToList();

		if (alternatives.Count > 1 && andMatch)
		{
			// rows must be matched by every alternative
			var first = perAlternative[0];
			var rest = perAlternative.Skip(1);
			return first.Where(row => rest.All(others => others.Contains(row)))
				.Select(row => new CardigannRow(row))
				.ToList();
		}

		return perAlternative
			.SelectMany(rows => rows)
			.Distinct()
			.Select(row => new CardigannRow(row))
			.ToList();
	}

	private List<IElement> QueryHtml(IDocument document, string selector)
	{
		var predicates = ExtractContainsPredicates(selector, out var stripped);
		stripped = stripped.Trim();
		if (stripped.Length == 0)
			return [];

		try
		{
			var matches = document.QuerySelectorAll(stripped);
			if (predicates.Count == 0)
				return [.. matches];

			return matches
				.Where(element => predicates.All(predicate => element.TextContent.Contains(predicate.Text) != predicate.Negate))
				.ToList();
		}
		catch (AngleSharp.Dom.DomException)
		{
			return [];
		}
	}

	private string? EvaluateHtmlField(IElement row, string selector, CardigannField field)
	{
		if (!string.IsNullOrWhiteSpace(field.Remove))
		{
			foreach (var removable in QueryHtml(row.Owner!, field.Remove).Where(element => row.Contains(element)))
				removable.Remove();
		}

		var scopeId = $"scope{Guid.NewGuid():N}";
		row.SetAttribute("data-cardigann-scope", scopeId);
		List<IElement> matches;
		try
		{
			var rewritten = ScopeRegex().Replace(selector.Trim(), $"[data-cardigann-scope=\"{scopeId}\"]");
			matches = QueryHtml(row.Owner!, rewritten)
				.Where(element => element == row || row.Contains(element))
				.ToList();
		}
		finally
		{
			row.RemoveAttribute("data-cardigann-scope");
		}

		if (matches.Count == 0)
			return null;

		var target = matches[0];
		return field.Attribute is { } attribute
			? target.GetAttribute(attribute)
			: target.TextContent.Trim();
	}

	private IReadOnlyList<CardigannRow> SelectJsonRows(JsonDocument document, CardigannRows rows)
	{
		var selector = rows.Selector.Trim();
		var predicate = ContainsPredicateRegex().Match(selector);
		var path = PathRegex().Replace(selector, string.Empty).Trim();
		if (path.Length == 0 || path == "$")
		{
			return CollectJsonRows(document.RootElement, parent: null, rows, predicate);
		}

		var resolved = ResolveJsonPath(document.RootElement, path.StartsWith('$') ? path[1..] : path);
		return resolved is { } element ? CollectJsonRows(element, null, rows, predicate) : [];
	}

	private List<CardigannRow> CollectJsonRows(
		JsonElement element,
		JsonElement? parent,
		CardigannRows rows,
		Match predicate)
	{
		var results = new List<CardigannRow>();
		var items = element.ValueKind == JsonValueKind.Array
			? element.EnumerateArray().ToList()
			: [element];

		foreach (var item in items)
		{
			if (rows.Attribute is { } attribute)
			{
				if (item.ValueKind != JsonValueKind.Object
					|| !item.TryGetProperty(attribute, out var nested)
					|| nested.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
				{
					if (rows.MissingAttributeEqualsNoResults)
						continue;

					throw new IndexerException($"Json rows are missing the attribute {attribute}");
				}

				if (nested.ValueKind == JsonValueKind.Array && rows.Multiple)
				{
					foreach (var child in nested.EnumerateArray())
						AddJsonRow(results, child, item, rows, predicate);
				}
				else
				{
					AddJsonRow(results, nested, item, rows, predicate);
				}
			}
			else
			{
				AddJsonRow(results, item, parent, rows, predicate);
			}
		}

		return results;
	}

	private static void AddJsonRow(
		List<CardigannRow> results,
		JsonElement item,
		JsonElement? parent,
		CardigannRows rows,
		Match predicate)
	{
		if (predicate.Success && !JsonItemMatches(item, predicate.Groups["prop"].Value, predicate.Groups["text"].Value))
			return;

		results.Add(new CardigannRow(item, parent));
	}

	private static bool JsonItemMatches(JsonElement item, string property, string text)
		=> item.ValueKind == JsonValueKind.Object
			&& item.TryGetProperty(property, out var value)
			&& JsonToString(value).Contains(text, StringComparison.OrdinalIgnoreCase);

	private string? EvaluateJsonField(JsonElement? element, JsonElement? parent, string selector, string? attribute)
	{
		var path = selector.Trim();
		JsonElement? source;
		if (path.StartsWith("..", StringComparison.Ordinal))
		{
			source = parent is { } p ? ResolveJsonPath(p, path[2..]) : null;
		}
		else
		{
			if (element is not { } current)
				return null;
			source = path.Length == 0 || path == "." ? current : ResolveJsonPath(current, path);
		}

		return source is { } found ? JsonToString(found) : null;
	}

	private static JsonElement? ResolveJsonPath(JsonElement root, string path)
	{
		var current = root;
		var consumed = 0;
		while (consumed < path.Length)
		{
			if (path[consumed] == '.')
				consumed++;
			if (consumed >= path.Length)
				break;

			if (path[consumed] == '[')
			{
				var close = path.IndexOf(']', consumed);
				if (close < 0)
					return null;
				if (!int.TryParse(path[(consumed + 1)..close], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
					return null;
				if (current.ValueKind != JsonValueKind.Array || index >= current.GetArrayLength())
					return null;
				current = current[index];
				consumed = close + 1;
				continue;
			}

			var keyStart = consumed;
			while (consumed < path.Length && path[consumed] != '.' && path[consumed] != '[')
				consumed++;
			var key = path[keyStart..consumed];
			if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(key, out var next))
				return null;
			current = next;
		}

		return current;
	}

	private static string JsonToString(JsonElement element)
		=> element.ValueKind switch
		{
			JsonValueKind.String => element.GetString() ?? string.Empty,
			JsonValueKind.Number => element.GetRawText(),
			JsonValueKind.True => "true",
			JsonValueKind.False => "false",
			JsonValueKind.Null => string.Empty,
			_ => element.GetRawText()
		};

	private static IReadOnlyList<CardigannRow> SelectXmlRows(XmlDocument document, string selector)
	{
		var nodes = document.SelectNodes("/" + ToXPathSegments(selector));
		if (nodes is null)
			return [];

		var rows = new List<CardigannRow>();
		foreach (var node in nodes.Cast<XmlNode>())
			rows.Add(new CardigannRow(node));

		return rows;
	}

	private string? EvaluateXmlField(XmlNode row, string selector, string? attribute)
	{
		var segments = selector.Contains('>', StringComparison.Ordinal)
			? ToXPathSegments(selector.Split('>', StringSplitOptions.TrimEntries)[^1])
			: ToXPathSegments(selector.Trim());
		var found = row.SelectSingleNode("./" + segments);
		if (found is null)
			return null;

		if (attribute is { } attributeName)
			return found.Attributes?[attributeName]?.Value;

		return found is XmlElement element ? element.InnerText.Trim() : found.Value?.Trim();
	}

	private static string ToXPathSegments(string selector)
	{
		var trimmed = selector.Trim();
		if (trimmed.StartsWith('$'))
			trimmed = trimmed[1..];

		var parts = trimmed.Split('>', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		return string.Join("/", parts.Select(part =>
		{
			var attribute = AttributeSelectorRegex().Match(part);
			if (attribute.Success)
				return $"*[@{attribute.Groups["name"].Value}='{attribute.Groups["value"].Value}']";

			return $"*[local-name()='{part}']";
		}));
	}

	private static XmlDocument ToXmlDocument(string body)
	{
		var document = new XmlDocument
		{
			XmlResolver = null
		};
		using var reader = XmlReader.Create(new StringReader(body), new XmlReaderSettings
		{
			DtdProcessing = DtdProcessing.Ignore,
			XmlResolver = null
		});
		document.Load(reader);
		return document;
	}

	private static List<string> SplitAlternatives(string selector)
	{
		var parts = new List<string>();
		var builder = new System.Text.StringBuilder();
		var quote = '\0';
		var depth = 0;
		foreach (var character in selector)
		{
			if (quote != '\0')
			{
				builder.Append(character);
				if (character == quote)
					quote = '\0';
				continue;
			}

			switch (character)
			{
				case '"' or '\'':
					quote = character;
					builder.Append(character);
					break;
				case '(':
					depth++;
					builder.Append(character);
					break;
				case ')':
					depth--;
					builder.Append(character);
					break;
				case ',' when depth == 0:
					parts.Add(builder.ToString());
					builder.Clear();
					break;
				default:
					builder.Append(character);
					break;
			}
		}

		parts.Add(builder.ToString());
		return [.. parts.Select(part => part.Trim()).Where(part => part.Length > 0)];
	}

	private static List<(bool Negate, string Text)> ExtractContainsPredicates(string selector, out string stripped)
	{
		var predicates = new List<(bool, string)>();
		var builder = new System.Text.StringBuilder();
		for (var index = 0; index < selector.Length;)
		{
			var not = selector[index..].StartsWith(":not(:contains(", StringComparison.OrdinalIgnoreCase);
			var plain = !not && selector[index..].StartsWith(":contains(", StringComparison.OrdinalIgnoreCase);
			if (!not && !plain)
			{
				builder.Append(selector[index]);
				index++;
				continue;
			}

			var consume = not ? ":not(:contains(" : ":contains(";
			var start = index + consume.Length;
			var close = selector.IndexOf(')', start);
			var text = selector[start..close].Trim('"', '\'');
			predicates.Add((not, text));
			index = close + 1;
			if (not)
				index++; // closing paren of :not(
		}

		stripped = builder.ToString();
		return predicates;
	}

	[GeneratedRegex(@":has\((?<prop>\w+):contains\((?<text>[^)]*)\)\)")]
	private static partial Regex ContainsPredicateRegex();

	[GeneratedRegex(@":scope\b", RegexOptions.IgnoreCase)]
	private static partial Regex ScopeRegex();

	[GeneratedRegex(@":has\(.*\)$")]
	private static partial Regex PathRegex();

	[GeneratedRegex(@"^\[(?<name>[\w-]+)=(?<value>[^\]]+)\]$")]
	private static partial Regex AttributeSelectorRegex();
}

/// <summary>
///     A result row of any response kind
/// </summary>
public readonly record struct CardigannRow
{
	/// <summary>
	///     The html row element, if html
	/// </summary>
	public IElement? Html { get; }

	/// <summary>
	///     The json row element, if json
	/// </summary>
	public JsonElement? Json { get; }

	/// <summary>
	///     The parent json element, if any
	/// </summary>
	public JsonElement? JsonParent { get; }

	/// <summary>
	///     The xml row node, if xml
	/// </summary>
	public XmlNode? Xml { get; }

	/// <summary>
	///     Creates an html row
	/// </summary>
	/// <param name="html">The row element</param>
	public CardigannRow(IElement html) : this(html, null, null, null) { }

	/// <summary>
	///     Creates a json row
	/// </summary>
	/// <param name="json">The row element</param>
	/// <param name="parent">Its parent element</param>
	public CardigannRow(JsonElement json, JsonElement? parent) : this(null, json, parent, null) { }

	/// <summary>
	///     Creates an xml row
	/// </summary>
	/// <param name="xml">The row node</param>
	public CardigannRow(XmlNode xml) : this(null, null, null, xml) { }

	private CardigannRow(IElement? html, JsonElement? json, JsonElement? parent, XmlNode? xml)
	{
		Html = html;
		Json = json;
		JsonParent = parent;
		Xml = xml;
	}
}
