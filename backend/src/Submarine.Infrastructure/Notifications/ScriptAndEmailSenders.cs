using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using Submarine.Core.Enums;
using Submarine.Core.Notifications;

namespace Submarine.Infrastructure.Notifications;

/// <summary>Kodi sender showing on screen notifications and triggering library updates via JSON-RPC.</summary>
public sealed class KodiSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	/// <inheritdoc />
	public NotificationType Type => NotificationType.KODI;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (KodiSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var isMediaEvent = message.EventType
			is NotificationEventType.IMPORT
			or NotificationEventType.UPGRADE
			or NotificationEventType.RENAME
			or NotificationEventType.DELETE;

		if (settings.Notify)
		{
			await PostRpcAsync(settings, new JsonRpcRequest
			{
				Id = 1,
				Method = "GUI.ShowNotification",
				Params = new GuiNotificationParams
				{
					Title = message.Title,
					Message = message.Body,
					DisplayTime = settings.DisplayTime * 1000
				}
			}, cancellationToken);
		}

		if (settings.UpdateLibrary && (isMediaEvent || settings.AlwaysUpdate))
		{
			await PostRpcAsync(settings, new JsonRpcRequest { Id = 2, Method = "VideoLibrary.Scan" }, cancellationToken);
		}

		if (settings.CleanLibrary && message.EventType == NotificationEventType.DELETE)
		{
			await PostRpcAsync(settings, new JsonRpcRequest { Id = 3, Method = "VideoLibrary.Clean" }, cancellationToken);
		}
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);

	private async Task PostRpcAsync(KodiSettings settings, JsonRpcRequest payload, CancellationToken cancellationToken)
	{
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		var url = $"{(settings.UseSsl ? "https" : "http")}://{settings.Host}:{settings.Port}/jsonrpc";
		using var request = new HttpRequestMessage(HttpMethod.Post, url)
		{
			Content = JsonContent.Create(payload, options: new JsonSerializerOptions(JsonSerializerDefaults.Web))
		};
		if (!string.IsNullOrEmpty(settings.Username))
		{
			request.Headers.Authorization = NotificationSenderHttp.BasicAuth(settings.Username, settings.Password);
		}

		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	internal sealed class JsonRpcRequest
	{
		[JsonPropertyName("jsonrpc")]
		public string JsonRpc { get; set; } = "2.0";

		public string Method { get; set; } = string.Empty;

		public object? Params { get; set; }

		public int Id { get; set; }
	}

	internal sealed class GuiNotificationParams
	{
		public string Title { get; set; } = string.Empty;

		public string Message { get; set; } = string.Empty;

		public int DisplayTime { get; set; }
	}
}

/// <summary>Custom script sender executing a local process with the event as environment variables.</summary>
public sealed partial class CustomScriptSender(ILogger<CustomScriptSender> logger) : INotificationSender
{
	private static readonly TimeSpan ScriptTimeout = TimeSpan.FromSeconds(30);

	/// <inheritdoc />
	public NotificationType Type => NotificationType.CUSTOM_SCRIPT;

	/// <inheritdoc />
	public Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (CustomScriptSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		return RunAsync(BuildStartInfo(settings, message), cancellationToken);
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);

	internal ProcessStartInfo BuildStartInfo(CustomScriptSettings settings, NotificationMessage message)
	{
		var startInfo = new ProcessStartInfo(settings.Path)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};
		if (!string.IsNullOrEmpty(settings.Arguments))
		{
			startInfo.ArgumentList.Add(settings.Arguments);
		}

		var environment = startInfo.Environment;
		environment["submarine_eventtype"] = message.EventType.ToString();
		environment["submarine_series_id"] = message.SeriesId?.ToString() ?? string.Empty;
		environment["submarine_movie_id"] = message.MovieId?.ToString() ?? string.Empty;
		environment["submarine_media_title"] = message.MediaTitle;
		environment["submarine_year"] = message.Year?.ToString() ?? string.Empty;
		environment["submarine_quality"] = NotificationDispatcher.FormatQuality(message.Quality);
		environment["submarine_languages"] = string.Join(", ", message.Languages.Select(l => l.ToString()));
		environment["submarine_releasegroup"] = message.ReleaseGroup ?? string.Empty;
		environment["submarine_indexer"] = message.Indexer ?? string.Empty;
		environment["submarine_downloadclient"] = message.DownloadClient ?? string.Empty;
		environment["submarine_downloadid"] = message.DownloadId ?? string.Empty;
		environment["submarine_size"] = message.Size?.ToString() ?? string.Empty;
		environment["submarine_path"] = message.Path ?? string.Empty;

		return startInfo;
	}

	internal async Task RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
	{
		using var process = Process.Start(startInfo)
			?? throw new InvalidOperationException($"Failed to start script {startInfo.FileName}");

		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(ScriptTimeout);

		string output;
		string errors;
		try
		{
			output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
			errors = await process.StandardError.ReadToEndAsync(timeout.Token);
			await process.WaitForExitAsync(timeout.Token);
		}
		catch (OperationCanceledException)
		{
			try
			{
				process.Kill(entireProcessTree: true);
			}
			catch (InvalidOperationException)
			{
				// process exited between the cancellation and the kill
			}

			if (cancellationToken.IsCancellationRequested)
			{
				throw;
			}

			logger.LogWarning("Script {Script} timed out after {Timeout} and was killed",
				startInfo.FileName, ScriptTimeout);
			return;
		}

		if (process.ExitCode != 0)
		{
			logger.LogWarning("Script {Script} exited with code {ExitCode}. Stdout: {Stdout}. Stderr: {Stderr}",
				startInfo.FileName, process.ExitCode, output, errors);
		}
	}
}

/// <summary>Email sender delivering a plain text mail via SMTP.</summary>
public sealed class EmailSender : INotificationSender
{
	/// <inheritdoc />
	public NotificationType Type => NotificationType.EMAIL;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (EmailSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var mime = BuildMimeMessage(message, settings);
		var secureSocket = settings.UseEncryption switch
		{
			EmailSettings.EncryptionType.NONE => SecureSocketOptions.None,
			EmailSettings.EncryptionType.SSL => SecureSocketOptions.SslOnConnect,
			_ => SecureSocketOptions.StartTls
		};

		using var client = new SmtpClient();
		await client.ConnectAsync(settings.Server, settings.Port, secureSocket, cancellationToken);
		try
		{
			if (!string.IsNullOrEmpty(settings.Username))
			{
				await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken);
			}

			await client.SendAsync(mime, cancellationToken);
		}
		finally
		{
			await client.DisconnectAsync(true, cancellationToken);
		}
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);

	internal static MimeMessage BuildMimeMessage(NotificationMessage message, EmailSettings settings)
	{
		var mime = new MimeMessage
		{
			Subject = message.Title,
			Body = new TextPart("plain") { Text = message.Body }
		};
		mime.From.Add(MailboxAddress.Parse(settings.From));
		foreach (var to in settings.To)
		{
			mime.To.Add(MailboxAddress.Parse(to));
		}

		foreach (var cc in settings.Cc)
		{
			mime.Cc.Add(MailboxAddress.Parse(cc));
		}

		foreach (var bcc in settings.Bcc)
		{
			mime.Bcc.Add(MailboxAddress.Parse(bcc));
		}

		return mime;
	}
}
