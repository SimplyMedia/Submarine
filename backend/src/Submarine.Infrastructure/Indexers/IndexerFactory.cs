using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.Indexers.Cardigann;
using Submarine.Infrastructure.Indexers.Torznab;

namespace Submarine.Infrastructure.Indexers;

/// <summary>
///     Creates configured indexers from their implementation and settings json
/// </summary>
/// <param name="httpClientFactory">The indexer http client factory</param>
/// <param name="definitionLoader">The definition loader for Cardigann indexers</param>
/// <param name="loggerFactory">The logger factory</param>
public sealed class IndexerFactory(
	IndexerHttpClientFactory httpClientFactory,
	IndexerDefinitionLoader definitionLoader,
	ILoggerFactory loggerFactory) : IIndexerFactory
{
	/// <inheritdoc />
	public IIndexer Create(
		IndexerImplementation implementation,
		string settingsJson,
		IndexerProxySettings? proxy = null,
		IndexerDefinitionInfo? definition = null)
	{
		var errors = IndexerSettingsJson.Validate(implementation, settingsJson);
		if (errors.Count > 0)
			throw new IndexerException($"Invalid indexer settings: {string.Join(", ", errors)}");

		switch (implementation)
		{
			case IndexerImplementation.TORZNAB:
			{
				var settings = IndexerSettingsJson.Deserialize<TorznabSettings>(settingsJson);
				return new TorznabIndexer(
					settings,
					httpClientFactory.Create(settings.BaseUrl, 2, proxy),
					loggerFactory.CreateLogger<TorznabIndexer>());
			}
			case IndexerImplementation.NEWZNAB:
			{
				var settings = IndexerSettingsJson.Deserialize<NewznabSettings>(settingsJson);
				return new NewznabIndexer(
					settings,
					httpClientFactory.Create(settings.BaseUrl, 2, proxy),
					loggerFactory.CreateLogger<NewznabIndexer>());
			}
			case IndexerImplementation.CARDIGANN:
			{
				var settings = IndexerSettingsJson.Deserialize<CardigannSettings>(settingsJson);
				var cardigann = definitionLoader.Load(settings.DefinitionId)
					?? throw new IndexerException($"Cardigann definition {settings.DefinitionId} is unknown");
				return new CardigannIndexer(
					cardigann,
					settings,
					httpClientFactory.Create(
						cardigann.Id,
						cardigann.RequestDelay ?? 2,
						proxy),
					loggerFactory.CreateLogger<CardigannIndexer>());
			}
			default:
				throw new IndexerException($"Indexer implementation {implementation} is not supported");
		}
	}

	/// <inheritdoc />
	public IReadOnlyList<string> Validate(IndexerImplementation implementation, string settingsJson)
		=> IndexerSettingsJson.Validate(implementation, settingsJson);
}

/// <summary>
///     Factory contract for indexers
/// </summary>
public interface IIndexerFactory
{
	/// <summary>
	///     Creates an indexer
	/// </summary>
	/// <param name="implementation">The implementation kind</param>
	/// <param name="settingsJson">The serialized settings of the implementation</param>
	/// <param name="proxy">The proxy to route requests through, if any</param>
	/// <param name="definition">The definition metadata, informational for Cardigann</param>
	/// <returns>The configured indexer</returns>
	IIndexer Create(
		IndexerImplementation implementation,
		string settingsJson,
		IndexerProxySettings? proxy = null,
		IndexerDefinitionInfo? definition = null);

	/// <summary>
	///     Validates settings without creating an indexer
	/// </summary>
	/// <param name="implementation">The implementation kind</param>
	/// <param name="settingsJson">The serialized settings</param>
	/// <returns>The field errors, empty when valid</returns>
	IReadOnlyList<string> Validate(IndexerImplementation implementation, string settingsJson);
}

/// <summary>
///     Deserializes and validates the per implementation settings json
/// </summary>
public static class IndexerSettingsJson
{
	/// <summary>
	///     Deserializes settings of the given type
	/// </summary>
	/// <param name="settingsJson">The json text, empty object when null</param>
	/// <typeparam name="T">The settings record type</typeparam>
	/// <returns>The settings</returns>
	public static T Deserialize<T>(string? settingsJson)
		where T : notnull
		=> JsonSerializer.Deserialize<T>(string.IsNullOrWhiteSpace(settingsJson) ? "{}" : settingsJson, Options)
			?? throw new IndexerException("Indexer settings could not be deserialized");

	/// <summary>
	///     Validates settings against the implementation
	/// </summary>
	/// <param name="implementation">The implementation kind</param>
	/// <param name="settingsJson">The json text</param>
	/// <returns>The field errors, empty when valid</returns>
	public static IReadOnlyList<string> Validate(IndexerImplementation implementation, string? settingsJson)
	{
		try
		{
			return implementation switch
			{
				IndexerImplementation.TORZNAB => ValidateTorznab(Deserialize<TorznabSettings>(settingsJson)),
				IndexerImplementation.NEWZNAB => ValidateNewznab(Deserialize<NewznabSettings>(settingsJson)),
				IndexerImplementation.CARDIGANN => ValidateCardigann(Deserialize<CardigannSettings>(settingsJson)),
				_ => [$"Implementation {implementation} is not supported"]
			};
		}
		catch (JsonException exception)
		{
			return [$"Settings are not valid json: {exception.Message}"];
		}
		catch (IndexerException exception)
		{
			return [exception.Message];
		}
	}

	private static IReadOnlyList<string> ValidateTorznab(TorznabSettings settings)
	{
		var errors = ValidateBaseUrl(settings.BaseUrl, "Torznab");
		if (settings.MinimumSeeders < 0)
			errors.Add("MinimumSeeders must not be negative");
		return errors;
	}

	private static IReadOnlyList<string> ValidateNewznab(NewznabSettings settings)
		=> ValidateBaseUrl(settings.BaseUrl, "Newznab");

	private static IReadOnlyList<string> ValidateCardigann(CardigannSettings settings)
	{
		var errors = new List<string>();
		if (string.IsNullOrWhiteSpace(settings.DefinitionId))
			errors.Add("DefinitionId is required");
		if (settings.BaseUrl is { } baseUrl)
			ValidateBaseUrl(baseUrl, "Cardigann", errors);
		return errors;
	}

	private static List<string> ValidateBaseUrl(string? baseUrl, string kind, List<string>? errors = null)
	{
		errors ??= [];
		if (string.IsNullOrWhiteSpace(baseUrl))
			errors.Add($"{kind} BaseUrl is required");
		else if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
			errors.Add($"{kind} BaseUrl must be an absolute http(s) url");
		return errors;
	}

	private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
