using Submarine.Core.Quality;
using Xunit;

namespace Submarine.Core.Test.Quality
{
	public class RevisionTest
	{
		[Theory]
		[InlineData(1, 2, 1)]
		[InlineData(2, 1, -1)]
		[InlineData(1, 1, 0)]
		[InlineData(null, 1, 1)]
		public void CompareTo_ShouldCompareRevisionByVersion(int? first, int second, int result)
		{
			var firstRevision = first != null ? new Revision((int) first) : null;
			var secondRevision = new Revision(second);
			
			Assert.Equal(result, secondRevision.CompareTo(firstRevision));
		}
	}
}
