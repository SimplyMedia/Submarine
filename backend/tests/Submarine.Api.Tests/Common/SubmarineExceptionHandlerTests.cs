using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;
using Submarine.Api.Common;
using Submarine.Infrastructure.Metadata;
using Xunit;

namespace Submarine.Api.Tests.Common;

public sealed class SubmarineExceptionHandlerTests
{
	[Fact]
	public async Task TryHandleAsync_ShouldMapSiblingServiceException_ToBadGatewayNamingTheService()
	{
		ProblemDetailsContext? captured = null;
		var problemDetailsService = Substitute.For<IProblemDetailsService>();
		problemDetailsService.TryWriteAsync(Arg.Do<ProblemDetailsContext>(context => captured = context))
			.Returns(true);
		var handler = new SubmarineExceptionHandler(problemDetailsService, NullLogger<SubmarineExceptionHandler>.Instance);
		var httpContext = new DefaultHttpContext();
		var exception = new SiblingServiceException("Metadata service", "Metadata service is unreachable: boom", new HttpRequestException("boom"));

		var handled = await handler.TryHandleAsync(httpContext, exception, TestContext.Current.CancellationToken);

		handled.ShouldBeTrue();
		httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status502BadGateway);
		captured.ShouldNotBeNull();
		captured!.ProblemDetails.Status.ShouldBe(StatusCodes.Status502BadGateway);
		captured.ProblemDetails.Detail.ShouldNotBeNull().ShouldContain("Metadata service");
	}

	[Fact]
	public async Task TryHandleAsync_ShouldMapUnknownException_ToGenericInternalServerError()
	{
		var problemDetailsService = Substitute.For<IProblemDetailsService>();
		problemDetailsService.TryWriteAsync(Arg.Any<ProblemDetailsContext>()).Returns(true);
		var handler = new SubmarineExceptionHandler(problemDetailsService, NullLogger<SubmarineExceptionHandler>.Instance);
		var httpContext = new DefaultHttpContext();

		await handler.TryHandleAsync(httpContext, new InvalidOperationException("boom"), TestContext.Current.CancellationToken);

		httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
	}
}
