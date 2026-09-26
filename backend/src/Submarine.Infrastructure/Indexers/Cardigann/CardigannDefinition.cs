using System.Collections.Generic;
using System.Linq;
using Submarine.Core.Indexers;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Submarine.Infrastructure.Indexers.Cardigann;

/// <summary>
///     A Cardigann v11 indexer definition as described at
///     https://github.com/Prowlarr/Indexers/blob/master/definitions/v11/schema.json
/// </summary>
public record CardigannDefinition
{
	/// <summary>
	///     The unique definition id, also the file name
	/// </summary>
	[YamlMember(Alias = "id")]
	public string Id { get; init; } = string.Empty;

	/// <summary>
	///     The display name of the tracker
	/// </summary>
	[YamlMember(Alias = "name")]
	public string Name { get; init; } = string.Empty;

	/// <summary>
	///     A short description of the tracker
	/// </summary>
	[YamlMember(Alias = "description")]
	public string Description { get; init; } = string.Empty;

	/// <summary>
	///     The language of the tracker, e.g. en-US
	/// </summary>
	[YamlMember(Alias = "language")]
	public string Language { get; init; } = "en-US";

	/// <summary>
	///     public, private or semi-private
	/// </summary>
	[YamlMember(Alias = "type")]
	public string Type { get; init; } = "public";

	/// <summary>
	///     torrent or usenet, defaults to torrent as v11 definitions are torrent trackers
	/// </summary>
	[YamlMember(Alias = "protocol")]
	public string? Protocol { get; init; }

	/// <summary>
	///     The text encoding of the site responses
	/// </summary>
	[YamlMember(Alias = "encoding")]
	public string Encoding { get; init; } = "UTF-8";

	/// <summary>
	///     Minimum seconds between two requests to the site
	/// </summary>
	[YamlMember(Alias = "requestDelay")]
	public double? RequestDelay { get; init; }

	/// <summary>
	///     The current base urls of the tracker
	/// </summary>
	[YamlMember(Alias = "links")]
	public List<string> Links { get; init; } = [];

	/// <summary>
	///     Former base urls, kept for reference
	/// </summary>
	[YamlMember(Alias = "legacylinks")]
	public List<string> LegacyLinks { get; init; } = [];

	/// <summary>
	///     The search capabilities of the tracker
	/// </summary>
	[YamlMember(Alias = "caps")]
	public CardigannCaps Caps { get; init; } = new();

	/// <summary>
	///     The user configurable settings of the definition
	/// </summary>
	[YamlMember(Alias = "settings")]
	public List<CardigannSetting> Settings { get; init; } = [];

	/// <summary>
	///     The login flow, if the tracker requires authentication
	/// </summary>
	[YamlMember(Alias = "login")]
	public CardigannLogin? Login { get; init; }

	/// <summary>
	///     How to search the tracker
	/// </summary>
	[YamlMember(Alias = "search")]
	public CardigannSearch Search { get; init; } = new();

	/// <summary>
	///     How to resolve download links, if they are not in the result rows
	/// </summary>
	[YamlMember(Alias = "download")]
	public CardigannDownload? Download { get; init; }

	/// <summary>
	///     The effective protocol of the definition
	/// </summary>
	public Submarine.Core.Provider.Protocol ProtocolKind
		=> string.Equals(Protocol, "usenet", StringComparison.OrdinalIgnoreCase)
			? Submarine.Core.Provider.Protocol.USENET
			: Submarine.Core.Provider.Protocol.BITTORRENT;

	/// <summary>
	///     Projects the definition onto its user facing metadata
	/// </summary>
	/// <returns>The metadata, without the YAML body</returns>
	public IndexerDefinitionInfo ToInfo()
		=> new(
			Id,
			Name,
			Description,
			Language,
			Type,
			Protocol ?? "torrent",
			Links,
			LegacyLinks,
			Settings.Select(setting => new IndexerSettingField(
				setting.Name,
				ParseSettingType(setting.Type),
				setting.Label,
				setting.Default,
				setting.Options)).ToList(),
			Caps.CategoryMappings
				.Select(mapping => IndexerCategories.Resolve(mapping.Cat))
				.OfType<int>()
				.Distinct()
				.Order()
				.ToList());

	private static IndexerSettingType ParseSettingType(string? type)
		=> type?.ToLowerInvariant() switch
		{
			"password" => IndexerSettingType.PASSWORD,
			"checkbox" => IndexerSettingType.CHECKBOX,
			"select" => IndexerSettingType.SELECT,
			"info" => IndexerSettingType.INFO,
			"cardiganncaptcha" => IndexerSettingType.CARDIGANNCAPTCHA,
			_ => IndexerSettingType.TEXT
		};
}

/// <summary>
///     The caps block of a definition
/// </summary>
public record CardigannCaps
{
	/// <summary>
	///     Maps tracker categories onto standard Newznab categories
	/// </summary>
	[YamlMember(Alias = "categorymappings")]
	public List<CardigannCategoryMapping> CategoryMappings { get; init; } = [];

	/// <summary>
	///     Direct tracker id to standard category name mapping for numeric trackers
	/// </summary>
	[YamlMember(Alias = "categories")]
	public Dictionary<string, string> Categories { get; init; } = [];

	/// <summary>
	///     The supported search modes and their parameters
	/// </summary>
	[YamlMember(Alias = "modes")]
	public Dictionary<string, List<string>> Modes { get; init; } = [];

	/// <summary>
	///     Whether searches without any parameters are allowed
	/// </summary>
	[YamlMember(Alias = "allowrawsearch")]
	public bool AllowRawSearch { get; init; }
}

/// <summary>
///     A single tracker to standard category mapping
/// </summary>
public record CardigannCategoryMapping
{
	/// <summary>
	///     The tracker category id or name
	/// </summary>
	[YamlMember(Alias = "id")]
	public string Id { get; init; } = string.Empty;

	/// <summary>
	///     The standard category path, e.g. TV/Anime
	/// </summary>
	[YamlMember(Alias = "cat")]
	public string Cat { get; init; } = string.Empty;

	/// <summary>
	///     The tracker side description
	/// </summary>
	[YamlMember(Alias = "desc")]
	public string? Desc { get; init; }

	/// <summary>
	///     Whether the category is searched when no categories are requested
	/// </summary>
	[YamlMember(Alias = "default")]
	public bool Default { get; init; }
}

/// <summary>
///     A single definition setting field
/// </summary>
public record CardigannSetting
{
	/// <summary>
	///     The name referenced by templates as Config.Name
	/// </summary>
	[YamlMember(Alias = "name")]
	public string Name { get; init; } = string.Empty;

	/// <summary>
	///     text, password, checkbox, select, info or cardigannCaptcha
	/// </summary>
	[YamlMember(Alias = "type")]
	public string? Type { get; init; }

	/// <summary>
	///     The human readable label
	/// </summary>
	[YamlMember(Alias = "label")]
	public string? Label { get; init; }

	/// <summary>
	///     The default value
	/// </summary>
	[YamlMember(Alias = "default")]
	public string? Default { get; init; }

	/// <summary>
	///     The options of a select field, value to label
	/// </summary>
	[YamlMember(Alias = "options")]
	public Dictionary<string, string>? Options { get; init; }
}

/// <summary>
///     The login block of a definition
/// </summary>
public record CardigannLogin
{
	/// <summary>
	///     The login page path or absolute url
	/// </summary>
	[YamlMember(Alias = "path")]
	public string? Path { get; init; }

	/// <summary>
	///     form, post, get, cookie or oneurl
	/// </summary>
	[YamlMember(Alias = "method")]
	public string Method { get; init; } = "form";

	/// <summary>
	///     Selector of the login form for method form
	/// </summary>
	[YamlMember(Alias = "form")]
	public string? Form { get; init; }

	/// <summary>
	///     Static form fields, template rendered
	/// </summary>
	[YamlMember(Alias = "inputs")]
	public Dictionary<string, string> Inputs { get; init; } = [];

	/// <summary>
	///     Form fields sourced from page elements, selector to field name
	/// </summary>
	[YamlMember(Alias = "selectorinputs")]
	public Dictionary<string, string> SelectorInputs { get; init; } = [];

	/// <summary>
	///     Query parameters of the login request for method get
	/// </summary>
	[YamlMember(Alias = "getselectorinputs")]
	public Dictionary<string, string> GetSelectorInputs { get; init; } = [];

	/// <summary>
	///     Overrides the submit target of the login form
	/// </summary>
	[YamlMember(Alias = "submitpath")]
	public string? SubmitPath { get; init; }

	/// <summary>
	///     Selectors that, when found on the login response, mean the login failed
	/// </summary>
	[YamlMember(Alias = "error")]
	public List<CardigannLoginError> Error { get; init; } = [];

	/// <summary>
	///     A check that verifies the login succeeded
	/// </summary>
	[YamlMember(Alias = "test")]
	public CardigannLoginTest? Test { get; init; }

	/// <summary>
	///     The name of the setting field holding the raw cookie string for method cookie
	/// </summary>
	[YamlMember(Alias = "cookie")]
	public string? Cookie { get; init; }
}

/// <summary>
///     A login failure detector
/// </summary>
public record CardigannLoginError
{
	/// <summary>
	///     The selector of the error message element
	/// </summary>
	[YamlMember(Alias = "selector")]
	public string Selector { get; init; } = string.Empty;

	/// <summary>
	///     The error message reported to the user
	/// </summary>
	[YamlMember(Alias = "message")]
	public string? Message { get; init; }
}

/// <summary>
///     A login success check
/// </summary>
public record CardigannLoginTest
{
	/// <summary>
	///     The path to request
	/// </summary>
	[YamlMember(Alias = "path")]
	public string Path { get; init; } = string.Empty;

	/// <summary>
	///     A selector that must match on the response
	/// </summary>
	[YamlMember(Alias = "selector")]
	public string Selector { get; init; } = string.Empty;
}
