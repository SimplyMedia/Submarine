using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using Submarine.Core.Indexers;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.Indexers.Cardigann;

/// <summary>
///     Runs a Cardigann definition against a tracker
/// </summary>
/// <param name="definition">The parsed definition</param>
/// <param name="settings">The user settings of this indexer instance</param>
/// <param name="http">The http access for this indexer</param>
/// <param name="logger">The logger</param>
public sealed partial class CardigannIndexer(
	CardigannDefinition definition,
	CardigannSettings settings,
	IIndexerHttpClient http,
	ILogger<CardigannIndexer> logger) : IIndexer
{
	private static readonly CardigannTemplateEngine TemplateEngine = new();
	private static readonly CardigannSelectorEngine SelectorEngine = new();
	private static readonly HtmlParser LoginParser = new();
	private static readonly IReadOnlyDictionary<string, object?> EmptyResult = new Dictionary<string, object?>();

	private IndexerCapabilities? _capabilities;
	private bool _loggedIn;
	private Dictionary<string, string>? _config;

	/// <inheritdoc />
	public string Name => definition.Name;

	/// <inheritdoc />
	public Protocol Protocol => definition.ProtocolKind;

	private Dictionary<string, string> Config
	{
		get
		{
			if (_config is { } existing)
				return existing;

			var sitelink = settings.BaseUrl ?? definition.Links.FirstOrDefault() ?? throw new IndexerException($"Definition {definition.Id} has no links");
			if (!sitelink.EndsWith('/'))
				sitelink += "/";

			var config = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["sitelink"] = sitelink
			};
			foreach (var setting in definition.Settings)
			{
				var value = settings.Fields?.GetValueOrDefault(setting.Name);
				config[setting.Name] = value ?? setting.Default ?? string.Empty;
			}

			_config = config;
			return config;
		}
	}

	/// <inheritdoc />
	public Task<IndexerCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		_capabilities ??= BuildCapabilities();
		return Task.FromResult(_capabilities);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ReleaseInfo>> FetchAsync(SearchRequest request, CancellationToken cancellationToken = default)
	{
		var (mode, query) = request switch
		{
			TvSearchRequest tv => ("tv-search", tv),
			MovieSearchRequest movie => ("movie-search", movie),
			_ => ("search", request)
		};

		var fallbackToBasic = false;
		if (!SupportsMode(mode))
		{
			if (!SupportsMode("search"))
				throw new IndexerException($"{definition.Name} does not support {mode} searches");
			fallbackToBasic = true;
			mode = "search";
		}

		var season = query is TvSearchRequest tvRequest ? tvRequest.Season : null;
		var episode = query is TvSearchRequest tvEpisode ? tvEpisode.Episode : null;
		var imdbId = request switch
		{
			TvSearchRequest tvImdb => tvImdb.ImdbId,
			MovieSearchRequest movieImdb => movieImdb.ImdbId,
			_ => null
		};
		var tmdbId = request switch
		{
			TvSearchRequest tvTmdb => tvTmdb.TmdbId,
			MovieSearchRequest movieTmdb => movieTmdb.TmdbId,
			_ => null
		};
		var year = query is MovieSearchRequest movieYear ? movieYear.Year : null;

		var keywords = request.Query ?? string.Empty;
		keywords = ApplyKeywordsFilters(keywords);

		var categories = MapRequestCategories(request.Categories);
		var context = new CardigannTemplateContext(
			Config,
			CardigannTemplateContext.BuildQuery(
				ToQueryType(mode),
				fallbackToBasic ? request.Query : null,
				keywords,
				season,
				episode,
				request is TvSearchRequest tvIds ? tvIds.TvdbId : null,
				tmdbId,
				imdbId,
				year,
				request.Limit,
				request.Offset),
			keywords,
			categories);

		await EnsureLoggedInAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTime.UtcNow;
		var releases = new List<ReleaseInfo>();
		foreach (var path in definition.Search.Paths)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				releases.AddRange(await FetchPathAsync(path, mode, context, now, cancellationToken).ConfigureAwait(false));
			}
			catch (IndexerAuthException)
			{
				throw;
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				logger.LogWarning(exception, "Search path {Path} of {Indexer} failed", path.Path, definition.Name);
			}
		}

		return releases
			.DistinctBy(release => release.Guid)
			.ToList();
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<ReleaseInfo>> FetchRssAsync(CancellationToken cancellationToken = default)
		=> FetchAsync(new BasicSearchRequest(IsRss: true), cancellationToken);

	/// <inheritdoc />
	public async Task<HttpResponseMessage> DownloadAsync(Uri link, CancellationToken cancellationToken = default)
	{
		if (link.Scheme.Equals("magnet", StringComparison.OrdinalIgnoreCase))
			throw new IndexerException("Magnet links cannot be downloaded over http");

		await EnsureLoggedInAsync(cancellationToken).ConfigureAwait(false);

		if (definition.Download is null)
			return await http.GetAsync(link, cancellationToken: cancellationToken).ConfigureAwait(false);

		var download = definition.Download;
		var now = DateTime.UtcNow;

		IReadOnlyDictionary<string, object?>? beforeResult = null;
		Uri pageUri = link;
		if (download.Before is { } before)
		{
			pageUri = ResolveUri(before.Path, []);
			Dictionary<string, string> beforeInputs = before.Inputs.Count == 0
				? new Dictionary<string, string>()
				: new Dictionary<string, string>(TemplateEngine.RenderAll(before.Inputs, BasicContext()));
			var beforeResponse = before.Method.Equals("post", StringComparison.OrdinalIgnoreCase)
				? await http.PostFormAsync(pageUri, beforeInputs, cancellationToken: cancellationToken).ConfigureAwait(false)
				: await http.GetAsync(AppendQuery(pageUri, beforeInputs), cancellationToken: cancellationToken).ConfigureAwait(false);
			var beforeBody = await beforeResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			if (before.Selector is { } selector)
			{
				var document = SelectorEngine.ParseResponse(CardigannResponseType.Html, beforeBody);
				var extracted = ExtractHtmlText(document, selector, before.Attribute);
				beforeResult = extracted is null
					? null
					: new Dictionary<string, object?> { ["before"] = extracted };
			}
		}

		var page = await http.GetAsync(pageUri, cancellationToken: cancellationToken).ConfigureAwait(false);
		var html = await page.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		var parsed = SelectorEngine.ParseResponse(CardigannResponseType.Html, html);

		string? resolvedLink = null;
		foreach (var selectorField in download.Selectors)
		{
			var value = SelectorEngine.EvaluateField(
				new CardigannRow(((IDocument)parsed).DocumentElement),
				CardigannResponseType.Html,
				selectorField,
				(template, _) => TemplateEngine.Render(template, BasicContext(), beforeResult),
				now);
			if (string.IsNullOrWhiteSpace(value))
				continue;

			resolvedLink = ApplyFiltersToValue(value, selectorField.Filters, beforeResult, now);
			if (resolvedLink.Length > 0)
				break;
		}

		if (string.IsNullOrWhiteSpace(resolvedLink) && download.InfoHash is { } infoHash)
		{
			var hashContext = beforeResult ?? EmptyResult;
			var hash = ExtractFieldOnDocument(parsed, infoHash.Hash, hashContext, now);
			var title = ExtractFieldOnDocument(parsed, infoHash.Title, hashContext, now);
			if (string.IsNullOrWhiteSpace(hash))
				throw new IndexerException($"No download link or infohash found on {link}");

			return MagnetResponse(BuildMagnet(hash, title ?? "unknown"));
		}

		if (string.IsNullOrWhiteSpace(resolvedLink))
			throw new IndexerException($"No download link found on {link}");

		if (resolvedLink.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
			return MagnetResponse(resolvedLink);

		var target = ResolveUri(resolvedLink, []);
		return await http.GetAsync(target, cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	private static HttpResponseMessage MagnetResponse(string magnet)
		=> new(HttpStatusCode.OK)
		{
			Content = new StringContent(magnet, Encoding.UTF8, "text/uri-list")
		};

	private async Task<IReadOnlyList<ReleaseInfo>> FetchPathAsync(
		CardigannSearchPath path,
		string mode,
		CardigannTemplateContext context,
		DateTime now,
		CancellationToken cancellationToken)
	{
		string renderedPath;
		try
		{
			renderedPath = TemplateEngine.Render(path.Path, context);
		}
		catch (CardigannTemplateException exception)
		{
			logger.LogWarning(exception, "Path template of {Indexer} could not be rendered, skipping", definition.Name);
			return [];
		}

		if (renderedPath.Length == 0)
			return [];

		var inputs = new Dictionary<string, string>();
		if (path.InheritInputs)
			inputs = new Dictionary<string, string>(TemplateEngine.RenderAll(definition.Search.Inputs, context));
		foreach (var (name, template) in path.Inputs ?? [])
			inputs[name] = TemplateEngine.Render(template, context);

		var headers = definition.Search.Headers.Count == 0
			? null
			: definition.Search.Headers.ToDictionary(
				kv => kv.Key,
				kv => (IReadOnlyList<string>)kv.Value.Select(value => TemplateEngine.Render(value, context)).ToList());

		var uri = IsAbsolute(renderedPath) ? new Uri(renderedPath) : ResolveUri(renderedPath, []);
		HttpResponseMessage response;
		if (path.Method.Equals("post", StringComparison.OrdinalIgnoreCase))
		{
			response = await http.PostFormAsync(uri, inputs, headers, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			uri = AppendQuery(uri, inputs);
			response = await http.GetAsync(uri, headers, cancellationToken).ConfigureAwait(false);
		}

		if (!response.IsSuccessStatusCode)
			throw new IndexerException($"{definition.Name} returned {(int)response.StatusCode} for {uri}");

		var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		body = ApplyPreprocessingFilters(body, context);

		if (path.Response?.NoResultsMessage is { } noResults && body.Contains(noResults, StringComparison.Ordinal))
			return [];

		var responseType = ParseResponseType(path.Response?.Type);
		var document = SelectorEngine.ParseResponse(responseType, body);
		var rows = SelectorEngine.SelectRows(document, responseType, definition.Search.Rows);

		var releases = new List<ReleaseInfo>();
		foreach (var row in rows)
		{
			try
			{
				var release = BuildRelease(row, responseType, context, now);
				if (release is { })
					releases.Add(release);
			}
			catch (IndexerException exception)
			{
				logger.LogWarning(exception, "Row of {Indexer} could not be parsed", definition.Name);
			}
		}

		return releases;
	}

	private ReleaseInfo? BuildRelease(CardigannRow row, CardigannResponseType responseType, CardigannTemplateContext context, DateTime now)
	{
		var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
		string RenderTemplate(string template, IReadOnlyDictionary<string, object?> result)
			=> TemplateEngine.Render(template, context, MergeResult(fields, result));

		foreach (var (name, cardigannField) in definition.Search.Fields)
		{
			var value = SelectorEngine.EvaluateField(row, responseType, cardigannField, RenderTemplate, now);
			fields[name] = value;
		}

		var title = fields.GetValueOrDefault("title") as string;
		if (string.IsNullOrWhiteSpace(title))
			return null;

		var details = ResolveUrl(AsUrl(fields.GetValueOrDefault("details") as string));
		var downloadRaw = ResolveUrl(AsUrl(fields.GetValueOrDefault("download") as string));
		var infoHash = CleanInfoHash(fields.GetValueOrDefault("infohash") as string);

		string? magnetUrl = null;
		string? downloadUrl = null;
		if (downloadRaw is { } download && download.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
		{
			magnetUrl = download;
			infoHash ??= ExtractInfoHashFromMagnet(download);
		}
		else if (downloadRaw is { })
		{
			downloadUrl = downloadRaw;
		}

		if (infoHash is { })
			magnetUrl ??= BuildMagnet(infoHash, title);

		var guid = details
			?? downloadUrl
			?? magnetUrl
			?? (infoHash is { } hash ? $"urn:btih:{hash}" : null);
		if (guid is null)
			return null;

		var seeders = ParseInt(fields.GetValueOrDefault("seeders") as string);
		var leechers = ParseInt(fields.GetValueOrDefault("leechers") as string);
		var peers = ParseInt(fields.GetValueOrDefault("peers") as string) ?? (seeders.HasValue && leechers.HasValue ? seeders + leechers : null);

		var categories = fields.GetValueOrDefault("category") is { } categoryValue
			? MapResultCategories(categoryValue.ToString() ?? string.Empty)
			: [];

		var flags = new List<IndexerFlag>();
		var downloadFactor = ParseDouble(fields.GetValueOrDefault("downloadvolumefactor") as string) ?? 1;
		var uploadFactor = ParseDouble(fields.GetValueOrDefault("uploadvolumefactor") as string) ?? 1;
		AddVolumeFlags(flags, downloadFactor, uploadFactor);

		var publishDate = ParseDate(fields.GetValueOrDefault("date") as string, now);

		return new ReleaseInfo
		{
			Guid = guid,
			Title = title.Trim(),
			DownloadUrl = downloadUrl,
			MagnetUrl = magnetUrl,
			InfoHash = infoHash,
			InfoUrl = details,
			Size = CardigannSizeParser.ParseBytes(fields.GetValueOrDefault("size") as string),
			PublishDate = publishDate,
			Categories = categories,
			Seeders = seeders,
			Leechers = leechers,
			Peers = peers,
			Grabs = ParseInt(fields.GetValueOrDefault("grabs") as string),
			Files = ParseInt(fields.GetValueOrDefault("files") as string),
			DownloadVolumeFactor = downloadFactor,
			UploadVolumeFactor = uploadFactor,
			MinimumRatio = ParseDouble(fields.GetValueOrDefault("minimumratio") as string),
			MinimumSeedTime = ParseInt(fields.GetValueOrDefault("minimumseedtime") as string),
			Protocol = Protocol,
			Indexer = Name,
			IndexerFlags = flags,
			TvdbId = ParseInt(fields.GetValueOrDefault("tvdbid") as string),
			TmdbId = ParseInt(fields.GetValueOrDefault("tmdbid") as string),
			ImdbId = NormalizeImdbId(fields.GetValueOrDefault("imdbid") as string),
			PosterUrl = ResolveUrl(AsUrl(fields.GetValueOrDefault("poster") as string)),
			Description = fields.GetValueOrDefault("description") as string
		};
	}

	private static IReadOnlyDictionary<string, object?> MergeResult(
		IReadOnlyDictionary<string, object?> fields,
		IReadOnlyDictionary<string, object?> extra)
	{
		if (extra.Count == 0)
			return fields;

		var merged = new Dictionary<string, object?>(fields);
		foreach (var (key, value) in extra)
			merged[key] = value;
		return merged;
	}

	private string ApplyKeywordsFilters(string keywords)
	{
		foreach (var filter in definition.Search.KeywordsFilters)
		{
			var args = (filter.Args ?? [])
				.Select(arg => TemplateEngine.Render(arg, BasicContext(keywords)))
				.ToList();
			keywords = CardigannFilters.Apply(filter.Name, keywords, args, DateTime.UtcNow);
		}

		return keywords;
	}

	private string ApplyPreprocessingFilters(string body, CardigannTemplateContext context)
	{
		foreach (var filter in definition.Search.PreprocessingFilters)
		{
			var args = (filter.Args ?? [])
				.Select(arg => TemplateEngine.Render(arg, context))
				.ToList();
			body = CardigannFilters.Apply(filter.Name, body, args, DateTime.UtcNow);
		}

		return body;
	}

	private string ApplyFiltersToValue(
		string value,
		IReadOnlyList<CardigannFilter> filters,
		IReadOnlyDictionary<string, object?>? result,
		DateTime now)
	{
		foreach (var filter in filters)
		{
			var args = (filter.Args ?? [])
				.Select(arg => TemplateEngine.Render(arg, BasicContext(result: result)))
				.ToList();
			value = CardigannFilters.Apply(filter.Name, value, args, now);
		}

		return value;
	}

	private string? ExtractFieldOnDocument(
		object document,
		CardigannField field,
		IReadOnlyDictionary<string, object?> result,
		DateTime now)
	{
		var root = document switch
		{
			IDocument html => html.DocumentElement,
			JsonDocument json => null,
			_ => null
		};
		if (root is null)
			return null;

		var value = SelectorEngine.EvaluateField(
			new CardigannRow(root),
			CardigannResponseType.Html,
			field,
			(template, _) => TemplateEngine.Render(template, BasicContext(result: result)),
			now);
		return value is null ? null : ApplyFiltersToValue(value, field.Filters, result, now);
	}

	private string? ExtractHtmlText(object document, string selector, string? attribute)
	{
		if (document is not IDocument html)
			return null;

		var element = html.QuerySelector(selector);
		if (element is null)
			return null;

		return attribute is { } name ? element.GetAttribute(name) : element.TextContent.Trim();
	}

	private CardigannTemplateContext BasicContext(string? keywords = null, IReadOnlyDictionary<string, object?>? result = null)
		=> new(
			Config,
			CardigannTemplateContext.BuildQuery("search", null, keywords ?? string.Empty),
			keywords ?? string.Empty,
			[]);

	private async Task EnsureLoggedInAsync(CancellationToken cancellationToken)
	{
		var login = definition.Login;
		if (login is null || _loggedIn)
			return;

		var sitelink = Config["sitelink"];
		switch (login.Method.ToLowerInvariant())
		{
			case "form":
			{
				var loginUri = ResolveUri(login.Path ?? string.Empty, []);
				var inputs = TemplateEngine.RenderAll(login.Inputs, BasicContext());
				var page = await http.GetAsync(loginUri, cancellationToken: cancellationToken).ConfigureAwait(false);
				var html = await page.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				var document = LoginParser.ParseDocument(html);
				var form = (login.Form is { } formSelector ? document.QuerySelector(formSelector) : null) ?? document.QuerySelector("form");
				if (form is null)
					throw new IndexerAuthException($"Login form of {definition.Name} not found");

				var formFields = new Dictionary<string, string>(StringComparer.Ordinal);
				foreach (var input in form.QuerySelectorAll("input, select"))
				{
					var name = input.GetAttribute("name");
					if (string.IsNullOrEmpty(name))
						continue;
					formFields[name] = input.GetAttribute("value") ?? string.Empty;
				}

				foreach (var (name, value) in inputs)
					formFields[name] = value;

				foreach (var (selector, name) in login.SelectorInputs)
				{
					var element = document.QuerySelector(selector);
					if (element is { })
						formFields[name] = element.GetAttribute("value") ?? element.TextContent.Trim();
				}

				var action = login.SubmitPath ?? form.GetAttribute("action") ?? login.Path ?? string.Empty;
				var submitUri = IsAbsolute(action) ? new Uri(action) : new Uri(new Uri(sitelink), action);
				await CheckLoginErrorsAsync(
					await http.PostFormAsync(submitUri, formFields, cancellationToken: cancellationToken).ConfigureAwait(false),
					login,
					cancellationToken).ConfigureAwait(false);
				break;
			}
			case "post":
			{
				var loginUri = ResolveUri(login.SubmitPath ?? login.Path ?? string.Empty, []);
				var inputs = TemplateEngine.RenderAll(login.Inputs, BasicContext());
				await CheckLoginErrorsAsync(
					await http.PostFormAsync(loginUri, inputs, cancellationToken: cancellationToken).ConfigureAwait(false),
					login,
					cancellationToken).ConfigureAwait(false);
				break;
			}
			case "get":
			{
				var loginUri = ResolveUri(login.Path ?? string.Empty, []);
				var inputs = TemplateEngine.RenderAll(login.Inputs, BasicContext());
				await CheckLoginErrorsAsync(
					await http.GetAsync(AppendQuery(loginUri, inputs), cancellationToken: cancellationToken).ConfigureAwait(false),
					login,
					cancellationToken).ConfigureAwait(false);
				break;
			}
			case "cookie":
			{
				var fieldName = login.Cookie ?? "cookie";
				var raw = settings.Fields?.GetValueOrDefault(fieldName);
				if (string.IsNullOrWhiteSpace(raw))
					throw new IndexerAuthException($"The {fieldName} setting of {definition.Name} is required for cookie login");

				var loginUri = ResolveUri(login.Path ?? string.Empty, []);
				foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
					http.Cookies.SetCookies(loginUri, part);
				await CheckLoginErrorsAsync(
					await http.GetAsync(loginUri, cancellationToken: cancellationToken).ConfigureAwait(false),
					login,
					cancellationToken).ConfigureAwait(false);
				break;
			}
			case "oneurl":
			{
				var url = TemplateEngine.Render(login.Inputs.GetValueOrDefault("url") ?? string.Empty, BasicContext());
				if (url.Length == 0)
					throw new IndexerAuthException($"The url input of {definition.Name} is required for oneurl login");

				await CheckLoginErrorsAsync(
					await http.GetAsync(new Uri(url), cancellationToken: cancellationToken).ConfigureAwait(false),
					login,
					cancellationToken).ConfigureAwait(false);
				break;
			}
			default:
				throw new IndexerAuthException($"Login method {login.Method} of {definition.Name} is not supported");
		}

		if (login.Test is { } test)
		{
			var testUri = IsAbsolute(test.Path) ? new Uri(test.Path) : ResolveUri(test.Path, []);
			var response = await http.GetAsync(testUri, cancellationToken: cancellationToken).ConfigureAwait(false);
			var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			if (LoginParser.ParseDocument(html).QuerySelector(test.Selector) is null)
				throw new IndexerAuthException($"The login test of {definition.Name} failed, check your credentials");
		}

		_loggedIn = true;
	}

	private async Task CheckLoginErrorsAsync(HttpResponseMessage response, CardigannLogin login, CancellationToken cancellationToken)
	{
		if (login.Error.Count == 0)
			return;

		var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		var document = LoginParser.ParseDocument(html);
		foreach (var error in login.Error)
		{
			var element = SelectorEngine.Query(document, error.Selector).FirstOrDefault();
			if (element is { })
				throw new IndexerAuthException(error.Message ?? element.TextContent.Trim());
		}
	}

	private IndexerCapabilities BuildCapabilities()
	{
		var caps = definition.Caps;
		IndexerCategory? StandardCategory(string path)
		{
			var id = IndexerCategories.Resolve(path);
			return id is { } resolved ? IndexerCategories.ById(resolved) : null;
		}

		var categories = new List<IndexerCategory>();
		foreach (var mapping in caps.CategoryMappings)
		{
			if (StandardCategory(mapping.Cat) is { } category && categories.All(existing => existing.Id != category.Id))
				categories.Add(category);
		}

		foreach (var (trackerId, standardName) in caps.Categories)
		{
			if (StandardCategory(standardName) is { } category && categories.All(existing => existing.Id != category.Id))
				categories.Add(category);
		}

		return new IndexerCapabilities
		{
			SearchAvailable = SupportsMode("search"),
			SearchParams = ParamsFor("search"),
			TvSearchAvailable = SupportsMode("tv-search"),
			TvSearchParams = ParamsFor("tv-search"),
			MovieSearchAvailable = SupportsMode("movie-search"),
			MovieSearchParams = ParamsFor("movie-search"),
			MusicSearchAvailable = SupportsMode("music-search"),
			MusicSearchParams = ParamsFor("music-search"),
			BookSearchAvailable = SupportsMode("book-search"),
			BookSearchParams = ParamsFor("book-search"),
			Categories = categories,
			SupportsRawSearch = caps.AllowRawSearch
		};
	}

	private bool SupportsMode(string mode)
		=> definition.Caps.Modes.ContainsKey(mode);

	private SearchParams ParamsFor(string mode)
		=> definition.Caps.Modes.GetValueOrDefault(mode, []).Aggregate(
			SearchParams.None,
			(current, parameter) => current | parameter.ToLowerInvariant() switch
			{
				"q" => SearchParams.Q,
				"season" => SearchParams.Season,
				"ep" => SearchParams.Ep,
				"imdbid" or "imdbidshort" => SearchParams.ImdbId,
				"tmdbid" => SearchParams.TmdbId,
				"tvdbid" => SearchParams.TvdbId,
				"rid" => SearchParams.Rid,
				"year" => SearchParams.Year,
				_ => SearchParams.None
			});

	private List<string> MapRequestCategories(IReadOnlyList<int> standardCategories)
	{
		var mappings = definition.Caps.CategoryMappings;
		var direct = definition.Caps.Categories;
		var trackerIds = new List<string>();

		if (standardCategories.Count == 0)
		{
			trackerIds.AddRange(mappings.Where(mapping => mapping.Default).Select(mapping => mapping.Id));
			if (trackerIds.Count == 0 && direct.Count > 0)
				trackerIds.AddRange(direct.Keys);
			return trackerIds.Distinct().ToList();
		}

		var wanted = standardCategories
			.SelectMany(id => (IReadOnlyList<int>)IndexerCategories.InclusiveDescendantIds(id))
			.ToHashSet();

		foreach (var mapping in mappings)
		{
			var standardId = IndexerCategories.Resolve(mapping.Cat);
			if (standardId is { } id && wanted.Contains(id) && !trackerIds.Contains(mapping.Id))
				trackerIds.Add(mapping.Id);
		}

		foreach (var (trackerId, standardName) in direct)
		{
			var standardId = IndexerCategories.Resolve(standardName);
			if (standardId is { } id && wanted.Contains(id) && !trackerIds.Contains(trackerId))
				trackerIds.Add(trackerId);
		}

		return trackerIds;
	}

	private List<int> MapResultCategories(string trackerValue)
	{
		var values = trackerValue
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(value => value.Trim())
			.Where(value => value.Length > 0)
			.ToList();
		if (values.Count == 0)
			values = [trackerValue.Trim()];

		var ids = new List<int>();
		foreach (var value in values)
		{
			var mapped = definition.Caps.CategoryMappings
				.Where(mapping => string.Equals(mapping.Id, value, StringComparison.OrdinalIgnoreCase))
				.Select(mapping => IndexerCategories.Resolve(mapping.Cat))
				.OfType<int>()
				.ToList();

			if (mapped.Count > 0)
			{
				ids.AddRange(mapped);
				continue;
			}

			mapped = definition.Caps.Categories
				.Where(kv => string.Equals(kv.Key, value, StringComparison.OrdinalIgnoreCase))
				.Select(kv => IndexerCategories.Resolve(kv.Value))
				.OfType<int>()
				.ToList();

			if (mapped.Count > 0)
			{
				ids.AddRange(mapped);
				continue;
			}

			if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var passthrough))
				ids.Add(passthrough);
		}

		return ids.Distinct().ToList();
	}

	private Uri ResolveUri(string path, Dictionary<string, string> inputs)
	{
		var rendered = TemplateEngine.Render(path, BasicContext());
		var uri = IsAbsolute(rendered) ? new Uri(rendered) : new Uri(new Uri(Config["sitelink"]), rendered);
		return AppendQuery(uri, inputs);
	}

	private static bool IsAbsolute(string url)
		=> url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
			|| url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

	private static Uri AppendQuery(Uri uri, IReadOnlyDictionary<string, string> inputs)
	{
		if (inputs.Count == 0)
			return uri;

		var existing = uri.Query.TrimStart('?');
		var existingKeys = new HashSet<string>(
			existing.Split('&', StringSplitOptions.RemoveEmptyEntries)
				.Select(pair => Uri.UnescapeDataString(pair.Split('=')[0])),
			StringComparer.Ordinal);

		var added = inputs
			.Where(kv => !existingKeys.Contains(kv.Key) && kv.Value.Length > 0)
			.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}")
			.ToList();

		if (added.Count == 0)
			return uri;

		var separator = existing.Length > 0 ? "&" : string.Empty;
		return new Uri($"{uri.GetLeftPart(UriPartial.Path)}?{existing}{separator}{string.Join("&", added)}");
	}

	private static string? AsUrl(string? value)
		=> string.IsNullOrWhiteSpace(value) ? null : value.Trim();

	private string? ResolveUrl(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var trimmed = value.Trim();
		return IsAbsolute(trimmed) ? trimmed : new Uri(new Uri(Config["sitelink"]), trimmed).ToString();
	}

	private static string? CleanInfoHash(string? value)
		=> string.IsNullOrWhiteSpace(value)
			? null
			: InfoHashRegex().Match(value) is { Success: true } match ? match.Value.ToLowerInvariant() : value.Trim().ToLowerInvariant();

	private static string? ExtractInfoHashFromMagnet(string magnet)
		=> MagnetHashRegex().Match(magnet) is { Success: true } match ? match.Groups["hash"].Value.ToLowerInvariant() : null;

	private static string BuildMagnet(string infoHash, string title)
		=> $"magnet:?xt=urn:btih:{infoHash}&dn={Uri.EscapeDataString(title)}";

	private static string? NormalizeImdbId(string? value)
		=> string.IsNullOrWhiteSpace(value)
			? null
			: value.Trim().StartsWith("tt", StringComparison.OrdinalIgnoreCase) ? value.Trim() : $"tt{value.Trim()}";

	private static int? ParseInt(string? value)
		=> int.TryParse(value?.Trim().Replace(",", string.Empty), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

	private static double? ParseDouble(string? value)
		=> double.TryParse(value?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

	private static DateTime? ParseDate(string? value, DateTime now)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var trimmed = value.Trim();
		if (trimmed.Equals("now", StringComparison.OrdinalIgnoreCase))
			return now;

		if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epoch))
			return DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;

		return CardigannTimeAgoParser.ParseFuzzy(trimmed, now)
			?? (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
				? parsed.ToUniversalTime()
				: null);
	}

	private static void AddVolumeFlags(List<IndexerFlag> flags, double downloadFactor, double uploadFactor)
	{
		switch (downloadFactor)
		{
			case 0:
				flags.Add(IndexerFlag.FREELEECH);
				break;
			case 0.25:
				flags.Add(IndexerFlag.G_FREELEECH);
				break;
			case 0.5:
				flags.Add(IndexerFlag.HALFLEECH);
				break;
		}

		if (uploadFactor == 2)
			flags.Add(IndexerFlag.DOUBLE_UPLOAD);
	}

	private static string ToQueryType(string mode)
		=> mode switch
		{
			"tv-search" => "tvsearch",
			"movie-search" => "movie",
			"music-search" => "music",
			"book-search" => "book",
			_ => "search"
		};

	private static CardigannResponseType ParseResponseType(string? type)
		=> type?.ToLowerInvariant() switch
		{
			"json" => CardigannResponseType.Json,
			"xml" => CardigannResponseType.Xml,
			_ => CardigannResponseType.Html
		};

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		http.Dispose();
		return ValueTask.CompletedTask;
	}

	[GeneratedRegex(@"[0-9a-fA-F]{40}")]
	private static partial Regex InfoHashRegex();

	[GeneratedRegex(@"xt=urn:btih:(?<hash>[0-9a-zA-Z]+)")]
	private static partial Regex MagnetHashRegex();
}
