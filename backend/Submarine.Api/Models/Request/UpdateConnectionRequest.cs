namespace Submarine.Api.Models.Request;

public record UpdateConnectionRequest
{
	public string? Name { get; set; }

	public bool? Enable { get; set; }

	public string? Host { get; set; }

	public int? Port { get; set; }

	public bool? UseSsl { get; set; }

	public string? ApiKey { get; set; }

	public bool? OnGrab { get; set; }

	public bool? OnImport { get; set; }

	public bool? OnRename { get; set; }

	public List<string>? Tags { get; set; }

	public string? WebhookUrl { get; set; }

	public string? BotToken { get; set; }

	public string? ChatId { get; set; }

	public string? Url { get; set; }

	public string? Method { get; set; }

	public string? Username { get; set; }

	public string? Password { get; set; }
}
