using Submarine.Core.Languages;
using Submarine.Core.Quality;

namespace Submarine.Core.Notifications;

/// <summary>
///     Kind of event a notification is sent for.
/// </summary>
public enum NotificationEventType
{
	/// <summary>Release grabbed.</summary>
	GRAB,

	/// <summary>File imported.</summary>
	IMPORT,

	/// <summary>File upgraded.</summary>
	UPGRADE,

	/// <summary>Files renamed.</summary>
	RENAME,

	/// <summary>Media or files deleted.</summary>
	DELETE,

	/// <summary>Health issue detected.</summary>
	HEALTH,

	/// <summary>Health issue resolved.</summary>
	HEALTH_RESTORED,

	/// <summary>Manual interaction required.</summary>
	MANUAL_INTERACTION,

	/// <summary>Application update available.</summary>
	APPLICATION_UPDATE,

	/// <summary>Test message.</summary>
	TEST
}

/// <summary>
///     An episode referenced by a notification.
/// </summary>
/// <param name="Season">Season number.</param>
/// <param name="Number">Episode number.</param>
/// <param name="Title">Episode title.</param>
/// <param name="AirDate">Local air date as stored by the metadata provider.</param>
public sealed record NotificationEpisode(int Season, int Number, string? Title, string? AirDate);

/// <summary>
///     A link included in a notification.
/// </summary>
/// <param name="Label">Link label.</param>
/// <param name="Url">Link target.</param>
public sealed record NotificationLink(string Label, string Url);

/// <summary>
///     Payload handed to notification senders.
/// </summary>
/// <param name="EventType">Event that triggered the notification.</param>
/// <param name="Title">Short headline.</param>
/// <param name="Body">Human readable detail text.</param>
/// <param name="SeriesId">Related series id, when the event belongs to a series.</param>
/// <param name="MovieId">Related movie id, when the event belongs to a movie.</param>
/// <param name="MediaTitle">Series or movie title.</param>
/// <param name="Year">Release year of the media.</param>
/// <param name="Quality">Quality of the release or file.</param>
/// <param name="Languages">Languages of the release or file.</param>
/// <param name="ReleaseGroup">Release group of the release or file.</param>
/// <param name="Indexer">Indexer the release came from.</param>
/// <param name="DownloadClient">Download client that grabbed the release.</param>
/// <param name="DownloadId">Download client side id.</param>
/// <param name="Size">Release or file size in bytes.</param>
/// <param name="Path">File path on disk, when the event has one.</param>
/// <param name="ImageUrl">Poster or cover image url.</param>
/// <param name="Episodes">Episodes referenced by the event.</param>
/// <param name="Links">External links for the media.</param>
public sealed record NotificationMessage(
	NotificationEventType EventType,
	string Title,
	string Body,
	int? SeriesId,
	int? MovieId,
	string MediaTitle,
	int? Year,
	QualityModel? Quality,
	IReadOnlyList<Language> Languages,
	string? ReleaseGroup,
	string? Indexer,
	string? DownloadClient,
	string? DownloadId,
	long? Size,
	string? Path,
	string? ImageUrl,
	IReadOnlyList<NotificationEpisode> Episodes,
	IReadOnlyList<NotificationLink> Links);
