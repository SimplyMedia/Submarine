using Submarine.Core.DecisionEngine;
using Submarine.Core.Languages;
using Submarine.Core.Provider;

namespace Submarine.Api.Models.Response;

/// <summary>
///     A flattened <see cref="DownloadDecision" /> for the interactive search UI
/// </summary>
public record SearchDecisionResponse(
	string Title,
	string? Indexer,
	long? Size,
	int? Seeders,
	bool Approved,
	int Score,
	IReadOnlyList<string> Rejections,
	IReadOnlyList<string> MatchedCustomFormats,
	string? DownloadUrl,
	string Guid,
	DateTimeOffset? PublishDate,
	Protocol Protocol,
	string Quality,
	IReadOnlyList<Language> Languages,
	string? ReleaseGroup)
{
	public static SearchDecisionResponse FromDecision(DownloadDecision decision)
	{
		var info = decision.Candidate.Info;
		var release = decision.Candidate.Release;

		return new SearchDecisionResponse(
			info.Title,
			decision.Candidate.IndexerName,
			info.Size,
			info.Seeders,
			decision.Approved,
			decision.Score,
			decision.Rejections.Select(rejection => rejection.Reason).ToList(),
			decision.MatchedCustomFormats,
			info.DownloadUrl,
			info.Guid,
			info.PublishDate,
			info.Protocol,
			release.Quality.Resolution.Name,
			release.Languages,
			release.ReleaseGroup);
	}
}
