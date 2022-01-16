using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Attributes;
using Submarine.Core.Quality;
using Submarine.Core.Quality.Attributes;
using Submarine.Core.Util.Extensions;

namespace Submarine.Core.Parser;

public class StreamingProviderParserService : IParser<StreamingProvider?>
{
	private readonly ILogger<StreamingProviderParserService> _logger;

	private readonly Dictionary<StreamingProvider, Regex> _streamingProviderRegexes;

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
