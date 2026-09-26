using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Api.Features.QualityOverrides;
using Submarine.Core.CustomFormats;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Languages;
using Submarine.Core.Modules;
using Submarine.Core.Naming;
using Submarine.Core.Parser;
using Submarine.Core.Parser.Release;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Release.Usenet;
using Submarine.Core.Validator;

namespace Submarine.Api.Features.Parse;

/// <summary>
///     Registers the frozen parser stack, the decision engine, naming and the quality override source.
/// </summary>
public sealed class ParserServiceModule : IServiceModule
{
	/// <inheritdoc />
	public void Register(IServiceCollection services, IConfiguration configuration)
	{
		services.AddMemoryCache();

		// frozen parser stack, singletons so regexes and metadata are built once
		services.AddSingleton<IParser<IReadOnlyList<Language>>, LanguageParserService>();
		services.AddSingleton<IParser<StreamingProvider?>, StreamingProviderParserService>();
		services.AddSingleton<IParser<QualityModel>, QualityParserService>();
		services.AddSingleton<IParser<string?>, ReleaseGroupParserService>();
		services.AddSingleton<TorrentReleaseValidatorService>();
		services.AddSingleton<UsenetReleaseValidatorService>();
		services.AddSingleton<IParser<BaseRelease>, ReleaseParserService>();
		services.AddSingleton<IParser<TorrentRelease>, TorrentReleaseParserService>();
		services.AddSingleton<IParser<UsenetRelease>, UsenetReleaseParserService>();

		services.AddSingleton<Submarine.Api.Features.QualityOverrides.QualityOverrideSource>();
		services.AddSingleton<IQualityOverrideSource>(serviceProvider => serviceProvider.GetRequiredService<Submarine.Api.Features.QualityOverrides.QualityOverrideSource>());

		services.AddSingleton<ReleaseFilterEvaluator>();
		services.AddSingleton<IDownloadDecisionMaker, DownloadDecisionMaker>();
		services.AddSingleton<IReleaseComparer, ReleaseComparer>();

		services.AddSingleton<NamingService>();
		services.AddSingleton<IFolderNameRenderer>(serviceProvider => serviceProvider.GetRequiredService<NamingService>());
	}
}
