using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;
using Submarine.Core.Languages;
using Submarine.Core.Quality;

namespace Submarine.Core.History;

/// <summary>
///     A record of something that happened to a tracked Media item, e.g. a grab or import
/// </summary>
public class HistoryEvent : ICreatable
{
	/// <summary>
	///     Id of the history event
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Type of the history event
	/// </summary>
	public HistoryEventType Type { get; set; }

	/// <summary>
	///     Id of the Series this event relates to, if any
	/// </summary>
	public int? SeriesId { get; set; }

	/// <summary>
	///     Id of the Episode this event relates to, if any
	/// </summary>
	public int? EpisodeId { get; set; }

	/// <summary>
	///     Id of the Movie this event relates to, if any
	/// </summary>
	public int? MovieId { get; set; }

	/// <summary>
	///     Source title of the event, e.g. the release title or old file path
	/// </summary>
	public string SourceTitle { get; set; } = string.Empty;

	/// <summary>
	///     Quality involved in the event, if any
	/// </summary>
	public QualityModel? Quality { get; set; }

	/// <summary>
	///     Languages involved in the event, if any
	/// </summary>
	public List<Language>? Languages { get; set; }

	/// <summary>
	///     Additional data of the event, e.g. indexer, download client or path
	/// </summary>
	public Dictionary<string, string> Data { get; set; } = new();

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }
}
