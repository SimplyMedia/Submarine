using Submarine.Api.Features.Config;
using Submarine.Core.Entities;
using Shouldly;
using Xunit;

namespace Submarine.Api.Tests.System;

public sealed class GeneralConfigValidatorTests
{
	[Fact]
	public void Validate_ShouldRejectInvalidProxyBypassRegex()
	{
		var result = new GeneralConfigValidator().Validate(new GeneralConfig { ProxyBypassFilter = "[" });

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(error => error.PropertyName == nameof(GeneralConfig.ProxyBypassFilter));
	}
}
