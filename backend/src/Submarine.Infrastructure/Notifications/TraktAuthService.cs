using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace Submarine.Infrastructure.Notifications;

/// <summary>Result of starting a Trakt OAuth device code flow.</summary>
public sealed record TraktDeviceCode(string DeviceCode, string UserCode, string VerificationUrl, int ExpiresIn, int Interval);

/// <summary>Outcome of a single device code poll.</summary>
public enum TraktPollStatus
{
	/// <summary>The user has not finished authorizing yet; poll again after the interval.</summary>
	PENDING,

	/// <summary>The user approved the request; tokens are populated.</summary>
	AUTHORIZED,

	/// <summary>The user denied the request.</summary>
	DENIED,

	/// <summary>The device code expired before the user authorized it.</summary>
	EXPIRED
}

/// <summary>A poll result, or a completed token exchange.</summary>
public sealed record TraktPollResult(TraktPollStatus Status, TraktTokens? Tokens);

/// <summary>An OAuth token pair returned by the device token exchange or a refresh.</summary>
public sealed record TraktTokens(string AccessToken, string RefreshToken, int ExpiresIn);

/// <summary>
///     Talks to Trakt's OAuth device code flow (https://trakt.docs.apiary.io/#reference/authentication-devices)
///     and token refresh endpoint. Client id/secret are supplied per call since they can come from either the
///     notification's own settings or the instance wide Trakt:ClientId/Trakt:ClientSecret fallback.
/// </summary>
public interface ITraktAuthService
{
	/// <summary>Starts a device code flow, returning the code the user must enter at the verification url.</summary>
	Task<TraktDeviceCode> StartDeviceFlowAsync(string clientId, CancellationToken cancellationToken = default);

	/// <summary>Polls once for the outcome of a previously started device code flow.</summary>
	Task<TraktPollResult> PollDeviceFlowAsync(string clientId, string clientSecret, string deviceCode, CancellationToken cancellationToken = default);

	/// <summary>Exchanges a refresh token for a new access/refresh token pair.</summary>
	Task<TraktTokens> RefreshAsync(string clientId, string clientSecret, string refreshToken, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class TraktAuthService(IHttpClientFactory httpClientFactory) : ITraktAuthService
{
	private const string BaseUrl = "https://api.trakt.tv";

	/// <inheritdoc />
	public async Task<TraktDeviceCode> StartDeviceFlowAsync(string clientId, CancellationToken cancellationToken = default)
	{
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		var response = await client.PostAsJsonAsync($"{BaseUrl}/oauth/device/code", new { client_id = clientId }, cancellationToken);
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<DeviceCodeResponse>(cancellationToken: cancellationToken)
			?? throw new InvalidOperationException("Trakt returned an empty device code response");
		return new TraktDeviceCode(body.DeviceCode, body.UserCode, body.VerificationUrl, body.ExpiresIn, body.Interval);
	}

	/// <inheritdoc />
	public async Task<TraktPollResult> PollDeviceFlowAsync(string clientId, string clientSecret, string deviceCode, CancellationToken cancellationToken = default)
	{
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		var response = await client.PostAsJsonAsync($"{BaseUrl}/oauth/device/token", new
		{
			code = deviceCode,
			client_id = clientId,
			client_secret = clientSecret
		}, cancellationToken);

		switch (response.StatusCode)
		{
			case HttpStatusCode.OK:
				var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken)
					?? throw new InvalidOperationException("Trakt returned an empty token response");
				return new TraktPollResult(TraktPollStatus.AUTHORIZED, new TraktTokens(body.AccessToken, body.RefreshToken, body.ExpiresIn));
			case HttpStatusCode.BadRequest: // pending
				return new TraktPollResult(TraktPollStatus.PENDING, null);
			case HttpStatusCode.Gone: // expired
				return new TraktPollResult(TraktPollStatus.EXPIRED, null);
			case (HttpStatusCode)418: // denied
			case HttpStatusCode.Conflict: // already used
				return new TraktPollResult(TraktPollStatus.DENIED, null);
			default:
				throw new InvalidOperationException($"Trakt device token poll failed with status {(int)response.StatusCode}");
		}
	}

	/// <inheritdoc />
	public async Task<TraktTokens> RefreshAsync(string clientId, string clientSecret, string refreshToken, CancellationToken cancellationToken = default)
	{
		var client = httpClientFactory.CreateClient(NotificationSenderFactory.HttpClientName);
		var response = await client.PostAsJsonAsync($"{BaseUrl}/oauth/token", new
		{
			refresh_token = refreshToken,
			client_id = clientId,
			client_secret = clientSecret,
			grant_type = "refresh_token"
		}, cancellationToken);
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken)
			?? throw new InvalidOperationException("Trakt returned an empty refresh response");
		return new TraktTokens(body.AccessToken, body.RefreshToken, body.ExpiresIn);
	}

	private sealed class DeviceCodeResponse
	{
		[JsonPropertyName("device_code")]
		public string DeviceCode { get; set; } = string.Empty;

		[JsonPropertyName("user_code")]
		public string UserCode { get; set; } = string.Empty;

		[JsonPropertyName("verification_url")]
		public string VerificationUrl { get; set; } = string.Empty;

		[JsonPropertyName("expires_in")]
		public int ExpiresIn { get; set; }

		public int Interval { get; set; }
	}

	private sealed class TokenResponse
	{
		[JsonPropertyName("access_token")]
		public string AccessToken { get; set; } = string.Empty;

		[JsonPropertyName("refresh_token")]
		public string RefreshToken { get; set; } = string.Empty;

		[JsonPropertyName("expires_in")]
		public int ExpiresIn { get; set; }
	}
}

/// <summary>
///     Resolves the Trakt client id/secret to use for a notification: the notification's own settings,
///     falling back to the instance wide Trakt:ClientId / Trakt:ClientSecret configuration (same fallback used
///     by the Trakt import list).
/// </summary>
public static class TraktCredentials
{
	/// <summary>Resolves the client id, throwing when neither the settings nor the configuration provide one.</summary>
	public static string RequireClientId(string? settingsClientId, IConfiguration configuration)
		=> settingsClientId is { Length: > 0 }
			? settingsClientId
			: configuration["Trakt:ClientId"] ?? throw new InvalidOperationException("Trakt notification needs a client id");

	/// <summary>Resolves the client secret, throwing when neither the settings nor the configuration provide one.</summary>
	public static string RequireClientSecret(string? settingsClientSecret, IConfiguration configuration)
		=> settingsClientSecret is { Length: > 0 }
			? settingsClientSecret
			: configuration["Trakt:ClientSecret"] ?? throw new InvalidOperationException("Trakt notification needs a client secret");
}
