using Microsoft.AspNetCore.Http.HttpResults;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Notifications;
using Submarine.Infrastructure.Notifications;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Notifications;

/// <summary>
///     Notification connections: CRUD, schema and test.
/// </summary>
public sealed class NotificationsModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/notifications");
		group.MapGet("/", ListAsync);
		group.MapGet("/schema", SchemaAsync);
		group.MapPost("/test", TestUnsavedAsync);
		group.MapPost("/", CreateAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapPost("/{id:int}/test", TestSavedAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
		group.MapPost("/trakt/authorize", TraktAuthorizeAsync);
		group.MapPost("/trakt/poll", TraktPollAsync);
	}

	private static async Task<Results<Ok<TraktAuthorizeResponse>, ProblemHttpResult>> TraktAuthorizeAsync(
		TraktAuthorizeRequest request,
		ITraktAuthService authService,
		IConfiguration configuration,
		CancellationToken cancellationToken)
	{
		try
		{
			var clientId = TraktCredentials.RequireClientId(request.ClientId, configuration);
			var code = await authService.StartDeviceFlowAsync(clientId, cancellationToken);
			return TypedResults.Ok(new TraktAuthorizeResponse(code.DeviceCode, code.UserCode, code.VerificationUrl, code.ExpiresIn, code.Interval));
		}
		catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Could not start Trakt authorization", detail: ex.Message);
		}
	}

	private static async Task<Results<Ok<TraktPollResponse>, ProblemHttpResult>> TraktPollAsync(
		TraktPollRequest request,
		ITraktAuthService authService,
		IConfiguration configuration,
		TimeProvider timeProvider,
		CancellationToken cancellationToken)
	{
		try
		{
			var clientId = TraktCredentials.RequireClientId(request.ClientId, configuration);
			var clientSecret = TraktCredentials.RequireClientSecret(request.ClientSecret, configuration);
			var result = await authService.PollDeviceFlowAsync(clientId, clientSecret, request.DeviceCode, cancellationToken);
			if (result.Status != TraktPollStatus.AUTHORIZED || result.Tokens is not { } tokens)
			{
				return TypedResults.Ok(new TraktPollResponse(result.Status.ToString(), null, null, null));
			}

			var expiresAt = timeProvider.GetUtcNow().UtcDateTime.AddSeconds(tokens.ExpiresIn);
			return TypedResults.Ok(new TraktPollResponse(result.Status.ToString(), tokens.AccessToken, tokens.RefreshToken, expiresAt));
		}
		catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Could not poll Trakt authorization", detail: ex.Message);
		}
	}

	private static async Task<Ok<List<NotificationDto>>> ListAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var notifications = await db.Notifications.AsNoTracking()
			.Include(n => n.Tags)
			.OrderBy(n => n.Id)
			.ToListAsync(cancellationToken);
		return TypedResults.Ok(notifications.Select(ToDto).ToList());
	}

	private static Ok<List<NotificationSchemaDto>> SchemaAsync()
		=> TypedResults.Ok(NotificationSettingsJson.DescribeAll()
			.Select(pair => new NotificationSchemaDto(
				pair.Key.ToString(),
				pair.Value,
				NotificationCapabilities.SupportedEvents(pair.Key).Select(e => e.ToString()).ToList()))
			.ToList());

	private static async Task<Created<NotificationDto>> CreateAsync(
		NotificationConfigurationService configuration,
		IValidator<SaveNotificationRequest> validator,
		SaveNotificationRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateAndThrowAsync(request, cancellationToken);
		var notification = await configuration.SaveAsync(null, ToConfiguration(request), cancellationToken);
		return TypedResults.Created($"/api/v1/notifications/{notification.Id}", ToDto(notification));
	}

	private static async Task<Ok<NotificationDto>> UpdateAsync(
		int id,
		NotificationConfigurationService configuration,
		IValidator<SaveNotificationRequest> validator,
		SaveNotificationRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateAndThrowAsync(request, cancellationToken);
		var notification = await configuration.SaveAsync(id, ToConfiguration(request), cancellationToken);
		return TypedResults.Ok(ToDto(notification));
	}

	private static async Task<NoContent> DeleteAsync(int id, NotificationConfigurationService configuration, CancellationToken cancellationToken)
	{
		await configuration.DeleteAsync(id, cancellationToken);
		return TypedResults.NoContent();
	}

	private static async Task<Results<NoContent, ProblemHttpResult>> TestSavedAsync(
		int id,
		SubmarineDbContext db,
		INotificationSenderFactory factory,
		CancellationToken cancellationToken)
	{
		var notification = await db.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Notification {id} does not exist");
		return await TestAsync(factory, notification.Type, notification.SettingsJson, cancellationToken);
	}

	private static async Task<Results<NoContent, ProblemHttpResult>> TestUnsavedAsync(
		INotificationSenderFactory factory,
		IValidator<TestNotificationRequest> validator,
		TestNotificationRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateAndThrowAsync(request, cancellationToken);
		return await TestAsync(factory, request.Type, request.SettingsJson, cancellationToken);
	}

	private static async Task<Results<NoContent, ProblemHttpResult>> TestAsync(
		INotificationSenderFactory factory,
		NotificationType type,
		string settingsJson,
		CancellationToken cancellationToken)
	{
		try
		{
			await factory.Resolve(type).TestAsync(settingsJson, cancellationToken);
			return TypedResults.NoContent();
		}
		catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
		{
			return TypedResults.Problem(
				statusCode: StatusCodes.Status400BadRequest,
				title: "Test notification failed",
				detail: ex.Message);
		}
	}

	private static NotificationDto ToDto(Notification notification)
		=> new(
			notification.Id,
			notification.Name,
			notification.Type.ToString(),
			notification.Enable,
			notification.SettingsJson,
			notification.OnGrab,
			notification.OnImport,
			notification.OnUpgrade,
			notification.OnRename,
			notification.OnDelete,
			notification.OnHealthIssue,
			notification.OnHealthRestored,
			notification.OnApplicationUpdate,
			notification.OnManualInteractionRequired,
			notification.IncludeHealthWarnings,
			notification.Tags.Select(t => t.Label).OrderBy(l => l, StringComparer.OrdinalIgnoreCase).ToList());

	private static NotificationConfiguration ToConfiguration(SaveNotificationRequest request)
		=> new(
			request.Name, request.Type, request.Enable, request.SettingsJson, request.OnGrab, request.OnImport,
			request.OnUpgrade, request.OnRename, request.OnDelete, request.OnHealthIssue, request.OnHealthRestored,
			request.OnApplicationUpdate, request.OnManualInteractionRequired, request.IncludeHealthWarnings, request.Tags ?? []);


}

/// <summary>Create or replace a notification request.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Type">Notification implementation.</param>
/// <param name="Enable">Whether the notification is active.</param>
/// <param name="SettingsJson">Implementation settings as JSON.</param>
/// <param name="OnGrab">Notify on grab.</param>
/// <param name="OnImport">Notify on import.</param>
/// <param name="OnUpgrade">Notify on upgrade.</param>
/// <param name="OnRename">Notify on rename.</param>
/// <param name="OnDelete">Notify on delete.</param>
/// <param name="OnHealthIssue">Notify on health issues.</param>
/// <param name="OnHealthRestored">Notify on restored health.</param>
/// <param name="OnApplicationUpdate">Notify on application updates.</param>
/// <param name="OnManualInteractionRequired">Notify on manual interaction.</param>
/// <param name="IncludeHealthWarnings">Also notify for warnings and notices.</param>
/// <param name="Tags">Tag labels restricting the notification.</param>
public sealed record SaveNotificationRequest(
	string Name,
	NotificationType Type,
	bool Enable = true,
	string SettingsJson = "{}",
	bool OnGrab = false,
	bool OnImport = true,
	bool OnUpgrade = true,
	bool OnRename = false,
	bool OnDelete = false,
	bool OnHealthIssue = false,
	bool OnHealthRestored = false,
	bool OnApplicationUpdate = false,
	bool OnManualInteractionRequired = false,
	bool IncludeHealthWarnings = false,
	IReadOnlyList<string>? Tags = null);

/// <summary>Test a notification with settings that are not saved yet.</summary>
/// <param name="Type">Notification implementation.</param>
/// <param name="SettingsJson">Implementation settings as JSON.</param>
public sealed record TestNotificationRequest(NotificationType Type, string SettingsJson);

/// <summary>Starts a Trakt OAuth device code flow.</summary>
/// <param name="ClientId">Trakt app client id, falls back to the instance's Trakt:ClientId setting.</param>
public sealed record TraktAuthorizeRequest(string? ClientId);

/// <summary>The device code the user must enter at the verification url.</summary>
public sealed record TraktAuthorizeResponse(string DeviceCode, string UserCode, string VerificationUrl, int ExpiresIn, int Interval);

/// <summary>Polls once for the outcome of a previously started device code flow.</summary>
/// <param name="ClientId">Trakt app client id, falls back to the instance's Trakt:ClientId setting.</param>
/// <param name="ClientSecret">Trakt app client secret, falls back to the instance's Trakt:ClientSecret setting.</param>
/// <param name="DeviceCode">Device code returned by <see cref="TraktAuthorizeResponse" />.</param>
public sealed record TraktPollRequest(string? ClientId, string? ClientSecret, string DeviceCode);

/// <summary>Outcome of a device code poll: PENDING, AUTHORIZED, DENIED or EXPIRED. Tokens are set only when AUTHORIZED.</summary>
public sealed record TraktPollResponse(string Status, string? AccessToken, string? RefreshToken, DateTime? ExpiresAt);

/// <summary>A saved notification.</summary>
public sealed record NotificationDto(
	int Id,
	string Name,
	string Type,
	bool Enable,
	string SettingsJson,
	bool OnGrab,
	bool OnImport,
	bool OnUpgrade,
	bool OnRename,
	bool OnDelete,
	bool OnHealthIssue,
	bool OnHealthRestored,
	bool OnApplicationUpdate,
	bool OnManualInteractionRequired,
	bool IncludeHealthWarnings,
	IReadOnlyList<string> Tags);

/// <summary>Field descriptors of one notification type.</summary>
/// <param name="Type">Notification implementation name.</param>
/// <param name="Fields">Field descriptors.</param>
/// <param name="SupportedEvents">Events this notification type can deliver, driving which toggles the UI shows.</param>
public sealed record NotificationSchemaDto(string Type, IReadOnlyList<NotificationFieldDescriptor> Fields, IReadOnlyList<string> SupportedEvents);

/// <summary>Validator for <see cref="SaveNotificationRequest" />.</summary>
public sealed class SaveNotificationRequestValidator : AbstractValidator<SaveNotificationRequest>
{
	/// <inheritdoc />
	public SaveNotificationRequestValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
		RuleFor(x => x.SettingsJson).NotEmpty().Must(BeJsonObject).WithMessage("must be a JSON object");
		RuleFor(x => x.SettingsJson).Custom(ValidateSettings);
	}

	private static bool BeJsonObject(string json)
		=> json.StartsWith('{') && json.EndsWith('}');

	private static void ValidateSettings(string json, ValidationContext<SaveNotificationRequest> context)
	{
		var result = NotificationSettingsJson.Validate(context.InstanceToValidate.Type, json);
		if (result.IsValid)
		{
			return;
		}

		foreach (var (field, messages) in result.Errors)
		{
			var property = field == "$" ? "SettingsJson" : field;
			foreach (var message in messages)
			{
				context.AddFailure(property, message);
			}
		}
	}
}

/// <summary>Validator for <see cref="TestNotificationRequest" />.</summary>
public sealed class TestNotificationRequestValidator : AbstractValidator<TestNotificationRequest>
{
	/// <inheritdoc />
	public TestNotificationRequestValidator()
	{
		RuleFor(x => x.SettingsJson).NotEmpty().Must(BeJsonObject).WithMessage("must be a JSON object");
		RuleFor(x => x.SettingsJson).Custom(ValidateSettings);
	}

	private static bool BeJsonObject(string json)
		=> json.StartsWith('{') && json.EndsWith('}');

	private static void ValidateSettings(string json, ValidationContext<TestNotificationRequest> context)
	{
		var result = NotificationSettingsJson.Validate(context.InstanceToValidate.Type, json);
		if (result.IsValid)
		{
			return;
		}

		foreach (var (field, messages) in result.Errors)
		{
			var property = field == "$" ? "SettingsJson" : field;
			foreach (var message in messages)
			{
				context.AddFailure(property, message);
			}
		}
	}
}
