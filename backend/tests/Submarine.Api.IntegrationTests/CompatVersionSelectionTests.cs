using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Submarine.Api.Features.Compat.Shared;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class CompatVersionSelectionTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public CompatVersionSelectionTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task FirstAccessPersistsLowestVersionAndTombstoneRequiresExplicitReactivation()
	{
		var ids = await SeedSeriesAsync();
		using var scope = _factory.Services.CreateScope();
		var selection = scope.ServiceProvider.GetRequiredService<CompatVersionSelection>();
		var selected = await selection.GetForSeriesAsync(ids.SeriesId);
		selected.ShouldNotBeNull();
		selected!.MediaVersionId.ShouldBe(ids.FirstVersionId);
		selected.SeriesId.ShouldBe(ids.SeriesId);
		selected.Excluded.ShouldBeFalse();

		var addedVersionId = await AddVersionAsync(ids.SeriesId, ids.RootFolderId, ids.QualityProfileId, ids.LanguageProfileId);
		(await selection.GetForSeriesAsync(ids.SeriesId))!.MediaVersionId.ShouldBe(ids.FirstVersionId);
		await selection.RebindSeriesBeforeVersionRemovalAsync(ids.SeriesId, ids.FirstVersionId);
		(await _factory.WithDbAsync(db => db.CompatLibraryBindings.AsNoTracking().SingleAsync(x => x.SeriesId == ids.SeriesId)))
			.MediaVersionId.ShouldBe(addedVersionId);

		await selection.ExcludeSeriesAsync(ids.SeriesId);
		(await selection.GetForSeriesAsync(ids.SeriesId)).ShouldBeNull();
		var excluded = await _factory.WithDbAsync(db => db.CompatLibraryBindings.AsNoTracking().SingleAsync(x => x.SeriesId == ids.SeriesId));
		excluded.Excluded.ShouldBeTrue();
		excluded.MediaVersionId.ShouldBeNull();

		await selection.BindSeriesVersionAsync(ids.SeriesId, ids.FirstVersionId);
		var reactivated = await selection.GetForSeriesAsync(ids.SeriesId);
		reactivated.ShouldNotBeNull();
		reactivated!.MediaVersionId.ShouldBe(ids.FirstVersionId);
		reactivated.Excluded.ShouldBeFalse();
		await selection.RebindSeriesBeforeVersionRemovalAsync(ids.SeriesId, ids.FirstVersionId);
		await _factory.WithDbAsync(async db =>
		{
			var firstVersion = await db.MediaVersions.SingleAsync(x => x.Id == ids.FirstVersionId);
			db.MediaVersions.Remove(firstVersion);
			await db.SaveChangesAsync();
			return true;
		});
		await selection.RebindSeriesBeforeVersionRemovalAsync(ids.SeriesId, addedVersionId);
		await _factory.WithDbAsync(async db =>
		{
			var finalVersion = await db.MediaVersions.SingleAsync(x => x.Id == addedVersionId);
			db.MediaVersions.Remove(finalVersion);
			await db.SaveChangesAsync();
			return true;
		});
		(await _factory.WithDbAsync(db => db.CompatLibraryBindings.AsNoTracking().SingleAsync(x => x.SeriesId == ids.SeriesId)))
			.MediaVersionId.ShouldBeNull();
		(await selection.GetForSeriesAsync(ids.SeriesId)).ShouldBeNull();

	}

	[Fact]
	public async Task BindingRejectsVersionFromDifferentTitle()
	{
		var left = await SeedSeriesAsync();
		var right = await SeedSeriesAsync();
		using var scope = _factory.Services.CreateScope();
		var selection = scope.ServiceProvider.GetRequiredService<CompatVersionSelection>();
		var error = await Should.ThrowAsync<InvalidOperationException>(() => selection.BindSeriesVersionAsync(left.SeriesId, right.FirstVersionId));
		error.Message.ShouldContain("does not belong");
	}

	private async Task<SeedIds> SeedSeriesAsync()
	{
		return await _factory.WithDbAsync(async db =>
		{
			var unique = Guid.NewGuid().ToString("N");
			var root = new RootFolder { Path = Path.Combine(Path.GetTempPath(), unique), MediaKind = MediaKind.SERIES };
			var quality = new QualityProfile { Name = "compat-test-" + unique };
			var language = new LanguageProfile { Name = "compat-test-" + unique, Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
			var series = new Series { TvdbId = Random.Shared.Next(10_000, int.MaxValue), Title = "compat-test-" + unique, SortTitle = unique, CleanTitle = unique };
			db.AddRange(root, quality, language, series);
			await db.SaveChangesAsync();
			var first = new MediaVersion
			{
				Name = "primary",
				SeriesId = series.Id,
				QualityProfileId = quality.Id,
				LanguageProfileId = language.Id,
				RootFolderId = root.Id,
				Path = "primary"
			};
			db.MediaVersions.Add(first);
			await db.SaveChangesAsync();
			return new SeedIds(series.Id, root.Id, quality.Id, language.Id, first.Id);
		});
	}

	private async Task<int> AddVersionAsync(int seriesId, int rootFolderId, int qualityProfileId, int languageProfileId)
	{
		return await _factory.WithDbAsync(async db =>
		{
			var version = new MediaVersion
			{
				Name = "sibling",
				SeriesId = seriesId,
				QualityProfileId = qualityProfileId,
				LanguageProfileId = languageProfileId,
				RootFolderId = rootFolderId,
				Path = "sibling"
			};
			db.MediaVersions.Add(version);
			await db.SaveChangesAsync();
			return version.Id;
		});
	}

	private sealed record SeedIds(int SeriesId, int RootFolderId, int QualityProfileId, int LanguageProfileId, int FirstVersionId);
}
