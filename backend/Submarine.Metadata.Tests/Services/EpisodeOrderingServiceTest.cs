using System.Text.Json;
using Submarine.Metadata.Clients;
using Submarine.Metadata.Contracts;
using Submarine.Metadata.Services;
using Xunit;

namespace Submarine.Metadata.Tests.Services;

public class EpisodeOrderingServiceTest
{
	[Fact]
	public void Normalize_ShouldIncludeAbsoluteOrdering_WhenEpisodesCarryAbsoluteNumbers()
	{
		const string json = """
			[
				{
					"id": 305065,
					"name": "Asteroid Blues",
					"overview": "Spike and Jet chase a bounty on the asteroid colony Tijuana.",
					"aired": "1998-10-24",
					"runtime": 24,
					"seasonNumber": 1,
					"number": 1,
					"absoluteNumber": 1
				},
				{
					"id": 305090,
					"name": "The Real Folk Blues (Part 2)",
					"aired": "1999-04-24",
					"seasonNumber": 1,
					"number": 26,
					"absoluteNumber": 26
				}
			]
			""";

		var normalized = EpisodeOrderingService.Normalize(Deserialize(json));

		Assert.Equal(2, normalized.Count);

		var first = normalized[0];
		Assert.Equal(305065, first.TvdbId);
		Assert.Equal("Asteroid Blues", first.Title);
		Assert.Equal("Spike and Jet chase a bounty on the asteroid colony Tijuana.", first.Overview);
		Assert.Equal(new DateOnly(1998, 10, 24), first.AirDate);
		Assert.Equal(24, first.Runtime);
		Assert.Equal(2, first.Numbers.Count);
		Assert.Contains(new EpisodeNumber(EpisodeOrdering.Aired, 1, 1, null), first.Numbers);
		Assert.Contains(new EpisodeNumber(EpisodeOrdering.Absolute, null, null, 1), first.Numbers);

		Assert.Contains(new EpisodeNumber(EpisodeOrdering.Absolute, null, null, 26), normalized[1].Numbers);
	}

	[Fact]
	public void Normalize_ShouldIncludeDivergingDvdOrdering_WhenDvdOrderDiffersFromAired()
	{
		const string json = """
			[
				{
					"id": 297999,
					"name": "The Train Job",
					"aired": "2002-09-20",
					"seasonNumber": 1,
					"number": 1,
					"dvdSeason": 1,
					"dvdEpisode": 2
				}
			]
			""";

		var episode = Assert.Single(EpisodeOrderingService.Normalize(Deserialize(json)));

		Assert.Equal(2, episode.Numbers.Count);
		Assert.Contains(new EpisodeNumber(EpisodeOrdering.Aired, 1, 1, null), episode.Numbers);
		Assert.Contains(new EpisodeNumber(EpisodeOrdering.Dvd, 1, 2, null), episode.Numbers);
	}

	[Fact]
	public void Normalize_ShouldOmitDvdAndAbsoluteOrderings_WhenEpisodeLacksThem()
	{
		const string json = """
			[
				{
					"id": 4360271,
					"name": "Winter Is Coming",
					"aired": "2011-04-17",
					"seasonNumber": 1,
					"number": 1
				}
			]
			""";

		var episode = Assert.Single(EpisodeOrderingService.Normalize(Deserialize(json)));

		var number = Assert.Single(episode.Numbers);
		Assert.Equal(new EpisodeNumber(EpisodeOrdering.Aired, 1, 1, null), number);
	}

	[Fact]
	public void Normalize_ShouldOmitAbsoluteAndDvdOrderings_WhenAbsoluteNumberIsZeroAndDvdDataIsPartial()
	{
		const string json = """
			[
				{
					"id": 3254641,
					"name": "Pilot",
					"aired": "2008-01-20",
					"seasonNumber": 1,
					"number": 1,
					"absoluteNumber": 0,
					"dvdSeason": 1
				}
			]
			""";

		var episode = Assert.Single(EpisodeOrderingService.Normalize(Deserialize(json)));

		var number = Assert.Single(episode.Numbers);
		Assert.Equal(EpisodeOrdering.Aired, number.Ordering);
	}

	private static IReadOnlyList<TvdbEpisode> Deserialize(string json)
		=> JsonSerializer.Deserialize<IReadOnlyList<TvdbEpisode>>(json)!;
}
