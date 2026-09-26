using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.MediaFiles;

/// <summary>
///     Extracts media info by shelling out to ffprobe. Disabled transparently (returns null) when ffprobe
///     is not installed.
/// </summary>
public sealed class FfprobeMediaInfoService(ILogger<FfprobeMediaInfoService> logger) : IMediaInfoService
{
	/// <inheritdoc />
	public async Task<MediaInfoModel?> ProbeAsync(string filePath, CancellationToken cancellationToken = default)
	{
		var startInfo = new ProcessStartInfo("ffprobe")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		startInfo.ArgumentList.Add("-v");
		startInfo.ArgumentList.Add("quiet");
		startInfo.ArgumentList.Add("-print_format");
		startInfo.ArgumentList.Add("json");
		startInfo.ArgumentList.Add("-show_format");
		startInfo.ArgumentList.Add("-show_streams");
		startInfo.ArgumentList.Add(filePath);

		try
		{
			using var process = Process.Start(startInfo);
			if (process is null)
			{
				return null;
			}

			var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
			await process.WaitForExitAsync(cancellationToken);
			var json = await outputTask;

			return process.ExitCode == 0 ? MediaInfoJsonParser.Parse(json) : null;
		}
		catch (Win32Exception)
		{
			logger.LogWarning("ffprobe is not installed or not on PATH; skipping media info extraction for {Path}", filePath);
			return null;
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "ffprobe failed for {Path}", filePath);
			return null;
		}
	}
}
