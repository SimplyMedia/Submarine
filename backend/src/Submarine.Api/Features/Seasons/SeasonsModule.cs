using FluentValidation;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Features.Series;
using Submarine.Infrastructure.Library;

namespace Submarine.Api.Features.Seasons;

/// <summary>
///     Season monitoring endpoints under the series resource.
/// </summary>
public sealed class SeasonsModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/series/{seriesId:int}/seasons");
		group.MapPut("/{seasonNumber:int}/monitor", MonitorAsync);
		group.MapPut("/season-pass", SeasonPassAsync);
	}

	private static async Task<NoContent> MonitorAsync(
		LibraryMutator mutator,
		int seriesId,
		int seasonNumber,
		[FromBody] SeasonMonitorRequest request,
		CancellationToken cancellationToken)
	{
		await mutator.SetSeasonMonitoredAsync(seriesId, seasonNumber, request.Monitored, cancellationToken);
		return TypedResults.NoContent();
	}

	private static async Task<NoContent> SeasonPassAsync(
		LibraryMutator mutator,
		IValidator<SeasonPassRequest> validator,
		int seriesId,
		[FromBody] SeasonPassRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		await mutator.ApplySeasonPassAsync(
			seriesId,
			(request.Seasons ?? []).ToDictionary(x => x.SeasonNumber, x => x.Monitored),
			request.MonitoringOption,
			request.MonitorSpecials ?? false,
			cancellationToken);
		return TypedResults.NoContent();
	}
}

/// <summary>Season monitor request.</summary>
/// <param name="Monitored">New monitored flag.</param>
public sealed record SeasonMonitorRequest(bool Monitored);

/// <summary>Validator for <see cref="SeasonMonitorRequest" />.</summary>
public sealed class SeasonMonitorRequestValidator : AbstractValidator<SeasonMonitorRequest>
{
	/// <inheritdoc />
	public SeasonMonitorRequestValidator()
	{
		RuleFor(x => x.Monitored).NotNull();
	}
}

/// <summary>Validator for <see cref="SeasonPassRequest" />.</summary>
public sealed class SeasonPassRequestPostValidator : AbstractValidator<SeasonPassRequest>
{
	/// <inheritdoc />
	public SeasonPassRequestPostValidator()
	{
		RuleFor(x => x.MonitoringOption).IsInEnum().When(x => x.MonitoringOption.HasValue);
		RuleFor(x => x)
			.Must(x => x.Seasons is { Count: > 0 } || x.MonitoringOption.HasValue)
			.WithMessage("Either seasons or a monitoring option is required");
		RuleForEach(x => x.Seasons).ChildRules(season =>
		{
			season.RuleFor(x => x.SeasonNumber).GreaterThanOrEqualTo(0);
		}).When(x => x.Seasons is not null);
	}
}
