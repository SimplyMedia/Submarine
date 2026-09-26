using Submarine.Core.Naming;
using Shouldly;
using Xunit;

namespace Submarine.Core.Tests.Naming;

public class NamingPreviewTest
{
	[Fact]
	public void Samples_ShouldRenderEveryNamingTemplate()
	{
		var samples = NamingPreview.Samples(new Core.Entities.NamingConfig(), new NamingService());

		samples.ShouldNotBeEmpty();
		samples.Select(sample => sample.Name).ShouldBeUnique();
		samples.ShouldAllBe(sample => !string.IsNullOrWhiteSpace(sample.Preview));
	}
}
