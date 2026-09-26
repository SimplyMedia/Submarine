using System.Text.Json;
using System.Text.Json.Serialization;
using Submarine.Core.Enums;

namespace Submarine.Core.Metadata;

/// <summary>
///     One configurable field of a metadata consumer type, used by the schema endpoint.
/// </summary>
/// <param name="Name">Field name as it appears in the settings JSON.</param>
/// <param name="Label">Human readable label.</param>
/// <param name="HelpText">Short explanation, often the resulting file name.</param>
/// <param name="Default">Default value.</param>
public sealed record MetadataConsumerFieldDescriptor(string Name, string Label, string? HelpText, bool Default);

/// <summary>
///     Deserializes metadata consumer settings per <see cref="MetadataConsumerType" /> and describes their fields.
///     Every field is a checkbox, so no per-field validation is needed.
/// </summary>
public static class MetadataConsumerSettingsJson
{
	/// <summary>
	///     Serializer options used for metadata consumer settings: camel-case properties.
	/// </summary>
	public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() }
	};

	/// <summary>
	///     Deserializes the json for the consumer type, falling back to defaults when the json is empty or invalid.
	/// </summary>
	public static MetadataConsumerSettings Parse(MetadataConsumerType type, string json)
	{
		try
		{
			return (MetadataConsumerSettings?)JsonSerializer.Deserialize(json, SettingsType(type), SerializerOptions)
				?? Default(type);
		}
		catch (JsonException)
		{
			return Default(type);
		}
	}

	/// <summary>
	///     Serializes settings to json.
	/// </summary>
	public static string Serialize(MetadataConsumerSettings settings)
		=> JsonSerializer.Serialize(settings, settings.GetType(), SerializerOptions);

	/// <summary>
	///     Field descriptors for every metadata consumer type, driving the schema endpoint.
	/// </summary>
	public static IReadOnlyDictionary<MetadataConsumerType, MetadataConsumerFieldDescriptor[]> DescribeAll()
		=> Enum.GetValues<MetadataConsumerType>().ToDictionary(type => type, Describe);

	/// <summary>
	///     Field descriptors of one metadata consumer type.
	/// </summary>
	public static MetadataConsumerFieldDescriptor[] Describe(MetadataConsumerType type)
		=> type switch
		{
			MetadataConsumerType.KODI =>
			[
				new("seriesMetadata", "Series metadata", "tvshow.nfo", true),
				new("seriesMetadataUrl", "Series metadata URL", "Append the TheTVDB url", false),
				new("episodeMetadata", "Episode metadata", "<filename>.nfo", true),
				new("seriesImages", "Series images", "fanart.jpg, poster.jpg, banner.jpg", true),
				new("seasonImages", "Season images", "season##-poster.jpg, season##-banner.jpg", true),
				new("episodeImages", "Episode images", "<filename>-thumb.jpg", true),
				new("movieMetadata", "Movie metadata", "<filename>.nfo", true),
				new("movieMetadataUrl", "Movie metadata URL", "Append the TMDB and IMDB urls", false),
				new("movieImages", "Movie images", "fanart.jpg, poster.jpg", true)
			],
			MetadataConsumerType.PLEX =>
			[
				new("seriesPlexMatchFile", "Series Plex match file", ".plexmatch", true),
				new("episodeMappings", "Episode mappings", "Include an Episode: line per file", false)
			],
			MetadataConsumerType.EMBY =>
			[
				new("movieMetadata", "Movie metadata", "movie.xml", true)
			],
			MetadataConsumerType.ROKSBOX =>
			[
				new("episodeMetadata", "Episode metadata", "<filename>.xml", true),
				new("seriesImages", "Series images", "<series folder>.jpg", true),
				new("seasonImages", "Season images", "<season folder>.jpg", true),
				new("episodeImages", "Episode images", "<filename>.jpg", true),
				new("movieMetadata", "Movie metadata", "<filename>.xml", true),
				new("movieImages", "Movie images", "<filename>-poster.jpg, <filename>-fanart.jpg", true)
			],
			MetadataConsumerType.WDTV =>
			[
				new("episodeMetadata", "Episode metadata", "<filename>.xml", true),
				new("seriesImages", "Series images", "folder.jpg", true),
				new("seasonImages", "Season images", "Season##/folder.jpg", true),
				new("episodeImages", "Episode images", "<filename>.metathumb", true),
				new("movieMetadata", "Movie metadata", "<filename>.xml", true),
				new("movieImages", "Movie images", "folder.jpg", true)
			],
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown metadata consumer type")
		};

	private static MetadataConsumerSettings Default(MetadataConsumerType type)
		=> type switch
		{
			MetadataConsumerType.KODI => new KodiMetadataConsumerSettings(),
			MetadataConsumerType.PLEX => new PlexMetadataConsumerSettings(),
			MetadataConsumerType.EMBY => new EmbyMetadataConsumerSettings(),
			MetadataConsumerType.ROKSBOX => new RoksboxMetadataConsumerSettings(),
			MetadataConsumerType.WDTV => new WdtvMetadataConsumerSettings(),
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown metadata consumer type")
		};

	private static Type SettingsType(MetadataConsumerType type)
		=> type switch
		{
			MetadataConsumerType.KODI => typeof(KodiMetadataConsumerSettings),
			MetadataConsumerType.PLEX => typeof(PlexMetadataConsumerSettings),
			MetadataConsumerType.EMBY => typeof(EmbyMetadataConsumerSettings),
			MetadataConsumerType.ROKSBOX => typeof(RoksboxMetadataConsumerSettings),
			MetadataConsumerType.WDTV => typeof(WdtvMetadataConsumerSettings),
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown metadata consumer type")
		};
}
