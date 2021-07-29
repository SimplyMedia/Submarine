using Submarine.Core.Quality;
using Xunit;

namespace Submarine.Core.Test.Quality
{
	public class QualityResolutionTest
	{
		[Fact]
		public void Check_QualityArray_IsNotEmpty()
		{
			Assert.True(QualityResolutionModel.All.Length > 0);
		}
	}
}
