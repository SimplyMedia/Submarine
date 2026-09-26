using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Submarine.Core.Indexers;

namespace Submarine.Infrastructure.Indexers.Cardigann;

/// <summary>
///     Syncs Cardigann definitions from the Prowlarr definition repository
/// </summary>
/// <param name="httpClient">The http client for the definition index and files</param>
/// <param name="loader">The definition loader providing the app data target folder</param>
/// <param name="logger">The logger</param>
public sealed class IndexerDefinitionSyncClient(
	HttpClient httpClient,
	IndexerDefinitionLoader loader,
	ILogger<IndexerDefinitionSyncClient> logger)
{
	private const string IndexUrl = "https://indexers.prowlarr.com/master/11";
	private const string DefinitionUrlPrefix = "https://indexers.prowlarr.com/master/11/";
	private const string GitHubRawPrefix = "https://raw.githubusercontent.com/Prowlarr/Indexers/master/definitions/v11/";

	/// <summary>
	///     Lists the definitions available upstream
	/// </summary>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The available definitions without their YAML bodies</returns>
	public async Task<IReadOnlyList<IndexerDefinitionInfo>> GetAvailableAsync(CancellationToken cancellationToken = default)
	{
		var index = await FetchIndexAsync(cancellationToken).ConfigureAwait(false);
		return index
			.Select(entry => new IndexerDefinitionInfo(
				entry.Id,
				entry.Name,
				entry.Description ?? string.Empty,
				entry.Language ?? "en-US",
				entry.Type ?? "public",
				entry.Protocol ?? "torrent",
				entry.Links ?? [],
				[],
				ParseSettings(entry.Settings),
				[]))
			.ToList();
	}

	/// <summary>
	///     Downloads every upstream definition into the app data definitions folder
	/// </summary>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The amount of definitions written</returns>
	public async Task<int> SyncAsync(CancellationToken cancellationToken = default)
	{
		var index = await FetchIndexAsync(cancellationToken).ConfigureAwait(false);
		Directory.CreateDirectory(loader.AppDataPath);

		var written = 0;
		foreach (var entry in index)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var yaml = await FetchDefinitionAsync(entry, cancellationToken).ConfigureAwait(false);
			if (yaml is null)
				continue;

			var target = Path.Combine(loader.AppDataPath, entry.Id + ".yml");
			try
			{
				await File.WriteAllTextAsync(target, yaml, cancellationToken).ConfigureAwait(false);
				written++;
			}
			catch (IOException exception)
			{
				logger.LogWarning(exception, "Definition {Id} could not be written", entry.Id);
			}
		}

		return written;
	}

	private async Task<List<IndexerIndexEntry>> FetchIndexAsync(CancellationToken cancellationToken)
	{
		using var response = await httpClient.GetAsync(IndexUrl, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			return JsonSerializer.Deserialize<List<IndexerIndexEntry>>(body) ?? [];
		}
		catch (JsonException exception)
		{
			throw new IndexerException($"The definition index could not be parsed: {exception.Message}", exception);
		}
	}

	private async Task<string?> FetchDefinitionAsync(IndexerIndexEntry entry, CancellationToken cancellationToken)
	{
		foreach (var url in new[]
		{
			DefinitionUrlPrefix + (string.IsNullOrEmpty(entry.File) ? entry.Id : entry.File),
			GitHubRawPrefix + entry.Id + ".yml"
		})
		{
			try
			{
				using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
				if (!response.IsSuccessStatusCode)
					continue;

				var yaml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				if (yaml.TrimStart().StartsWith("---", StringComparison.Ordinal) || yaml.Contains("name:", StringComparison.Ordinal))
					return yaml;
			}
			catch (HttpRequestException exception)
			{
				logger.LogWarning(exception, "Definition {Id} could not be fetched from {Url}", entry.Id, url);
			}
		}

		logger.LogWarning("Definition {Id} could not be fetched from any source", entry.Id);
		return null;
	}

	private static List<IndexerSettingField> ParseSettings(List<IndexerIndexSetting>? settings)
		=> settings?.Select(setting => new IndexerSettingField(
			setting.Name ?? string.Empty,
			setting.Type?.ToLowerInvariant() switch
			{
				"password" => IndexerSettingType.PASSWORD,
				"checkbox" => IndexerSettingType.CHECKBOX,
				"select" => IndexerSettingType.SELECT,
				"info" => IndexerSettingType.INFO,
				"cardiganncaptcha" => IndexerSettingType.CARDIGANNCAPTCHA,
				_ => IndexerSettingType.TEXT
			},
			setting.Label,
			Convert.ToString(setting.Default, System.Globalization.CultureInfo.InvariantCulture),
			null)).ToList() ?? [];

	private sealed record IndexerIndexEntry(
		[property: JsonPropertyName("id")] string Id,
		[property: JsonPropertyName("file")] string? File,
		[property: JsonPropertyName("name")] string Name,
		[property: JsonPropertyName("description")] string? Description,
		[property: JsonPropertyName("language")] string? Language,
		[property: JsonPropertyName("type")] string? Type,
		[property: JsonPropertyName("protocol")] string? Protocol,
		[property: JsonPropertyName("links")] List<string>? Links,
		[property: JsonPropertyName("settings")] List<IndexerIndexSetting>? Settings);

	private sealed record IndexerIndexSetting(
		[property: JsonPropertyName("name")] string? Name,
		[property: JsonPropertyName("type")] string? Type,
		[property: JsonPropertyName("label")] string? Label,
		[property: JsonPropertyName("default")] JsonElement? Default);
}
