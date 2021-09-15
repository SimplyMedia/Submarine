using System;
using Microsoft.Extensions.Logging;
using Submarine.Core.Quality;
using Submarine.Core.Quality.Attributes;
using Submarine.Core.Util.Extensions;

namespace Submarine.Core.Parser
{
	public class StreamingProviderParserService : IParser<StreamingProvider?>
	{
		private readonly ILogger<StreamingProviderParserService> _logger;

		public StreamingProviderParserService(ILogger<StreamingProviderParserService> logger)
			=> _logger = logger;
		
		public StreamingProvider? Parse(string input)
		{
			_logger.LogDebug($"Trying to parse Streaming Provider for {input}");

			return ParseStreamingProvider(input.Trim());
		}

		private static StreamingProvider? ParseStreamingProvider(string input)
		{
			foreach (var provider in Enum.GetValues<StreamingProvider>())
			{
				var regex = provider.GetAttribute<RegExAttribute>()?.Regex;

				if (regex == null)
					throw new InvalidOperationException($"Missing Regex for StreamingProvider {provider}");

				if (regex.IsMatch(input))
					return provider;
			}

			return null;
		}
	}
}
