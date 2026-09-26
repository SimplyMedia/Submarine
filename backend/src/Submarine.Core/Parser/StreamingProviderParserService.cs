using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Attributes;
using Submarine.Core.Quality;
using Submarine.Core.Util.Extensions;

namespace Submarine.Core.Parser;

/// <summary>
///     Service which parses the Streaming Provider of releases
/// </summary>
public class StreamingProviderParserService : IParser<StreamingProvider?>
{
	private readonly ILogger<StreamingProviderParserService> _logger;

	private readonly Dictionary<StreamingProvider, Regex> _streamingProviderRegexes;

	/// <summary>
	///     Creates a new <see cref="StreamingProviderParserService" />
	/// </summary>
	/// <param name="logger">The logger of this <see cref="StreamingProviderParserService" /></param>
	/// <exception cref="InvalidOperationException">if an StreamingProvider enum member has no Regex Attribute present</exception>
	public StreamingProviderParserService(ILogger<StreamingProviderParserService> logger)
	{
		_logger = logger;
		_streamingProviderRegexes = new Dictionary<StreamingProvider, Regex>();
		foreach (var provider in Enum.GetValues<StreamingProvider>())
		{
			var regex = provider.GetAttribute<RegExAttribute>()?.Regex;

			if (regex == null)
				throw new InvalidOperationException($"Missing Regex for StreamingProvider {provider}");

			_streamingProviderRegexes.Add(provider, regex);
		}
	}

	/// <summary>
	///     Parses the Streaming Provider of a release, if any is present
	/// </summary>
	/// <param name="input">The Release name</param>
	/// <returns>The parsed Streaming Provider, if any</returns>
	public StreamingProvider? Parse(string input)
	{
		_logger.LogDebug("Trying to parse Streaming Provider for {Input}", input);

		return ParseStreamingProvider(input.Trim());
	}

	private StreamingProvider? ParseStreamingProvider(string input)
	{
		foreach (var (provider, regex) in _streamingProviderRegexes)
		{
			if (!regex.IsMatch(input)) continue;
			_logger.LogDebug("{Input} matched Regex for {StreamingProvider}", input, provider);
			return provider;
		}

		_logger.LogDebug("{Input} didn't match any Regex for StreamingProviders", input);

		return null;
	}
}
