using Submarine.Core.Enums;

namespace Submarine.Infrastructure.ImportLists;

/// <summary>One settings field of an import list type.</summary>
/// <param name="Name">Setting name inside SettingsJson.</param>
/// <param name="Label">Display label.</param>
/// <param name="Type">text, password, number, select or checkbox.</param>
/// <param name="Options">Choices for select fields.</param>
/// <param name="Required">Whether the field must be set.</param>
/// <param name="HelpText">Short help text.</param>
/// <param name="DefaultValue">Default value.</param>
public sealed record ImportListField(
	string Name,
	string Label,
	string Type,
	IReadOnlyList<string>? Options,
	bool Required,
	string? HelpText,
	string? DefaultValue);

/// <summary>Settings schema of one import list type.</summary>
/// <param name="Type">Import list type.</param>
/// <param name="Fields">Field descriptors.</param>
public sealed record ImportListTypeSchema(ImportListType Type, IReadOnlyList<ImportListField> Fields);

/// <summary>
///     Static settings schemas for every import list type.
/// </summary>
public static class ImportListSchemas
{
	/// <summary>
	///     The schema for every import list type.
	/// </summary>
	public static IReadOnlyList<ImportListTypeSchema> All { get; } =
	[
		new(ImportListType.TMDB_LIST,
		[
			Number("listId", "TMDB list id", "Numeric id of the TMDB list")
		]),
		new(ImportListType.TMDB_POPULAR, []),
		new(ImportListType.TMDB_COLLECTION,
		[
			Number("collectionId", "TMDB collection id", "Numeric id of the TMDB collection")
		]),
		new(ImportListType.TMDB_PERSON,
		[
			Number("personId", "TMDB person id", "Numeric id of the person")
		]),
		new(ImportListType.TRAKT_LIST,
		[
			Text("user", "Trakt user", true, "Trakt username owning the list"),
			Text("list", "List slug", true, "Slug of the public list"),
			Text("clientId", "Client id", false, "Trakt app client id, falls back to the Trakt:ClientId setting"),
			Password("accessToken", "Access token", false, "Only needed for private lists")
		]),
		new(ImportListType.TRAKT_POPULAR,
		[
			Select("category", "Category", ["trending", "popular"], "Which Trakt category to sync"),
			Text("clientId", "Client id", false, "Trakt app client id, falls back to the Trakt:ClientId setting")
		]),
		new(ImportListType.TRAKT_USER,
		[
			Text("user", "Trakt user", true, "Trakt username"),
			Select("listType", "List type", ["watchlist", "collection", "watched"], "Which user list to sync"),
			Text("clientId", "Client id", false, "Trakt app client id, falls back to the Trakt:ClientId setting"),
			Password("accessToken", "Access token", false, "Required for private user lists")
		]),
		new(ImportListType.ANILIST_SEASON, []),
		new(ImportListType.PLEX,
		[
			Password("accessToken", "Plex token", true, "Plex access token with watchlist access")
		]),
		new(ImportListType.SONARR,
		[
			Text("baseUrl", "Base url", true, "Url of the Sonarr instance, for example http://sonarr:8989"),
			Password("apiKey", "API key", true, "API key of the Sonarr instance")
		]),
		new(ImportListType.RADARR,
		[
			Text("baseUrl", "Base url", true, "Url of the Radarr instance, for example http://radarr:7878"),
			Password("apiKey", "API key", true, "API key of the Radarr instance")
		]),
		new(ImportListType.STEVEN_LU,
		[
			Text("url", "Url", false, "Defaults to https://stevenlu.com/movies.json")
		]),
		new(ImportListType.CUSTOM,
		[
			Text("url", "Url", true, "JSON url returning [{tmdbId|tvdbId, title}]")
		]),
		new(ImportListType.SIMKL,
		[
			Password("accessToken", "Access token", true, "Simkl personal access token"),
			Text("clientId", "Client id", false, "Simkl app client id, falls back to the Simkl:ClientId setting"),
			Select("listType", "List type", ["watching", "plan_to_watch", "hold", "completed", "dropped"], "Which Simkl list to sync"),
			Select("showType", "Catalogue", ["main", "anime"], "Main catalogue (shows/movies) or the anime catalogue")
		]),
		new(ImportListType.IMDB,
		[
			Text("listId", "List id", true, "IMDb list id of the form ls12345678")
		]),
		new(ImportListType.MYANIMELIST,
		[
			Password("accessToken", "Access token", true, "MyAnimeList personal access token"),
			Select("listStatus", "List status", ["all", "watching", "completed", "on_hold", "dropped", "plan_to_watch"], "Which MyAnimeList status to sync")
		]),
		new(ImportListType.RSS,
		[
			Text("url", "Url", true, "RSS/XML feed url. Series feeds need a numeric TVDB id in each item's guid; movie feeds are parsed by title and IMDb id")
		])
	];

	/// <summary>
	///     Minimum time between automatic scheduled fetches of a list of this type. Manual test,
	///     preview and single-list syncs bypass this.
	/// </summary>
	public static TimeSpan MinRefreshInterval(ImportListType type)
		=> type switch
		{
			ImportListType.SONARR => TimeSpan.FromMinutes(5),
			ImportListType.RADARR => TimeSpan.FromMinutes(15),
			ImportListType.PLEX => TimeSpan.FromHours(6),
			ImportListType.CUSTOM => TimeSpan.FromHours(6),
			ImportListType.SIMKL => TimeSpan.FromHours(6),
			ImportListType.MYANIMELIST => TimeSpan.FromHours(6),
			ImportListType.RSS => TimeSpan.FromHours(6),
			ImportListType.STEVEN_LU => TimeSpan.FromHours(24),
			_ => TimeSpan.FromHours(12)
		};

	private static ImportListField Text(string name, string label, bool required, string help, string? defaultValue = null)
		=> new(name, label, "text", null, required, help, defaultValue);

	private static ImportListField Password(string name, string label, bool required, string help)
		=> new(name, label, "password", null, required, help, null);

	private static ImportListField Number(string name, string label, string help)
		=> new(name, label, "number", null, true, help, null);

	private static ImportListField Select(string name, string label, IReadOnlyList<string> options, string help)
		=> new(name, label, "select", options, false, help, options[0]);
}
