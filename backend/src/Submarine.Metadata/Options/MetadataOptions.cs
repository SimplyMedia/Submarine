namespace Submarine.Metadata.Options;

public sealed class TmdbOptions
{
	public const string SectionName = "Tmdb";

	/// <summary>Upstream root, override only for tests or proxies.</summary>
	public string BaseUrl { get; set; } = "https://api.themoviedb.org/3/";

	/// <summary>Read access token sent as Bearer. Takes precedence over ApiKey.</summary>
	public string? AccessToken { get; set; }

	/// <summary>v3 API key sent as the api_key query parameter.</summary>
	public string? ApiKey { get; set; }
}

public sealed class TvdbOptions
{
	public const string SectionName = "Tvdb";

	/// <summary>Upstream root, override only for tests or proxies.</summary>
	public string BaseUrl { get; set; } = "https://api4.thetvdb.com/v4/";

	/// <summary>Project API key used for the v4 login.</summary>
	public string ApiKey { get; set; } = string.Empty;

	/// <summary>Optional subscriber PIN, sent as `pin` in the login body when set.</summary>
	public string? Pin { get; set; }
}

public sealed class CacheOptions
{
	public const string SectionName = "Cache";

	/// <summary>TTL for single-entity lookups (series, movie, collection).</summary>
	public TimeSpan DetailTtl { get; set; } = TimeSpan.FromHours(6);

	/// <summary>TTL for searches, lists and popular pages.</summary>
	public TimeSpan SearchTtl { get; set; } = TimeSpan.FromMinutes(15);
}
