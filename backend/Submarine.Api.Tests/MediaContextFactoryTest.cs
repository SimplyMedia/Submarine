using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.DecisionEngine.Filter;
using Submarine.Core.Languages;
using Submarine.Core.Library;
using Submarine.Core.Profile;
using Submarine.Core.Provider;
using Xunit;

namespace Submarine.Api.Tests;

public class MediaContextFactoryTest : DatabaseTestBase
{
	[Fact]
	public async Task BuildMovieContextAsync_ShouldPickLowestOrderTagMatchingDelayProfile_WhenTagsIntersect()
	{
		await SeedProfilesAsync();

		Context.DelayProfiles.AddRange(
			DelayProfile("Default", order: int.MaxValue),
			DelayProfile("Anime Slow", order: 5, "anime"),
			DelayProfile("Anime Fast", order: 1, "anime"),
			DelayProfile("HD", order: 0, "hd"));
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		var context = await BuildAsync(Movie("anime"));

		Assert.Equal("Anime Fast", context.DelayProfile!.Name);
	}

	[Fact]
	public async Task BuildMovieContextAsync_ShouldFallBackToDefaultDelayProfile_WhenNoTagsIntersect()
	{
		await SeedProfilesAsync();

		Context.DelayProfiles.AddRange(
			DelayProfile("Default", order: int.MaxValue),
			DelayProfile("Anime", order: 1, "anime"));
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		var context = await BuildAsync(Movie("other"));

		Assert.Equal("Default", context.DelayProfile!.Name);
	}

	[Fact]
	public async Task BuildMovieContextAsync_ShouldTagFilterReleaseProfiles_WhenBuildingContext()
	{
		await SeedProfilesAsync();

		Context.ReleaseProfiles.AddRange(
			ReleaseProfile("Global"),
			ReleaseProfile("Anime", "anime"),
			ReleaseProfile("HD", "hd"),
			ReleaseProfile("Disabled", enabled: false));
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		var context = await BuildAsync(Movie("anime"));

		Assert.Equal(new[] { "Anime", "Global" }, context.ReleaseProfiles.Select(p => p.Name).OrderBy(n => n));
	}

	private async Task SeedProfilesAsync()
	{
		Context.QualityProfiles.Add(new QualityProfile { Id = 1, Name = "Any" });
		Context.LanguageProfiles.Add(new LanguageProfile
		{
			Id = 1, Name = "English", Languages = new List<Language> { Language.ENGLISH }, Cutoff = Language.ENGLISH
		});
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);
	}

	private Task<Submarine.Core.DecisionEngine.MediaContext> BuildAsync(Movie movie)
	{
		var factory = new MediaContextFactory(new SeriesRepository(Context), new QualityProfileRepository(Context),
			new LanguageProfileRepository(Context), new ReleaseFilterRepository(Context),
			new CustomFormatRepository(Context), new DelayProfileRepository(Context),
			new ReleaseProfileRepository(Context));

		var version = new MediaVersion { QualityProfileId = 1, LanguageProfileId = 1 };

		return factory.BuildMovieContextAsync(movie, version, existingFileQuality: null, existingFileLanguages: null,
			Array.Empty<ReleaseFilter>(), Array.Empty<Submarine.Core.DecisionEngine.CustomFormats.CustomFormat>());
	}

	private static Movie Movie(params string[] tags)
		=> new() { Title = "Movie", Tags = tags.ToList() };

	private static DelayProfile DelayProfile(string name, int order, params string[] tags)
		=> new()
		{
			Name = name, PreferredProtocol = Protocol.BITTORRENT, Order = order, Tags = tags.ToList()
		};

	private static ReleaseProfile ReleaseProfile(string name, params string[] tags)
		=> new() { Name = name, Enabled = true, Tags = tags.ToList() };

	private static ReleaseProfile ReleaseProfile(string name, bool enabled)
		=> new() { Name = name, Enabled = enabled, Tags = new List<string>() };
}
