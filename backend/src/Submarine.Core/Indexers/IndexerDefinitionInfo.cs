using System;
using System.Collections.Generic;

namespace Submarine.Core.Indexers;

/// <summary>
///     User facing metadata of a Cardigann indexer definition, without the YAML body
/// </summary>
/// <param name="Id">The definition id, equals the file name</param>
/// <param name="Name">The tracker name</param>
/// <param name="Description">The tracker description</param>
/// <param name="Language">The tracker language, e.g. en-US</param>
/// <param name="Type">public, private or semi-private</param>
/// <param name="Protocol">torrent or usenet</param>
/// <param name="Links">The current base urls</param>
/// <param name="LegacyLinks">Former base urls</param>
/// <param name="Settings">The configurable fields</param>
/// <param name="Categories">The standard category ids covered by the mappings</param>
public record IndexerDefinitionInfo(
	string Id,
	string Name,
	string Description,
	string Language,
	string Type,
	string Protocol,
	IReadOnlyList<string> Links,
	IReadOnlyList<string> LegacyLinks,
	IReadOnlyList<IndexerSettingField> Settings,
	IReadOnlyList<int> Categories);

/// <summary>
///     A single configurable field of an indexer definition
/// </summary>
/// <param name="Name">The field name used in templates as Config.Name</param>
/// <param name="Type">The kind of field</param>
/// <param name="Label">The human readable label</param>
/// <param name="Default">The default value, if any</param>
/// <param name="Options">The selectable options for select fields</param>
public record IndexerSettingField(
	string Name,
	IndexerSettingType Type,
	string? Label = null,
	string? Default = null,
	IReadOnlyDictionary<string, string>? Options = null);

/// <summary>
///     The kind of a definition setting field
/// </summary>
public enum IndexerSettingType
{
	/// <summary>
	///     Single line text input
	/// </summary>
	TEXT,

	/// <summary>
	///     Masked text input
	/// </summary>
	PASSWORD,

	/// <summary>
	///     Boolean checkbox rendered as "true"/"false"
	/// </summary>
	CHECKBOX,

	/// <summary>
	///     Selection from fixed options
	/// </summary>
	SELECT,

	/// <summary>
	///     Informational text without input
	/// </summary>
	INFO,

	/// <summary>
	///     Marker that the definition works best with a FlareSolverr proxy
	/// </summary>
	CARDIGANNCAPTCHA
}
