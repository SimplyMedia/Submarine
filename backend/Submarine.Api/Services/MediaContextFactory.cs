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
	private const int ReleasedPhysicalAvailabilityDelayDays = 45;

	private readonly ISeriesRepository _seriesRepository;
	private readonly IQualityProfileRepository _qualityProfileRepository;
	private readonly ILanguageProfileRepository _languageProfileRepository;
	private readonly IReleaseFilterRepository _filterRepository;
	private readonly ICustomFormatRepository _formatRepository;
	private readonly IDelayProfileRepository _delayProfileRepository;
	private readonly IReleaseProfileRepository _releaseProfileRepository;

	public MediaContextFactory(ISeriesRepository seriesRepository,
		IQualityProfileRepository qualityProfileRepository, ILanguageProfileRepository languageProfileRepository,
		IReleaseFilterRepository filterRepository, ICustomFormatRepository formatRepository,
		IDelayProfileRepository delayProfileRepository, IReleaseProfileRepository releaseProfileRepository)
	{
		_seriesRepository = seriesRepository;
		_qualityProfileRepository = qualityProfileRepository;
		_languageProfileRepository = languageProfileRepository;
		_filterRepository = filterRepository;
		_formatRepository = formatRepository;
		_delayProfileRepository = delayProfileRepository;
		_releaseProfileRepository = releaseProfileRepository;
	}

	public async Task<IReadOnlyCollection<ReleaseFilter>> LoadFiltersAsync()
		=> (await _filterRepository.FindAllAsync()).Select(f => f.ToFilter()).ToList();

	public async Task<IReadOnlyCollection<CustomFormat>> LoadFormatsAsync()
		=> (await _formatRepository.FindAllAsync()).Select(f => f.ToFormat()).ToList();

	public async Task<MediaContext> BuildSeriesContextAsync(MediaVersion version, int seriesId, int season,
		QualityModel? existingFileQuality, IReadOnlyList<Language>? existingFileLanguages,
		IReadOnlyCollection<ReleaseFilter> filters, IReadOnlyCollection<CustomFormat> formats,
		IReadOnlyCollection<string> tags)
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

		var (delayProfile, releaseProfiles) = await LoadProfilesAsync(tags);

		return new MediaContext
		{
			QualityProfile = qualityProfile,
			LanguageProfile = await GetLanguageProfileAsync(version.LanguageProfileId),
			Filters = filters,
			CustomFormats = formats,
			CustomFormatScores = qualityProfile.FormatScores,
			ExistingFileQuality = existingFileQuality,
			ExistingFileLanguages = existingFileLanguages,
			SeasonReleaseGroup = seasonReleaseGroup,
			DelayProfile = delayProfile,
			ReleaseProfiles = releaseProfiles
		};
	}

	public async Task<MediaContext> BuildMovieContextAsync(Movie movie, MediaVersion version,
		QualityModel? existingFileQuality, IReadOnlyList<Language>? existingFileLanguages,
		IReadOnlyCollection<ReleaseFilter> filters, IReadOnlyCollection<CustomFormat> formats)
	{
		var qualityProfile = await GetQualityProfileAsync(version.QualityProfileId);

		var (delayProfile, releaseProfiles) = await LoadProfilesAsync(movie.Tags);

		return new MediaContext
		{
			QualityProfile = qualityProfile,
			LanguageProfile = await GetLanguageProfileAsync(version.LanguageProfileId),
			Filters = filters,
			CustomFormats = formats,
			CustomFormatScores = qualityProfile.FormatScores,
			ExistingFileQuality = existingFileQuality,
			ExistingFileLanguages = existingFileLanguages,
			DelayProfile = delayProfile,
			ReleaseProfiles = releaseProfiles,
			MinimumAvailabilityMet = MinimumAvailabilityMet(movie, DateTimeOffset.UtcNow)
		};
	}

	// picks the lowest-Order Delay Profile whose Tags intersect the media's Tags, falling back to the default profile
	// with no Tags, and the enabled Release Profiles whose Tags are empty or intersect the media's Tags
	private async Task<(DelayProfile?, IReadOnlyCollection<ReleaseProfile>)> LoadProfilesAsync(
		IReadOnlyCollection<string> tags)
	{
		var delayProfiles = await _delayProfileRepository.FindAllAsync();
		var delayProfile = delayProfiles
			                   .Where(p => p.Tags.Count > 0 && p.Tags.Any(tags.Contains))
			                   .OrderBy(p => p.Order)
			                   .FirstOrDefault()
		                   ?? delayProfiles.FirstOrDefault(p => p.Tags.Count == 0);

		var releaseProfiles = (await _releaseProfileRepository.FindAllAsync())
			.Where(p => p.Enabled && (p.Tags.Count == 0 || p.Tags.Any(tags.Contains)))
			.ToList();

		return (delayProfile, releaseProfiles);
	}

	// Radarr-style availability heuristic: without physical/digital release dates a RELEASED movie is treated as
	// available a fixed number of days after its release date, approximating the typical gap to a home-media release
	private static bool MinimumAvailabilityMet(Movie movie, DateTimeOffset now)
		=> movie.MinimumAvailability switch
		{
			MinimumAvailability.ANNOUNCED => true,
			MinimumAvailability.IN_CINEMAS => movie.ReleaseDate is { } released && released <= now,
			MinimumAvailability.RELEASED => movie.ReleaseDate is { } released
			                                && released.AddDays(ReleasedPhysicalAvailabilityDelayDays) <= now,
			_ => true
		};

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
