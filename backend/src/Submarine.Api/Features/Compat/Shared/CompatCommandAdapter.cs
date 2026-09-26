using System.Text.Json;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Commands;

namespace Submarine.Api.Features.Compat.Shared;

/// <summary>Translates the supported Sonarr/Radarr command dialect to persisted native commands.</summary>
public static class CompatCommandAdapter
{
	public static bool TryCreate(JsonElement request, string facade, out ICommand command, out string error)
	{
		command = null!;
		error = "Command body must be a JSON object with a supported name.";
		if (request.ValueKind != JsonValueKind.Object
			|| !request.TryGetProperty("name", out var nameElement)
			|| nameElement.ValueKind != JsonValueKind.String)
		{
			return false;
		}

		var name = nameElement.GetString();
		if (string.IsNullOrWhiteSpace(name))
		{
			return false;
		}

		if (!IsSupportedForFacade(name, facade))
		{
			error = $"Unknown or invalid compatibility command '{name}'.";
			return false;
		}

		var fields = request.EnumerateObject().Where(property => !property.NameEquals("name")).ToArray();
		if (fields.GroupBy(property => property.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() != 1))
		{
			error = "Command body contains duplicate properties.";
			return false;
		}

		try
		{
			command = name switch
			{
				"MissingEpisodeSearch" when HasOnly(fields)
					=> new MissingEpisodeSearchCommand(),
				"RefreshMonitoredDownloads" when HasOnly(fields)
					=> new DownloadMonitorCommand(),
				"RssSync" when HasOnly(fields)
					=> new RssSyncCommand(),
				"Backup" when HasOnly(fields)
					=> new BackupCommand(),
				"SeriesSearch" when HasOnly(fields, "seriesId")
					&& TryInt(request, "seriesId", out var seriesId)
					=> new SeriesSearchCommand(seriesId),
				"SeasonSearch" when HasOnly(fields, "seriesId", "seasonNumber")
					&& TryInt(request, "seriesId", out var seasonSeriesId)
					&& TryInt(request, "seasonNumber", out var seasonNumber)
				=> new SeasonSearchCommand(seasonSeriesId, seasonNumber),
				"EpisodeSearch" when HasOnly(fields, "episodeIds")
					&& TryIds(request, "episodeIds", out var episodeIds)
				=> new EpisodeSearchCommand(episodeIds),
				"MoviesSearch" when HasOnly(fields, "movieIds")
					&& TryIds(request, "movieIds", out var movieIds)
				=> new MovieSearchCommand(movieIds),
				"RescanSeries" when HasOnly(fields, "seriesId")
					&& TryOptionalInt(request, "seriesId", out var rescanSeriesId)
				=> new RescanSeriesCommand(rescanSeriesId),
				"RescanMovie" when HasOnly(fields, "movieId")
					&& TryOptionalInt(request, "movieId", out var rescanMovieId)
				=> new RescanMovieCommand(rescanMovieId),
				"RefreshSeries" when HasOnly(fields, "seriesId")
					&& TryOptionalInt(request, "seriesId", out var refreshSeriesId)
				=> new RefreshSeriesCommand(refreshSeriesId),
				"RefreshMovie" when HasOnly(fields, "movieId")
					&& TryOptionalInt(request, "movieId", out var refreshMovieId)
				=> new RefreshMovieCommand(refreshMovieId),
				_ => null!
			};
		}
		catch (JsonException)
		{
			command = null!;
		}

		if (command is not null)
		{
			error = string.Empty;
			return true;
		}

		error = $"Unknown or invalid compatibility command '{name}'.";
		return false;
	}

	public static bool TryProject(Command row, string facade, out CompatCommandDto dto)
	{
		var upstreamName = row.Name switch
		{
			"SeriesSearch" => "SeriesSearch",
			"SeasonSearch" => "SeasonSearch",
			"EpisodeSearch" => "EpisodeSearch",
			"MovieSearch" => "MoviesSearch",
			"RescanSeries" => "RescanSeries",
			"RescanMovie" => "RescanMovie",
			"RefreshSeries" => "RefreshSeries",
			"RefreshMovie" => "RefreshMovie",
			"MissingEpisodeSearch" => "MissingEpisodeSearch",
			"DownloadMonitor" => "RefreshMonitoredDownloads",
			"RssSync" => "RssSync",
			"Backup" => "Backup",
			_ => null
		};
		if (upstreamName is null)
		{
			dto = null!;
			return false;
		}

		if (!IsSupportedForFacade(upstreamName, facade))
		{
			dto = null!;
			return false;
		}

		using var bodyDocument = JsonDocument.Parse(row.Body);
		var status = row.Status switch
		{
			CommandStatus.QUEUED => "queued",
			CommandStatus.RUNNING => "started",
			CommandStatus.COMPLETED => "completed",
			CommandStatus.FAILED => "failed",
			CommandStatus.CANCELLED => "aborted",
			_ => throw new ArgumentOutOfRangeException(nameof(row.Status), row.Status, "Unknown native command status")
		};
		var durationEnd = row.EndedAt ?? (row.StartedAt is null ? null : DateTime.UtcNow);
		var duration = row.StartedAt is { } started && durationEnd is { } ended
			? (ended - started).ToString("c", System.Globalization.CultureInfo.InvariantCulture)
			: null;
		dto = new CompatCommandDto(
			row.Id,
			upstreamName,
			bodyDocument.RootElement.Clone(),
			status,
			row.CreatedAt,
			row.StartedAt,
			row.EndedAt,
			duration,
			row.Message ?? (row.Status == CommandStatus.FAILED ? row.Exception : null),
			row.Progress);
		return true;
	}

	private static bool HasOnly(JsonProperty[] fields, params string[] allowed)
		=> fields.All(field => allowed.Contains(field.Name, StringComparer.OrdinalIgnoreCase));

	private static bool TryInt(JsonElement source, string property, out int value)
	{
		value = default;
		return TryProperty(source, property, out var element)
			&& element.ValueKind == JsonValueKind.Number
			&& element.TryGetInt32(out value)
			&& value > 0;
	}

	private static bool TryOptionalInt(JsonElement source, string property, out int? value)
	{
		value = null;
		if (!TryProperty(source, property, out var element))
		{
			return true;
		}

		if (element.ValueKind == JsonValueKind.Null)
		{
			return true;
		}

		if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var parsed) && parsed > 0)
		{
			value = parsed;
			return true;
		}

		return false;
	}

	private static bool TryIds(JsonElement source, string property, out IReadOnlyList<int> ids)
	{
		ids = [];
		if (!TryProperty(source, property, out var element) || element.ValueKind != JsonValueKind.Array)
		{
			return false;
		}

		var parsed = new List<int>();
		foreach (var item in element.EnumerateArray())
		{
			if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var id) || id <= 0)
			{
				return false;
			}

			parsed.Add(id);
		}

		if (parsed.Count == 0 || parsed.Distinct().Count() != parsed.Count)
		{
			return false;
		}

		ids = parsed;
		return true;
	}

	private static bool TryProperty(JsonElement source, string name, out JsonElement value)
	{
		foreach (var property in source.EnumerateObject())
		{
			if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				value = property.Value;
				return true;
			}
		}

		value = default;
		return false;
	}

	private static bool IsSupportedForFacade(string name, string facade)
	{
		if (string.Equals(facade, "sonarr", StringComparison.OrdinalIgnoreCase))
		{
			return name is "SeriesSearch" or "SeasonSearch" or "EpisodeSearch"
				or "MissingEpisodeSearch" or "RefreshMonitoredDownloads"
				or "RescanSeries" or "RefreshSeries" or "RssSync" or "Backup";
		}

		if (string.Equals(facade, "radarr", StringComparison.OrdinalIgnoreCase))
		{
			return name is "MoviesSearch" or "RescanMovie" or "RefreshMovie"
				or "RssSync" or "Backup";
		}

		return false;
	}
}

public sealed record CompatCommandDto(
	int Id,
	string Name,
	JsonElement Body,
	string Status,
	DateTime Queued,
	DateTime? Started,
	DateTime? Ended,
	string? Duration,
	string? Message,
	int Progress);
