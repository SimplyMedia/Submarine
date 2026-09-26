using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Core.Parser;
using Submarine.Core.Parser.Release;
using Submarine.Core.Quality;
using Submarine.Core.Release;

namespace Submarine.Infrastructure.Tests.Import;

/// <summary>
///     Builds the real frozen <see cref="IParser{BaseRelease}" /> stack for tests, so the import pipeline is
///     exercised against actual parsing behaviour rather than a stub.
/// </summary>
public static class TestReleaseParserFactory
{
	/// <summary>Creates a parser instance backed by the frozen parser stack.</summary>
	public static IParser<BaseRelease> Create()
		=> new ReleaseParserService(
			NullLogger<ReleaseParserService>.Instance,
			new LanguageParserService(NullLogger<LanguageParserService>.Instance),
			new StreamingProviderParserService(NullLogger<StreamingProviderParserService>.Instance),
			new QualityParserService(NullLogger<QualityParserService>.Instance),
			new ReleaseGroupParserService(NullLogger<ReleaseGroupParserService>.Instance),
			new TestQualityOverrideSource());

	private sealed class TestQualityOverrideSource : IQualityOverrideSource
	{
		public IReadOnlyDictionary<string, QualitySource> Overrides { get; } = QualityEdgeCasesConstants.EdgeCaseReleaseGroupQualitySourceMapping;
	}
}
