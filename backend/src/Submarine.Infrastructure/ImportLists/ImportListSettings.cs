using System.Text.Json;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.ImportLists;

/// <summary>
///     Helpers for reading import list settings stored as JSON.
/// </summary>
public static class ImportListSettings
{
	/// <summary>Parse the settings JSON of a list.</summary>
	public static JsonDocument Parse(ImportList list)
	{
		try
		{
			return JsonDocument.Parse(string.IsNullOrWhiteSpace(list.SettingsJson) ? "{}" : list.SettingsJson);
		}
		catch (JsonException)
		{
			throw new InvalidOperationException($"Import list '{list.Name}' has invalid settings JSON");
		}
	}

	/// <summary>Read a string setting.</summary>
	public static string? String(JsonDocument settings, string name)
		=> settings.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;

	/// <summary>Read a required string setting.</summary>
	public static string RequireString(JsonDocument settings, string name, string listName)
		=> String(settings, name)
			?? throw new InvalidOperationException($"Import list '{listName}' is missing the '{name}' setting");

	/// <summary>Read an int setting.</summary>
	public static int? Int(JsonDocument settings, string name)
		=> settings.RootElement.TryGetProperty(name, out var value)
			&& value.ValueKind == JsonValueKind.Number
			&& value.TryGetInt32(out var number)
				? number
				: null;

	/// <summary>Read a required int setting.</summary>
	public static int RequireInt(JsonDocument settings, string name, string listName)
		=> Int(settings, name)
			?? throw new InvalidOperationException($"Import list '{listName}' is missing the '{name}' setting");

	/// <summary>Read a string setting with a default.</summary>
	public static string StringOr(JsonDocument settings, string name, string fallback)
		=> String(settings, name) is { Length: > 0 } value ? value : fallback;
}
