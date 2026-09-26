using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Submarine.Core.Indexers;

namespace Submarine.Infrastructure.Indexers.Cardigann;

/// <summary>
///     Loads bundled and user supplied Cardigann definitions from the definitions folders
/// </summary>
/// <param name="logger">The logger</param>
/// <param name="bundledPath">Overrides the bundled definitions folder, for tests</param>
/// <param name="appDataPath">Overrides the app data definitions folder, for tests</param>
public sealed class IndexerDefinitionLoader(
	ILogger<IndexerDefinitionLoader> logger,
	string? bundledPath = null,
	string? appDataPath = null)
{
	/// <summary>
	///     The bundled definitions folder next to the application
	/// </summary>
	public string BundledPath { get; } = bundledPath ?? Path.Combine(AppContext.BaseDirectory, "definitions");

	/// <summary>
	///     The user supplied definitions folder inside the app data directory
	/// </summary>
	public string AppDataPath { get; } = appDataPath ?? ResolveAppDataPath();

	private static string ResolveAppDataPath()
	{
		var baseData = Environment.GetEnvironmentVariable("SUBMARINE__DATA")
			?? Path.Combine(Environment.CurrentDirectory, "data");
		return Path.Combine(baseData, "definitions");
	}

	/// <summary>
	///     Loads all definitions, app data definitions win over bundled ones on id conflicts
	/// </summary>
	/// <returns>The loaded definitions</returns>
	public IReadOnlyList<CardigannDefinition> LoadAll()
	{
		var byId = new Dictionary<string, CardigannDefinition>(StringComparer.Ordinal);
		foreach (var (path, source) in EnumerateFiles(BundledPath).Select(path => (path, "bundled"))
			.Concat(EnumerateFiles(AppDataPath).Select(path => (path, "app data"))))
		{
			CardigannDefinition definition;
			try
			{
				var yaml = File.ReadAllText(path);
				definition = CardigannDefinitionParser.Parse(yaml, Path.GetFileNameWithoutExtension(path));
			}
			catch (Exception exception) when (exception is IndexerException or IOException)
			{
				logger.LogWarning(exception, "Definition {Path} could not be loaded", path);
				continue;
			}

			if (byId.TryGetValue(definition.Id, out var existing) && source != "app data")
				continue;

			byId[definition.Id] = definition;
		}

		return [.. byId.Values.OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)];
	}

	/// <summary>
	///     Loads one definition by id, app data wins over bundled
	/// </summary>
	/// <param name="id">The definition id</param>
	/// <returns>The definition, or null when unknown</returns>
	public CardigannDefinition? Load(string id)
	{
		foreach (var path in new[] { Path.Combine(AppDataPath, id + ".yml"), Path.Combine(BundledPath, id + ".yml") })
		{
			if (!File.Exists(path))
				continue;

			try
			{
				return CardigannDefinitionParser.Parse(File.ReadAllText(path), id);
			}
			catch (Exception exception) when (exception is IndexerException or IOException)
			{
				logger.LogWarning(exception, "Definition {Path} could not be loaded", path);
				return null;
			}
		}

		return null;
	}

	private static IEnumerable<string> EnumerateFiles(string path)
		=> Directory.Exists(path)
			? Directory.EnumerateFiles(path, "*.yml", SearchOption.TopDirectoryOnly)
			: [];
}
