using Submarine.Api.Exceptions;
using Submarine.Api.Repository;
using Submarine.Core.DecisionEngine;
using Submarine.Core.DecisionEngine.CustomFormats;
using Submarine.Core.DecisionEngine.Filter;
using Submarine.Core.Languages;
using Submarine.Core.Library;
using Submarine.Core.Profile;
using Submarine.Core.Quality;

namespace Submarine.Api.Services;

/// <summary>
///     Assembles the <see cref="MediaContext" /> a download decision is made in, shared by manual search and RSS sync
/// </summary>
public class MediaContextFactory
{
	private readonly ISeriesRepository _seriesRepository;
	private readonly IQualityProfileRepository _qualityProfileRepository;
	private readonly ILanguageProfileRepository _languageProfileRepository;
	private readonly IReleaseFilterRepository _filterRepository;
	private readonly ICustomFormatRepository _formatRepository;

	public MediaContextFactory(ISeriesRepository seriesRepository,
		IQualityProfileRepository qualityProfileRepository, ILanguageProfileRepository languageProfileRepository,
		IReleaseFilterRepository filterRepository, ICustomFormatRepository formatRepository)
	{
		_seriesRepository = seriesRepository;
		_qualityProfileRepository = qualityProfileRepository;
		_languageProfileRepository = languageProfileRepository;
		_filterRepository = filterRepository;
		_formatRepository = formatRepository;
	}

	public async Task<IReadOnlyCollection<ReleaseFilter>> LoadFiltersAsync()
		=> (await _filterRepository.FindAllAsync()).Select(f => f.ToFilter()).ToList();

	public async Task<IReadOnlyCollection<CustomFormat>> LoadFormatsAsync()
		=> (await _formatRepository.FindAllAsync()).Select(f => f.ToFormat()).ToList();

	public async Task<MediaContext> BuildSeriesContextAsync(MediaVersion version, int seriesId, int season,
		QualityModel? existingFileQuality, IReadOnlyList<Language>? existingFileLanguages,
		IReadOnlyCollection<ReleaseFilter> filters, IReadOnlyCollection<CustomFormat> formats)
	{
		var qualityProfile = await GetQualityProfileAsync(version.QualityProfileId);

		var seasonFiles = await _seriesRepository.FindEpisodeFilesBySeasonAsync(seriesId, season, version.Id);
		var seasonReleaseGroup = seasonFiles
			.Select(f => f.ReleaseGroup)
			.Where(group => group != null)
			.GroupBy(group => group)
			.OrderByDescending(group => group.Count())
			.Select(group => group.Key)
			.FirstOrDefault();

		return new MediaContext
		{
			QualityProfile = qualityProfile,
			LanguageProfile = await GetLanguageProfileAsync(version.LanguageProfileId),
			Filters = filters,
			CustomFormats = formats,
			CustomFormatScores = qualityProfile.FormatScores,
			ExistingFileQuality = existingFileQuality,
			ExistingFileLanguages = existingFileLanguages,
			SeasonReleaseGroup = seasonReleaseGroup
		};
	}

	public async Task<MediaContext> BuildMovieContextAsync(MediaVersion version, QualityModel? existingFileQuality,
		IReadOnlyList<Language>? existingFileLanguages, IReadOnlyCollection<ReleaseFilter> filters,
		IReadOnlyCollection<CustomFormat> formats)
	{
		var qualityProfile = await GetQualityProfileAsync(version.QualityProfileId);

		return new MediaContext
		{
			QualityProfile = qualityProfile,
			LanguageProfile = await GetLanguageProfileAsync(version.LanguageProfileId),
			Filters = filters,
			CustomFormats = formats,
			CustomFormatScores = qualityProfile.FormatScores,
			ExistingFileQuality = existingFileQuality,
			ExistingFileLanguages = existingFileLanguages
		};
	}

	private async Task<QualityProfile> GetQualityProfileAsync(int id)
	{
		var profile = await _qualityProfileRepository.FirstByConditionAsync(p => p.Id == id);

		if (profile == null)
			throw new NotFoundException();

		return profile;
	}

	private async Task<LanguageProfile> GetLanguageProfileAsync(int id)
	{
		var profile = await _languageProfileRepository.FirstByConditionAsync(p => p.Id == id);

		if (profile == null)
			throw new NotFoundException();

		return profile;
	}
}
