using System.ComponentModel;
using System.Diagnostics;
using Submarine.Core.Enums;
using Submarine.Core.Notifications;

namespace Submarine.Infrastructure.Notifications;

/// <summary>
///     Invokes the Synology DiskStation `synoindex` binary, abstracted so tests can substitute a fake.
/// </summary>
public interface ISynologyIndexerProcess
{
	/// <summary>Runs `synoindex` with the given arguments.</summary>
	/// <param name="arguments">Command line arguments passed to the binary.</param>
	/// <param name="treatStdOutAsError">Whether any standard output also indicates failure; true for every command except `--help`.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">The binary is unavailable or returned an error.</exception>
	Task RunAsync(string arguments, bool treatStdOutAsError = true, CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="ISynologyIndexerProcess" />, shelling out to the binary bundled on Synology NAS devices.</summary>
public sealed class SynologyIndexerProcess : ISynologyIndexerProcess
{
	private const string BinaryPath = "/usr/syno/bin/synoindex";

	/// <inheritdoc />
	public async Task RunAsync(string arguments, bool treatStdOutAsError = true, CancellationToken cancellationToken = default)
	{
		try
		{
			var startInfo = new ProcessStartInfo(BinaryPath)
			{
				Arguments = arguments,
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start synoindex");
			var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
			var error = await process.StandardError.ReadToEndAsync(cancellationToken);
			await process.WaitForExitAsync(cancellationToken);
			if ((treatStdOutAsError && !string.IsNullOrWhiteSpace(output)) || !string.IsNullOrWhiteSpace(error))
			{
				throw new InvalidOperationException($"synoindex returned an error: {(string.IsNullOrWhiteSpace(error) ? output : error)}");
			}
		}
		catch (Exception ex) when (ex is Win32Exception or System.IO.FileNotFoundException)
		{
			throw new InvalidOperationException("synoindex is not available on this system", ex);
		}
	}
}

/// <summary>
///     Synology DiskStation media indexer sender. Updates the built-in media index directly on disk instead
///     of calling an HTTP API, so it only works when Submarine runs on the NAS itself.
/// </summary>
public sealed class SynologyIndexerSender(ISynologyIndexerProcess process) : INotificationSender
{
	/// <inheritdoc />
	public NotificationType Type => NotificationType.SYNOLOGY_INDEXER;

	/// <inheritdoc />
	public Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (SynologyIndexerSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		if (!settings.UpdateLibrary || message.Path is null)
		{
			return Task.CompletedTask;
		}

		return message.EventType switch
		{
			NotificationEventType.IMPORT or NotificationEventType.UPGRADE => process.RunAsync($"-a {Escape(message.Path)}", cancellationToken: cancellationToken),
			NotificationEventType.DELETE => process.RunAsync($"-d {Escape(message.Path)}", cancellationToken: cancellationToken),
			NotificationEventType.RENAME => process.RunAsync($"-R {Escape(System.IO.Path.GetDirectoryName(message.Path) ?? message.Path)}", cancellationToken: cancellationToken),
			_ => Task.CompletedTask
		};
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> process.RunAsync("--help", treatStdOutAsError: false, cancellationToken: cancellationToken);

	private static string Escape(string path) => $"\"{path.Replace("\"", "\\\"")}\"";
}
