using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Submarine.Infrastructure.Indexers.Cardigann;

/// <summary>
///     The search block of a definition
/// </summary>
public record CardigannSearch
{
	/// <summary>
	///     The request paths to query, results are merged
	/// </summary>
	[YamlMember(Alias = "paths")]
	public List<CardigannSearchPath> Paths { get; init; } = [];

	/// <summary>
	///     Query parameters or form fields shared by all paths
	/// </summary>
	[YamlMember(Alias = "inputs")]
	public Dictionary<string, string> Inputs { get; init; } = [];

	/// <summary>
	///     Filters applied to the search keywords before use
	/// </summary>
	[YamlMember(Alias = "keywordsfilters")]
	public List<CardigannFilter> KeywordsFilters { get; init; } = [];

	/// <summary>
	///     Filters applied to the raw response body before parsing
	/// </summary>
	[YamlMember(Alias = "preprocessingfilters")]
	public List<CardigannFilter> PreprocessingFilters { get; init; } = [];

	/// <summary>
	///     Extra request headers, values are template rendered
	/// </summary>
	[YamlMember(Alias = "headers")]
	public Dictionary<string, List<string>> Headers { get; init; } = [];

	/// <summary>
	///     How to find result rows
	/// </summary>
	[YamlMember(Alias = "rows")]
	public CardigannRows Rows { get; init; } = new();

	/// <summary>
	///     How to extract the fields of a row
	/// </summary>
	[YamlMember(Alias = "fields")]
	public Dictionary<string, CardigannField> Fields { get; init; } = [];
}

/// <summary>
///     A single search request path
/// </summary>
public record CardigannSearchPath
{
	/// <summary>
	///     The path template or absolute url template
	/// </summary>
	[YamlMember(Alias = "path")]
	public string Path { get; init; } = string.Empty;

	/// <summary>
	///     GET or POST, defaults to GET
	/// </summary>
	[YamlMember(Alias = "method")]
	public string Method { get; init; } = "get";

	/// <summary>
	///     Inputs overriding or extending the shared search inputs
	/// </summary>
	[YamlMember(Alias = "inputs")]
	public Dictionary<string, string>? Inputs { get; init; }

	/// <summary>
	///     How to interpret the response body
	/// </summary>
	[YamlMember(Alias = "response")]
	public CardigannResponse? Response { get; init; }

	/// <summary>
	///     Restricts the search to these tracker categories
	/// </summary>
	[YamlMember(Alias = "categories")]
	public List<string>? Categories { get; init; }

	/// <summary>
	///     Whether redirects may be followed
	/// </summary>
	[YamlMember(Alias = "followredirect")]
	public bool FollowRedirect { get; init; } = true;

	/// <summary>
	///     Whether the path inherits the inputs of the first path, defaults to true
	/// </summary>
	[YamlMember(Alias = "inheritinputs")]
	public bool InheritInputs { get; init; } = true;
}

/// <summary>
///     How to interpret a response body
/// </summary>
public record CardigannResponse
{
	/// <summary>
	///     html, json or xml, defaults to html
	/// </summary>
	[YamlMember(Alias = "type")]
	public string Type { get; init; } = "html";

	/// <summary>
	///     The attribute holding the rows, for xml responses
	/// </summary>
	[YamlMember(Alias = "attribute")]
	public string? Attribute { get; init; }

	/// <summary>
	///     When the body contains this text, the search has no results instead of failing
	/// </summary>
	[YamlMember(Alias = "noResultsMessage")]
	public string? NoResultsMessage { get; init; }
}

/// <summary>
///     How to find the result rows of a response
/// </summary>
public record CardigannRows
{
	/// <summary>
	///     The row selector, comma separated alternatives for html, path or $ root for json
	/// </summary>
	[YamlMember(Alias = "selector")]
	public string Selector { get; init; } = string.Empty;

	/// <summary>
	///     Row filters, only andmatch is supported
	/// </summary>
	[YamlMember(Alias = "filters")]
	public List<CardigannFilter> Filters { get; init; } = [];

	/// <summary>
	///     The amount of leading rows to skip
	/// </summary>
	[YamlMember(Alias = "after")]
	public int? After { get; init; }

	/// <summary>
	///     A date source that applies to all rows lacking their own date
	/// </summary>
	[YamlMember(Alias = "dateheaders")]
	public CardigannField? DateHeaders { get; init; }

	/// <summary>
	///     A selector reporting the total amount of available rows
	/// </summary>
	[YamlMember(Alias = "count")]
	public CardigannField? Count { get; init; }

	/// <summary>
	///     The attribute holding the rows, for json responses
	/// </summary>
	[YamlMember(Alias = "attribute")]
	public string? Attribute { get; init; }

	/// <summary>
	///     Whether a row may itself contain multiple results, for json responses
	/// </summary>
	[YamlMember(Alias = "multiple")]
	public bool Multiple { get; init; }

	/// <summary>
	///     Whether a row without the selected attribute yields no results instead of an error
	/// </summary>
	[YamlMember(Alias = "missingAttributeEqualsNoResults")]
	public bool MissingAttributeEqualsNoResults { get; init; }
}

/// <summary>
///     A named filter with template rendered arguments
/// </summary>
public record CardigannFilter
{
	/// <summary>
	///     The filter name, see CardigannFilters
	/// </summary>
	[YamlMember(Alias = "name")]
	public string Name { get; init; } = string.Empty;

	/// <summary>
	///     The filter arguments
	/// </summary>
	[YamlMember(Alias = "args")]
	public List<string>? Args { get; init; }
}

/// <summary>
///     How to extract a single field from a row
/// </summary>
public record CardigannField
{
	/// <summary>
	///     The selector, comma separated alternatives where the first match wins
	/// </summary>
	[YamlMember(Alias = "selector")]
	public string? Selector { get; init; }

	/// <summary>
	///     Whether the field may be empty
	/// </summary>
	[YamlMember(Alias = "optional")]
	public bool Optional { get; init; }

	/// <summary>
	///     The value used when the selector found nothing, template rendered
	/// </summary>
	[YamlMember(Alias = "default")]
	public string? Default { get; init; }

	/// <summary>
	///     A literal value, template rendered, instead of a selector
	/// </summary>
	[YamlMember(Alias = "text")]
	public string? Text { get; init; }

	/// <summary>
	///     The attribute to read instead of the text content
	/// </summary>
	[YamlMember(Alias = "attribute")]
	public string? Attribute { get; init; }

	/// <summary>
	///     Filters applied to the extracted value
	/// </summary>
	[YamlMember(Alias = "filters")]
	public List<CardigannFilter> Filters { get; init; } = [];

	/// <summary>
	///     Maps extracted values onto other values, * is the fallback
	/// </summary>
	[YamlMember(Alias = "case")]
	public Dictionary<string, string>? Case { get; init; }

	/// <summary>
	///     Selector whose sub elements are removed before extraction
	/// </summary>
	[YamlMember(Alias = "remove")]
	public string? Remove { get; init; }

	/// <summary>
	///     Selector sourced form fields merged into the template context, selector to field name
	/// </summary>
	[YamlMember(Alias = "selectorinputs")]
	public Dictionary<string, string>? SelectorInputs { get; init; }
}

/// <summary>
///     The download block of a definition, resolving the real download link from a details page
/// </summary>
public record CardigannDownload
{
	/// <summary>
	///     Alternative selectors for the download link, first match wins
	/// </summary>
	[YamlMember(Alias = "selectors")]
	public List<CardigannField> Selectors { get; init; } = [];

	/// <summary>
	///     A page fetched before the download link is resolved
	/// </summary>
	[YamlMember(Alias = "before")]
	public CardigannDownloadBefore? Before { get; init; }

	/// <summary>
	///     The http method of the download request
	/// </summary>
	[YamlMember(Alias = "method")]
	public string Method { get; init; } = "get";

	/// <summary>
	///     Magnet construction from a details page infohash
	/// </summary>
	[YamlMember(Alias = "infohash")]
	public CardigannDownloadInfoHash? InfoHash { get; init; }
}

/// <summary>
///     A page fetched before resolving the download link
/// </summary>
public record CardigannDownloadBefore
{
	/// <summary>
	///     The path template of the page
	/// </summary>
	[YamlMember(Alias = "path")]
	public string Path { get; init; } = string.Empty;

	/// <summary>
	///     GET or POST
	/// </summary>
	[YamlMember(Alias = "method")]
	public string Method { get; init; } = "get";

	/// <summary>
	///     Inputs of the request
	/// </summary>
	[YamlMember(Alias = "inputs")]
	public Dictionary<string, string> Inputs { get; init; } = [];

	/// <summary>
	///     Selector evaluated on the response, result goes into the template context
	/// </summary>
	[YamlMember(Alias = "selector")]
	public string? Selector { get; init; }

	/// <summary>
	///     The attribute to read instead of the text content
	/// </summary>
	[YamlMember(Alias = "attribute")]
	public string? Attribute { get; init; }
}

/// <summary>
///     Magnet construction from an infohash found on the details page
/// </summary>
public record CardigannDownloadInfoHash
{
	/// <summary>
	///     How to extract the infohash
	/// </summary>
	[YamlMember(Alias = "hash")]
	public CardigannField Hash { get; init; } = new();

	/// <summary>
	///     How to extract the display title of the magnet
	/// </summary>
	[YamlMember(Alias = "title")]
	public CardigannField Title { get; init; } = new();

	/// <summary>
	///     Whether the hash is extracted from the response of the before request
	/// </summary>
	[YamlMember(Alias = "usebeforeresponse")]
	public bool UseBeforeResponse { get; init; }
}
