using System.Security.Cryptography;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Auth;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Config;

/// <summary>
///     General and UI configuration endpoints.
/// </summary>
public sealed class ConfigModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var general = endpoints.MapGroup("/api/v1/config/general");
		general.MapGet("/", GetGeneralAsync);
		general.MapPut("/", PutGeneralAsync);
		general.MapPost("/api-key", RegenerateApiKeyAsync);
		general.MapPost("/feed-token", RegenerateFeedTokenAsync);

		var ui = endpoints.MapGroup("/api/v1/config/ui");
		ui.MapGet("/", GetUiAsync);
		ui.MapPut("/", PutUiAsync);
	}

	private static async Task<Ok<GeneralConfig>> GetGeneralAsync(SubmarineDbContext db, CancellationToken cancellationToken)
		=> TypedResults.Ok(await db.GeneralConfig.AsNoTracking().SingleAsync(cancellationToken));

	private static async Task<Ok<GeneralConfig>> PutGeneralAsync(
		SubmarineDbContext db,
		IAuthConfigProvider authConfigProvider,
		IValidator<GeneralConfig> validator,
		GeneralConfig request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var config = await db.GeneralConfig.SingleAsync(cancellationToken);
		config.AuthMethod = request.AuthMethod;
		// FeedToken is server-generated only; see POST /api/v1/config/general/feed-token.
		config.UrlBase = request.UrlBase;
		config.InstanceName = request.InstanceName;
		config.LogLevel = request.LogLevel;
		config.Branch = request.Branch;
		config.UpdateAutomatically = request.UpdateAutomatically;
		await db.SaveChangesAsync(cancellationToken);
		authConfigProvider.Invalidate();
		return TypedResults.Ok(config);
	}

	private static async Task<Ok<ApiKeyDto>> RegenerateApiKeyAsync(
		SubmarineDbContext db,
		IAuthConfigProvider authConfigProvider,
		CancellationToken cancellationToken)
	{
		var config = await db.GeneralConfig.SingleAsync(cancellationToken);
		config.ApiKey = RandomNumberGenerator.GetHexString(32);
		await db.SaveChangesAsync(cancellationToken);
		authConfigProvider.Invalidate();
		return TypedResults.Ok(new ApiKeyDto(config.ApiKey));
	}

	private static async Task<Ok<FeedTokenDto>> RegenerateFeedTokenAsync(
		SubmarineDbContext db,
		CancellationToken cancellationToken)
	{
		var config = await db.GeneralConfig.SingleAsync(cancellationToken);
		config.FeedToken = RandomNumberGenerator.GetHexString(32);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(new FeedTokenDto(config.FeedToken));
	}

	private static async Task<Ok<UiConfig>> GetUiAsync(SubmarineDbContext db, CancellationToken cancellationToken)
		=> TypedResults.Ok(await db.UiConfig.AsNoTracking().SingleAsync(cancellationToken));

	private static async Task<Ok<UiConfig>> PutUiAsync(
		SubmarineDbContext db,
		IValidator<UiConfig> validator,
		UiConfig request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var config = await db.UiConfig.SingleAsync(cancellationToken);
		config.Theme = request.Theme;
		config.FirstDayOfWeek = request.FirstDayOfWeek;
		config.CalendarWeekColumnHeader = request.CalendarWeekColumnHeader;
		config.ShortDateFormat = request.ShortDateFormat;
		config.LongDateFormat = request.LongDateFormat;
		config.TimeFormat = request.TimeFormat;
		config.ShowRelativeDates = request.ShowRelativeDates;
		config.Language = request.Language;
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(config);
	}
}

/// <summary>Regenerated API key.</summary>
/// <param name="ApiKey">The new API key.</param>
public sealed record ApiKeyDto(string ApiKey);

/// <summary>Regenerated feed token.</summary>
/// <param name="FeedToken">The new feed token.</param>
public sealed record FeedTokenDto(string FeedToken);

/// <summary>Validator for <see cref="GeneralConfig" /> PUT requests.</summary>
public sealed class GeneralConfigValidator : AbstractValidator<GeneralConfig>
{
	/// <inheritdoc />
	public GeneralConfigValidator()
	{
		RuleFor(x => x.UrlBase)
			.Matches(@"^/[A-Za-z0-9._~\-/]*[A-Za-z0-9._~\-]$")
			.When(x => !string.IsNullOrEmpty(x.UrlBase))
			.WithMessage("UrlBase must be empty or an absolute path without a trailing slash");
	}
}

/// <summary>Validator for <see cref="UiConfig" /> PUT requests.</summary>
public sealed class UiConfigValidator : AbstractValidator<UiConfig>
{
	/// <inheritdoc />
	public UiConfigValidator()
	{
		RuleFor(x => x.Theme).IsInEnum();
		RuleFor(x => x.FirstDayOfWeek).InclusiveBetween(0, 6);
	}
}
