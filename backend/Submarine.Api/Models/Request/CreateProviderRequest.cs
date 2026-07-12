using Submarine.Core.Provider;

namespace Submarine.Api.Models.Request;

public record CreateProviderRequest
{
	public string Name { get; set; } = null!;

	public ProviderType Type { get; set; }

	public ProviderMode Mode { get; set; }

	public string Url { get; set; } = null!;

	public string ApiKey { get; set; } = null!;

	public short Priority { get; set; }

	public List<string> Tags { get; set; } = null!;

	public List<int>? Categories { get; set; }

	public List<int>? AnimeCategories { get; set; }

	public int? MinimumSeeders { get; set; }

	public float? SeedRatio { get; set; }

	public long? SeedTime { get; set; }

	public long? SeasonPackSeedTime { get; set; }

	public Provider ToProvider()
		=> Type switch
		{
			ProviderType.BITTORRENT_TRACKER => new BittorrentTracker
			{
				Name = Name,
				Mode = Mode,
				Url = Url,
				ApiKey = ApiKey,
				Priority = Priority,
				Tags = Tags,
				MinimumSeeders = MinimumSeeders,
				SeedRatio = SeedRatio,
				SeedTime = SeedTime,
				SeasonPackSeedTime = SeasonPackSeedTime
			},
			ProviderType.USENET_INDEXER => new UsenetIndexer
			{
				Name = Name,
				Mode = Mode,
				Url = Url,
				ApiKey = ApiKey,
				Priority = Priority,
				Tags = Tags
			},
			ProviderType.TORZNAB_INDEXER => new TorznabIndexer
			{
				Name = Name,
				Mode = Mode,
				Url = Url,
				ApiKey = ApiKey,
				Priority = Priority,
				Tags = Tags,
				Categories = Categories ?? new List<int>(),
				AnimeCategories = AnimeCategories ?? new List<int>(),
				MinimumSeeders = MinimumSeeders ?? 1,
				SeedRatio = SeedRatio,
				SeedTime = SeedTime,
				SeasonPackSeedTime = SeasonPackSeedTime
			},
			ProviderType.NEWZNAB_INDEXER => new NewznabIndexer
			{
				Name = Name,
				Mode = Mode,
				Url = Url,
				ApiKey = ApiKey,
				Priority = Priority,
				Tags = Tags,
				Categories = Categories ?? new List<int>(),
				AnimeCategories = AnimeCategories ?? new List<int>()
			},
			_ => throw new ArgumentOutOfRangeException()
		};
}
