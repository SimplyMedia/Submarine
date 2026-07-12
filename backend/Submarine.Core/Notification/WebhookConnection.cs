namespace Submarine.Core.Notification;

/// <summary>
///     A Connection which sends a JSON payload to a generic webhook endpoint
/// </summary>
public class WebhookConnection : Connection
{
	/// <summary>
	///     Url to send the webhook payload to
	/// </summary>
	public string Url { get; set; } = string.Empty;

	/// <summary>
	///     Http method used to send the webhook payload
	/// </summary>
	public string Method { get; set; } = "POST";

	/// <summary>
	///     Username for optional basic authentication
	/// </summary>
	public string? Username { get; set; }

	/// <summary>
	///     Password for optional basic authentication
	/// </summary>
	public string? Password { get; set; }

	/// <summary>
	///     Creates a new instance of <see cref="WebhookConnection" />
	/// </summary>
	public WebhookConnection()
		=> Type = ConnectionType.WEBHOOK;
}
