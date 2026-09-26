using System.Collections.Concurrent;

namespace Submarine.Api.Common;

/// <summary>
///     Serves the built SPA's index.html. The SPA is built with a fixed root baseURL, so when
///     an instance is served under a UrlBase this rewrites the `/_nuxt/` asset URLs and the
///     inlined Nuxt runtime config's baseURL to the configured UrlBase. The file is read once
///     and the rewritten result cached per UrlBase value.
/// </summary>
public sealed class IndexHtmlCache(IWebHostEnvironment environment)
{
	private readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.Ordinal);

	/// <summary>
	///     Get index.html rewritten for the given UrlBase (empty string for root), or null when the
	///     built SPA is not present.
	/// </summary>
	public string? GetHtml(string urlBase) => _cache.GetOrAdd(urlBase ?? string.Empty, Build);

	private string? Build(string urlBase)
	{
		var path = Path.Combine(environment.WebRootPath, "index.html");
		if (!File.Exists(path))
		{
			return null;
		}

		var html = File.ReadAllText(path);
		if (string.IsNullOrEmpty(urlBase))
		{
			return html;
		}

		return html
			.Replace("/_nuxt/", urlBase + "/_nuxt/", StringComparison.Ordinal)
			.Replace("baseURL:\"/\"", $"baseURL:\"{urlBase}\"", StringComparison.Ordinal);
	}
}
