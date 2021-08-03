using Submarine.Core.Quality;
using Xunit;

namespace Submarine.Core.Test.Quality
{
	public class QualityResolutionTest
	{
		[Fact]
		public void All_ShouldNotBeEmpty() 
			=> Assert.True(QualityResolutionModel.All.Length > 0);
	}
}
