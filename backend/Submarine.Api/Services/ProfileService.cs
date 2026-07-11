using Submarine.Api.Exceptions;
using Submarine.Api.Extensions;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;
using Submarine.Core.Languages;
using Submarine.Core.Profile;
using Submarine.Core.Quality;

namespace Submarine.Api.Services;

public class ProfileService
{
	private readonly IQualityProfileRepository _qualityProfileRepository;
	private readonly ILanguageProfileRepository _languageProfileRepository;

	public ProfileService(IQualityProfileRepository qualityProfileRepository,
		ILanguageProfileRepository languageProfileRepository)
	{
		_qualityProfileRepository = qualityProfileRepository;
		_languageProfileRepository = languageProfileRepository;
	}

	/// <summary>
	///     Seeds the default Quality Profile "Any" and Language Profile "English" if they do not already exist
	/// </summary>
	public async Task SeedDefaultsAsync()
	{
		var qualityProfiles = await _qualityProfileRepository.FindAllAsync();

		if (qualityProfiles.Count == 0)
		{
			var items = QualityResolutionModel.All
				.Select(q => new QualityProfileItem { Quality = q, Allowed = true })
				.ToList();

			await _qualityProfileRepository.CreateAsync(new QualityProfile
			{
				Name = "Any",
				UpgradeAllowed = true,
				Cutoff = items.Count - 1,
				Items = items
			});
		}

		var languageProfiles = await _languageProfileRepository.FindAllAsync();

		if (languageProfiles.Count == 0)
			await _languageProfileRepository.CreateAsync(new LanguageProfile
			{
				Name = "English",
				Languages = new List<Language> { Language.ENGLISH },
				Cutoff = Language.ENGLISH,
				UpgradeAllowed = false
			});
	}

	public Task<PagedResult<QualityProfile>> GetAllQualityProfilesAsync(int page, int pageSize)
		=> _qualityProfileRepository.Query().ToPagedResultAsync(page, pageSize);

	public async Task<QualityProfile> GetQualityProfileAsync(int id)
	{
		var profile = await _qualityProfileRepository.FirstByConditionAsync(p => p.Id == id);

		if (profile == null)
			throw new NotFoundException();

		return profile;
	}

	public async Task<QualityProfile> CreateQualityProfileAsync(CreateQualityProfileRequest request)
	{
		var profile = new QualityProfile
		{
			Name = request.Name,
			UpgradeAllowed = request.UpgradeAllowed,
			Cutoff = request.Cutoff,
			Items = request.Items
		};

		await _qualityProfileRepository.CreateAsync(profile);

		return profile;
	}

	public async Task<QualityProfile> UpdateQualityProfileAsync(int id, UpdateQualityProfileRequest request)
	{
		var profile = await GetQualityProfileAsync(id);

		profile.Name = request.Name;
		profile.UpgradeAllowed = request.UpgradeAllowed;
		profile.Cutoff = request.Cutoff;
		profile.Items = request.Items;

		await _qualityProfileRepository.UpdateAsync(profile);

		return profile;
	}

	public async Task<QualityProfile> DeleteQualityProfileAsync(int id)
	{
		var profile = await GetQualityProfileAsync(id);

		await _qualityProfileRepository.DeleteAsync(profile);

		return profile;
	}

	public Task<PagedResult<LanguageProfile>> GetAllLanguageProfilesAsync(int page, int pageSize)
		=> _languageProfileRepository.Query().ToPagedResultAsync(page, pageSize);

	public async Task<LanguageProfile> GetLanguageProfileAsync(int id)
	{
		var profile = await _languageProfileRepository.FirstByConditionAsync(p => p.Id == id);

		if (profile == null)
			throw new NotFoundException();

		return profile;
	}

	public async Task<LanguageProfile> CreateLanguageProfileAsync(CreateLanguageProfileRequest request)
	{
		var profile = new LanguageProfile
		{
			Name = request.Name,
			Languages = request.Languages,
			Cutoff = request.Cutoff,
			UpgradeAllowed = request.UpgradeAllowed
		};

		await _languageProfileRepository.CreateAsync(profile);

		return profile;
	}

	public async Task<LanguageProfile> UpdateLanguageProfileAsync(int id, UpdateLanguageProfileRequest request)
	{
		var profile = await GetLanguageProfileAsync(id);

		profile.Name = request.Name;
		profile.Languages = request.Languages;
		profile.Cutoff = request.Cutoff;
		profile.UpgradeAllowed = request.UpgradeAllowed;

		await _languageProfileRepository.UpdateAsync(profile);

		return profile;
	}

	public async Task<LanguageProfile> DeleteLanguageProfileAsync(int id)
	{
		var profile = await GetLanguageProfileAsync(id);

		await _languageProfileRepository.DeleteAsync(profile);

		return profile;
	}
}
