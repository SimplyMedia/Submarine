using Submarine.Core.Quality;
using Xunit;

namespace Submarine.Core.Test.Quality;

public class QualityResolutionTest
{
	[Fact]
	public void All_ShouldReturnMoreThanOneEntry_WhenAccessed()
		=> Assert.True(QualityResolutionModel.All.Length > 0);
}
