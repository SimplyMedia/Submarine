using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Submarine.Core.Enums;
using Submarine.Core.Notifications;

namespace Submarine.Infrastructure.Notifications;

/// <summary>Mailgun email API sender.</summary>
public sealed class MailgunSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	private const string BaseUrlUs = "https://api.mailgun.net/v3";
	private const string BaseUrlEu = "https://api.eu.mailgun.net/v3";

	/// <inheritdoc />
	public NotificationType Type => NotificationType.MAILGUN;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (MailgunSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		var baseUrl = settings.UseEuEndpoint ? BaseUrlEu : BaseUrlUs;

		var fields = new List<KeyValuePair<string, string>> { new("from", settings.From) };
		fields.AddRange(settings.Recipients.Select(recipient => new KeyValuePair<string, string>("to", recipient)));
		fields.Add(new KeyValuePair<string, string>("subject", message.Title));
		fields.Add(new KeyValuePair<string, string>("text", message.Body));

		using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/{settings.SenderDomain}/messages")
		{
			Content = new FormUrlEncodedContent(fields)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue(
			"Basic",
			Convert.ToBase64String(Encoding.UTF8.GetBytes($"api:{settings.ApiKey}")));

		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}

/// <summary>SendGrid email API sender.</summary>
public sealed class SendGridSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	private const string ApiUrl = "https://api.sendgrid.com/v3/mail/send";

	/// <inheritdoc />
	public NotificationType Type => NotificationType.SENDGRID;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (SendGridSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		var payload = new
		{
			personalizations = new[]
			{
				new
				{
					to = settings.Recipients.Select(recipient => new { email = recipient }).ToArray(),
					subject = message.Title
				}
			},
			from = new { email = settings.From },
			content = new[] { new { type = "text/plain", value = message.Body } }
		};

		using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
		{
			Content = JsonContent.Create(payload)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}
