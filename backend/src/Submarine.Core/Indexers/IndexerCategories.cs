using System;
using System.Collections.Generic;
using System.Linq;

namespace Submarine.Core.Indexers;

/// <summary>
///     The standard Newznab category tree with lookup helpers
/// </summary>
public static class IndexerCategories
{
	/// <summary>
	///     The complete standard category tree, parents before children
	/// </summary>
	public static IReadOnlyList<IndexerCategory> All { get; } = BuildTree();

	private static readonly Dictionary<int, IndexerCategory> ByIdMap = All
		.SelectMany(Flatten)
		.ToDictionary(category => category.Id);

	private static readonly Dictionary<string, IndexerCategory> ByPathMap = All
		.SelectMany(Flatten)
		.DistinctBy(category => Normalize(category.Name))
		.ToDictionary(category => Normalize(category.Name));

	/// <summary>
	///     Looks up a category by its id
	/// </summary>
	/// <param name="id">The category id</param>
	/// <returns>The category, or null</returns>
	public static IndexerCategory? ById(int id)
		=> ByIdMap.GetValueOrDefault(id);

	/// <summary>
	///     The id of the parent category, or null for root categories
	/// </summary>
	/// <param name="id">The category id</param>
	/// <returns>The parent id, or null</returns>
	public static int? ParentId(int id)
		=> ByIdMap.TryGetValue(id, out var category)
			? FindParent(All, category)
			: null;

	/// <summary>
	///     Resolves a Cardigann style category path like "TV/Anime" to its standard id
	/// </summary>
	/// <param name="path">The category path</param>
	/// <returns>The id of the deepest known category of the path, or null</returns>
	public static int? Resolve(string path)
	{
		var normalized = Normalize(path);
		if (ByPathMap.TryGetValue(normalized, out var exact))
			return exact.Id;

		// fall back to the longest known prefix, e.g. "TV/Unknown" resolves to TV
		var segments = normalized.Split('/');
		for (var i = segments.Length - 1; i > 0; i--)
		{
			var prefix = string.Join('/', segments.Take(i));
			if (ByPathMap.TryGetValue(prefix, out var parent))
				return parent.Id;
		}

		return null;
	}

	/// <summary>
	///     The category itself plus all transitive children ids, e.g. 5000 yields TV and every TV subcategory
	/// </summary>
	/// <param name="id">The category id</param>
	/// <returns>The inclusive descendant ids</returns>
	public static IReadOnlyList<int> InclusiveDescendantIds(int id)
	{
		var category = ById(id);
		if (category == null)
			return [id];

		var ids = new List<int> { category.Id };
		foreach (var child in category.SubCategories)
			ids.AddRange(InclusiveDescendantIds(child.Id));

		return ids;
	}

	private static IEnumerable<IndexerCategory> Flatten(IndexerCategory category)
	{
		yield return category;
		foreach (var child in category.SubCategories)
			foreach (var descendant in Flatten(child))
				yield return descendant;
	}

	private static int? FindParent(IReadOnlyList<IndexerCategory> level, IndexerCategory target)
	{
		foreach (var category in level)
		{
			if (category.SubCategories.Contains(target))
				return category.Id;

			var parent = FindParent(category.SubCategories, target);
			if (parent.HasValue)
				return parent;
		}

		return null;
	}

	private static string Normalize(string path)
		=> string.Concat(path.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == '/'));

	private static List<IndexerCategory> BuildTree()
	{
		IndexerCategory Category(int id, string name, params IndexerCategory[] subcategories)
			=> new(id, name, [.. subcategories]);

		return
		[
			Category(1000, "Console",
				Category(1010, "Console/NDS"),
				Category(1020, "Console/PSP"),
				Category(1030, "Console/Wii"),
				Category(1040, "Console/XBox"),
				Category(1050, "Console/XBox 360"),
				Category(1060, "Console/Wiiware"),
				Category(1070, "Console/XBox 360 DLC"),
				Category(1080, "Console/PS3"),
				Category(1090, "Console/Other"),
				Category(1110, "Console/3DS"),
				Category(1120, "Console/PS Vita"),
				Category(1130, "Console/WiiU"),
				Category(1140, "Console/XBox One"),
				Category(1180, "Console/PS4")),
			Category(2000, "Movies",
				Category(2010, "Movies/Foreign"),
				Category(2020, "Movies/Other"),
				Category(2030, "Movies/SD"),
				Category(2040, "Movies/HD"),
				Category(2045, "Movies/UHD"),
				Category(2050, "Movies/BluRay"),
				Category(2060, "Movies/3D"),
				Category(2070, "Movies/DVD"),
				Category(2080, "Movies/WEB-DL"),
				Category(2090, "Movies/x265")),
			Category(3000, "Audio",
				Category(3010, "Audio/MP3"),
				Category(3020, "Audio/Video"),
				Category(3030, "Audio/Audiobook"),
				Category(3040, "Audio/Lossless"),
				Category(3050, "Audio/Other"),
				Category(3060, "Audio/Foreign")),
			Category(4000, "PC",
				Category(4010, "PC/0day"),
				Category(4020, "PC/ISO"),
				Category(4030, "PC/Mac"),
				Category(4040, "PC/Mobile-Other"),
				Category(4050, "PC/Games"),
				Category(4060, "PC/Mobile-iOS"),
				Category(4070, "PC/Mobile-Android")),
			Category(5000, "TV",
				Category(5010, "TV/WEB-DL"),
				Category(5020, "TV/Foreign"),
				Category(5030, "TV/SD"),
				Category(5040, "TV/HD"),
				Category(5045, "TV/UHD"),
				Category(5050, "TV/Other"),
				Category(5060, "TV/Sport"),
				Category(5070, "TV/Anime"),
				Category(5080, "TV/Documentary"),
				Category(5090, "TV/x265")),
			Category(6000, "XXX",
				Category(6010, "XXX/DVD"),
				Category(6020, "XXX/WMV"),
				Category(6030, "XXX/XviD"),
				Category(6040, "XXX/x264"),
				Category(6045, "XXX/UHD"),
				Category(6050, "XXX/Pack"),
				Category(6060, "XXX/ImageSet"),
				Category(6070, "XXX/Other"),
				Category(6080, "XXX/SD"),
				Category(6090, "XXX/WEB-DL")),
			Category(7000, "Books",
				Category(7010, "Books/Mags"),
				Category(7020, "Books/EBook"),
				Category(7030, "Books/Comics"),
				Category(7040, "Books/Technical"),
				Category(7050, "Books/Other"),
				Category(7060, "Books/Foreign")),
			Category(8000, "Other",
				Category(8010, "Other/Misc"),
				Category(8020, "Other/Hashed"))
		];
	}
}
