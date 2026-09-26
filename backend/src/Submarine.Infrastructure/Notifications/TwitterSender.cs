using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Submarine.Core.Enums;
using Submarine.Core.Notifications;

namespace Submarine.Infrastructure.Notifications;

/// <summary>
///     Posts a tweet or direct message using OAuth1.0a credentials of a user supplied Twitter/X developer
///     app. There is no interactive authorization wizard: the four tokens must be obtained externally and
///     pasted into the settings, same as filling them by hand in the upstream advanced fields.
/// </summary>
public sealed class TwitterSender(IHttpClientFactory httpClientFactory) : INotificationSender
{
	private const string StatusUrl = "https://api.twitter.com/1.1/statuses/update.json";
	private const string DirectMessageUrl = "https://api.twitter.com/1.1/direct_messages/new.json";

	/// <inheritdoc />
	public NotificationType Type => NotificationType.TWITTER;

	/// <inheritdoc />
	public async Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default)
	{
		var settings = (TwitterSettings)NotificationSettingsJson.Parse(Type, settingsJson);
		var text = $"{message.Title}: {message.Body}";

		var (url, parameters) = settings.DirectMessage
			? (DirectMessageUrl, new Dictionary<string, string> { ["text"] = text, ["screen_name"] = settings.Mention ?? string.Empty })
			: (StatusUrl, new Dictionary<string, string> { ["status"] = string.IsNullOrEmpty(settings.Mention) ? text : $"{text} @{settings.Mention}" });

		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		using var request = new HttpRequestMessage(HttpMethod.Post, url)
		{
			Content = new FormUrlEncodedContent(parameters)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue(
			"OAuth",
			OAuth1Signer.BuildAuthorizationHeaderValue(HttpMethod.Post, url, parameters, settings));

		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public Task TestAsync(string settingsJson, CancellationToken cancellationToken = default)
		=> SendAsync(NotificationSenderHttp.TestMessage(), settingsJson, cancellationToken);
}

/// <summary>Minimal OAuth1.0a HMAC-SHA1 request signer for the Twitter/X v1.1 API.</summary>
internal static class OAuth1Signer
{
	/// <summary>Builds the value of an `Authorization: OAuth ...` header for a signed request.</summary>
	public static string BuildAuthorizationHeaderValue(
		HttpMethod method,
		string url,
		IReadOnlyDictionary<string, string> bodyParameters,
		TwitterSettings settings)
	{
		var oauthParameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
		{
			["oauth_consumer_key"] = settings.ConsumerKey,
			["oauth_nonce"] = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
			["oauth_signature_method"] = "HMAC-SHA1",
			["oauth_timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
			["oauth_token"] = settings.AccessToken,
			["oauth_version"] = "1.0"
		};

		var signingParameters = new SortedDictionary<string, string>(oauthParameters, StringComparer.Ordinal);
		foreach (var (key, value) in bodyParameters)
		{
			signingParameters[key] = value;
		}

		var parameterString = string.Join('&', signingParameters.Select(p => $"{Encode(p.Key)}={Encode(p.Value)}"));
		var baseString = $"{method.Method}&{Encode(url)}&{Encode(parameterString)}";
		var signingKey = $"{Encode(settings.ConsumerSecret)}&{Encode(settings.AccessTokenSecret)}";

		using var hmac = new HMACSHA1(Encoding.ASCII.GetBytes(signingKey));
		var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.ASCII.GetBytes(baseString)));
		oauthParameters["oauth_signature"] = signature;

		return string.Join(", ", oauthParameters.Select(p => $"{Encode(p.Key)}=\"{Encode(p.Value)}\""));
	}

	/// <summary>RFC 3986 percent-encoding, stricter than <see cref="Uri.EscapeDataString(string)" />'s reserved set.</summary>
	private static string Encode(string value)
		=> Uri.EscapeDataString(value).Replace("!", "%21").Replace("*", "%2A").Replace("'", "%27").Replace("(", "%28").Replace(")", "%29");
}
