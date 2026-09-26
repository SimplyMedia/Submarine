namespace Submarine.Infrastructure.Health;

/// <summary>
///     Builds documentation links for health issues.
/// </summary>
internal static class HealthWikiLinks
{
	private const string BaseUrl = "https://github.com/SimplyMedia/Submarine/wiki/Health-checks";

	/// <summary>
	///     Link to the health checks wiki page, anchored at the given section.
	/// </summary>
	public static string For(string anchor) => $"{BaseUrl}#{anchor}";
}
