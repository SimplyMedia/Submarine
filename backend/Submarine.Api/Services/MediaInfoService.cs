using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Submarine.Core.MediaFile;

namespace Submarine.Api.Services;

/// <summary>
///     Extracts technical <see cref="MediaInfo" /> from media files via ffprobe
/// </summary>
public class MediaInfoService
{
	private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);

	private readonly string _ffprobePath;

	private readonly ILogger<MediaInfoService> _logger;

	public MediaInfoService(IConfiguration configuration, ILogger<MediaInfoService> logger)
	{
		_ffprobePath = configuration["FFprobe:Path"] ?? "ffprobe";
		_logger = logger;
	}

	/// <summary>
	///     Probes <paramref name="path" /> with ffprobe. Best-effort: returns null when ffprobe is missing,
	///     fails or times out, so probing never blocks an import.
	/// </summary>
	/// <param name="path">Path of the media file to probe</param>
	/// <param name="cancellationToken">Token to cancel the probe</param>
	/// <returns>The extracted <see cref="MediaInfo" />, or null when probing failed</returns>
	public async Task<MediaInfo?> ProbeAsync(string path, CancellationToken cancellationToken = default)
	{
		var json = await ExecuteFfprobeAsync(path, cancellationToken);

		if (json == null)
			return null;

		try
		{
			return Parse(json);
		}
		catch (JsonException ex)
		{
			_logger.LogDebug(ex, "Parsing ffprobe output for {Path} failed", path);
			return null;
		}
	}

	/// <summary>
	///     Runs ffprobe against <paramref name="path" /> and returns its JSON output, or null when ffprobe
	///     is missing, exits nonzero or times out
	/// </summary>
	/// <param name="path">Path of the media file to probe</param>
	/// <param name="cancellationToken">Token to cancel the probe</param>
	/// <returns>The ffprobe JSON output, or null when probing failed</returns>
	protected virtual async Task<string?> ExecuteFfprobeAsync(string path, CancellationToken cancellationToken)
	{
		using var process = new Process();

		process.StartInfo = new ProcessStartInfo
		{
			FileName = _ffprobePath,
			RedirectStandardOutput = true,
			UseShellExecute = false
		};

		foreach (var argument in new[] { "-v", "quiet", "-print_format", "json", "-show_streams", "-show_format", path })
			process.StartInfo.ArgumentList.Add(argument);

		try
		{
			process.Start();
		}
		catch (Exception ex)
		{
			_logger.LogDebug(ex, "ffprobe could not be started from {FfprobePath}", _ffprobePath);
			return null;
		}

		var output = process.StandardOutput.ReadToEndAsync(cancellationToken);

		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(ProbeTimeout);

		try
		{
			await process.WaitForExitAsync(timeout.Token);
		}
		catch (OperationCanceledException)
		{
			process.Kill(true);

			if (cancellationToken.IsCancellationRequested)
				throw;

			_logger.LogDebug("ffprobe timed out after {Timeout} for {Path}", ProbeTimeout, path);
			return null;
		}

		if (process.ExitCode != 0)
		{
			_logger.LogDebug("ffprobe exited with code {ExitCode} for {Path}", process.ExitCode, path);
			return null;
		}

		return await output;
	}

	private static MediaInfo Parse(string json)
	{
		using var document = JsonDocument.Parse(json);

		var info = new MediaInfo();

		if (document.RootElement.TryGetProperty("streams", out var streams))
			foreach (var stream in streams.EnumerateArray())
				switch (GetString(stream, "codec_type"))
				{
					case "video" when info.VideoCodec == null:
						info.VideoCodec = GetString(stream, "codec_name");
						info.Width = GetInt(stream, "width");
						info.Height = GetInt(stream, "height");
						info.VideoDynamicRange = GetString(stream, "color_transfer") switch
						{
							"smpte2084" => "HDR10",
							"arib-std-b67" => "HLG",
							_ => null
						};
						break;
					case "audio":
						info.AudioCodec ??= GetString(stream, "codec_name");

						if (GetInt(stream, "channels") is { } channels)
							info.AudioChannels = Math.Max(info.AudioChannels ?? 0, channels);

						if (GetLanguage(stream) is { } audioLanguage && !info.AudioLanguages.Contains(audioLanguage))
							info.AudioLanguages.Add(audioLanguage);
						break;
					case "subtitle":
						if (GetLanguage(stream) is { } subtitleLanguage &&
						    !info.SubtitleLanguages.Contains(subtitleLanguage))
							info.SubtitleLanguages.Add(subtitleLanguage);
						break;
				}

		if (document.RootElement.TryGetProperty("format", out var format) &&
		    GetString(format, "duration") is { } duration &&
		    double.TryParse(duration, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
			info.RuntimeSeconds = (int)Math.Round(seconds);

		return info;
	}

	private static string? GetString(JsonElement element, string property)
		=> element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;

	private static int? GetInt(JsonElement element, string property)
		=> element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetInt32()
			: null;

	private static string? GetLanguage(JsonElement stream)
		=> stream.TryGetProperty("tags", out var tags) ? GetString(tags, "language") : null;
}
