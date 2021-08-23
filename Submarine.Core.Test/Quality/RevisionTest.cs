using Submarine.Core.Quality;
using Xunit;

namespace Submarine.Core.Test.Quality
{
	public class RevisionTest
	{
		[Theory]
		[InlineData(1, 2, 1)]
		[InlineData(2, 4, 1)]
		[InlineData(1, 3, 1)]
		public void CompareTo_ShouldReturnPositive_WhenSecondRevisionIsHigher(int? first, int second, int result)
			=> AssertRevision(first, second, result);

		[Theory]
		[InlineData(2, 1, -1)]
		[InlineData(4, 2, -1)]
		[InlineData(3, 1, -1)]
		public void CompareTo_ShouldReturnNegative_WhenFirstRevisionIsHigher(int? first, int second, int result)
			=> AssertRevision(first, second, result);
		
		[Theory]
		[InlineData(1, 1, 0)]
		public void CompareTo_ShouldReturnZero_WhenBothRevisionsAreTheSame(int? first, int second, int result)
			=> AssertRevision(first, second, result);

		[Fact]
		public void CompareTo_ShouldReturnPositive_WhenFirstRevisionIsNull()
			=> AssertRevision(null, 1, 1);

		private void AssertRevision(int? first, int second, int result)
		{
			var firstRevision = first != null ? new Revision((int) first) : null;
			var secondRevision = new Revision(second);
			
			Assert.Equal(result, secondRevision.CompareTo(firstRevision));
		}
	}
}
