using Submarine.Core.Provider;

namespace Submarine.Api.Models.Request;

public record UpdateProviderRequest
{
	public string? Name { get; set; }

	public ProviderMode? Mode { get; set; }

	public string? Url { get; set; }

	public string? ApiKey { get; set; }

	public short? Priority { get; set; }

	public List<int>? Categories { get; set; }

	public List<int>? AnimeCategories { get; set; }

	public int? MinimumSeeders { get; set; }

	public float? SeedRatio { get; set; }

	public long? SeedTime { get; set; }

	public long? SeasonPackSeedTime { get; set; }
}
