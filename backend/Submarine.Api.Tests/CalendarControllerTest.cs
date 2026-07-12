using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Controllers;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.Config;
using Submarine.Core.Library;
using Xunit;

namespace Submarine.Api.Tests;

public class CalendarControllerTest : DatabaseTestBase
{
	private const string FeedToken = "correct-token";

	private CalendarController CreateController()
	{
		var service = new CalendarService(new SeriesRepository(Context), new MovieRepository(Context));
		var store = new SecurityConfigStore(null!);
		store.Set(new SecurityConfig { Id = 1, FeedToken = FeedToken });

		return new CalendarController(service, store);
	}

	[Fact]
	public async Task GetFeedAsync_ShouldReturn401_WhenTokenIsWrong()
	{
		var controller = CreateController();

		var result = await controller.GetFeedAsync("wrong-token");

		var problem = Assert.IsAssignableFrom<ObjectResult>(result);
		Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
	}

	[Fact]
	public async Task GetFeedAsync_ShouldReturn401_WhenTokenIsMissing()
	{
		var controller = CreateController();

		var result = await controller.GetFeedAsync(null);

		var problem = Assert.IsAssignableFrom<ObjectResult>(result);
		Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
	}

	[Fact]
	public async Task GetFeedAsync_ShouldReturnEscapedIcs_WhenTokenIsValid()
	{
		var series = new Series
		{
			TvdbId = 1, Title = "Show, Inc.", Monitored = true, Type = SeriesType.STANDARD
		};
		Context.Series.Add(series);
		await Context.SaveChangesAsync();

		var episode = new Episode
		{
			SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 1, Title = "Pilot; Part 1",
			AirDate = DateTimeOffset.UtcNow, Monitored = true
		};
		Context.Episodes.Add(episode);
		await Context.SaveChangesAsync();

		var controller = CreateController();

		var result = await controller.GetFeedAsync(FeedToken);

		var content = Assert.IsType<ContentResult>(result);
		Assert.Equal("text/calendar", content.ContentType);
		Assert.Contains("BEGIN:VCALENDAR", content.Content);
		Assert.Contains($"UID:submarine-episode-{episode.Id}", content.Content);
		Assert.Contains("SUMMARY:Show\\, Inc. - S01E01 - Pilot\\; Part 1", content.Content);
	}
}
