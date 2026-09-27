using Submarine.Core.Languages;

namespace Submarine.Api.Features.Compat.Shared;

/// <summary>Explicit Sonarr/Radarr language IDs; native enum ordinals are not wire identities.</summary>
public static class CompatLanguageMap
{
	public static int ToUpstreamId(Language language, string facade)
	{
		EnsureFacade(facade);
		return language switch
		{
			Language.ENGLISH => 1,
			Language.FRENCH => 2,
			Language.SPANISH => 3,
			Language.GERMAN => 4,
			Language.ITALIAN => 5,
			Language.DANISH => 6,
			Language.DUTCH => 7,
			Language.JAPANESE => 8,
			Language.ICELANDIC => 9,
			Language.CHINESE => 10,
			Language.RUSSIAN => 11,
			Language.POLISH => 12,
			Language.VIETNAMESE => 13,
			Language.SWEDISH => 14,
			Language.NORWEGIAN => 15,
			Language.FINNISH => 16,
			Language.TURKISH => 17,
			Language.PORTUGUESE => 18,
			Language.FLEMISH => 19,
			Language.GREEK => 20,
			Language.KOREAN => 21,
			Language.HUNGARIAN => 22,
			Language.HEBREW => 23,
			Language.LITHUANIAN => 24,
			Language.CZECH => 25,
			Language.ARABIC => 26,
			Language.HINDI => 27,
			_ => throw new ArgumentOutOfRangeException(nameof(language), language, "No upstream language identity is defined")
		};
	}

	public static string Name(Language language)
		=> language switch
		{
			Language.ENGLISH => "English",
			Language.FRENCH => "French",
			Language.SPANISH => "Spanish",
			Language.GERMAN => "German",
			Language.ITALIAN => "Italian",
			Language.DANISH => "Danish",
			Language.DUTCH => "Dutch",
			Language.JAPANESE => "Japanese",
			Language.ICELANDIC => "Icelandic",
			Language.CHINESE => "Chinese",
			Language.RUSSIAN => "Russian",
			Language.POLISH => "Polish",
			Language.VIETNAMESE => "Vietnamese",
			Language.SWEDISH => "Swedish",
			Language.NORWEGIAN => "Norwegian",
			Language.FINNISH => "Finnish",
			Language.TURKISH => "Turkish",
			Language.PORTUGUESE => "Portuguese",
			Language.FLEMISH => "Flemish",
			Language.GREEK => "Greek",
			Language.KOREAN => "Korean",
			Language.HUNGARIAN => "Hungarian",
			Language.HEBREW => "Hebrew",
			Language.LITHUANIAN => "Lithuanian",
			Language.CZECH => "Czech",
			Language.ARABIC => "Arabic",
			Language.HINDI => "Hindi",
			_ => throw new ArgumentOutOfRangeException(nameof(language), language, "No upstream language name is defined")
		};

	public static bool TryGetNativeLanguage(int upstreamId, string facade, out Language language)
	{
		EnsureFacade(facade);
		foreach (var candidate in Enum.GetValues<Language>())
		{
			if (ToUpstreamId(candidate, facade) == upstreamId)
			{
				language = candidate;
				return true;
			}
		}
		language = default;
		return false;
	}

	public static IReadOnlyList<CompatLanguageResource> Catalog(string facade)
	{
		EnsureFacade(facade);
		var languages = Enum.GetValues<Language>()
			.Select(language => new CompatLanguageResource(ToUpstreamId(language, facade), Name(language)))
			.ToList();
		languages.Insert(0, new CompatLanguageResource(0, "Unknown"));
		languages.Add(new CompatLanguageResource(-2, "Original"));
		return languages;
	}

	private static void EnsureFacade(string facade)
	{
		if (facade is not ("sonarr" or "radarr"))
			throw new ArgumentOutOfRangeException(nameof(facade));
	}
}

public sealed record CompatLanguageResource(int Id, string Name);
