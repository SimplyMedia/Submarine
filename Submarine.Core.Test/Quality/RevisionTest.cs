using Submarine.Core.Quality;
using Xunit;

namespace Submarine.Core.Test.Quality;

public class RevisionTest
{
	[Theory]
	[InlineData(1, 2, 1)]
	[InlineData(2, 4, 1)]
	[InlineData(1, 3, 1)]
	public void CompareTo_ShouldReturnPositive_WhenSecondRevisionIsHigher(int? first, int second, int result)
		=> AssertCompareTo(first, second, result);

	[Theory]
	[InlineData(2, 1, -1)]
	[InlineData(4, 2, -1)]
	[InlineData(3, 1, -1)]
	public void CompareTo_ShouldReturnNegative_WhenFirstRevisionIsHigher(int? first, int second, int result)
		=> AssertCompareTo(first, second, result);

	[Theory]
	[InlineData(1, 1, 0)]
	[InlineData(2, 2, 0)]
	[InlineData(3, 3, 0)]
	public void CompareTo_ShouldReturnZero_WhenBothRevisionsAreTheSame(int? first, int second, int result)
		=> AssertCompareTo(first, second, result);

	[Fact]
	public void CompareTo_ShouldReturnPositive_WhenFirstRevisionIsNull()
		=> AssertCompareTo(null, 1, 1);

	[Theory]
	[InlineData(2, 1)]
	[InlineData(4, 2)]
	[InlineData(3, 1)]
	public void GreaterOperator_ShouldReturnTrue_WhenLeftIsGreater(int first, int second)
		=> Assert.True(new Revision(first) > new Revision(second));

	[Fact]
	public void GreaterOperator_ShouldReturnFalse_WhenLeftIsNull()
		=> Assert.False(null > new Revision());

	[Fact]
	public void GreaterOperator_ShouldReturnTrue_WhenRightIsNull()
		=> Assert.True(new Revision() > null);

	[Theory]
	[InlineData(1, 3)]
	[InlineData(0, 1)]
	[InlineData(2, 4)]
	public void LesserOperator_ShouldReturnTrue_WhenRightIsGreater(int first, int second)
		=> Assert.True(new Revision(first) < new Revision(second));

	[Fact]
	public void LesserOperator_ShouldReturnTrue_WhenLeftIsNull()
		=> Assert.True(null < new Revision());

	[Fact]
	public void LesserOperator_ShouldReturnFalse_WhenRightIsNull()
		=> Assert.False(new Revision() < null);

	[Theory]
	[InlineData(2, 1)]
	[InlineData(4, 2)]
	[InlineData(3, 1)]
	[InlineData(2, 2)]
	public void GreaterOrEqualOperator_ShouldReturnTrue_WhenLeftIsGreaterOrEqual(int first, int second)
		=> Assert.True(new Revision(first) >= new Revision(second));

	[Fact]
	public void GreaterOrEqualOperator_ShouldReturnFalse_WhenLeftIsNull()
		=> Assert.False(null >= new Revision());

	[Fact]
	public void GreaterOrEqualOperator_ShouldReturnTrue_WhenRightIsNull()
		=> Assert.True(new Revision() >= null);

	[Theory]
	[InlineData(1, 3)]
	[InlineData(0, 1)]
	[InlineData(2, 4)]
	[InlineData(2, 2)]
	public void LesserOrEqualOperator_ShouldReturnTrue_WhenRightIsGreaterOrEqual(int first, int second)
		=> Assert.True(new Revision(first) <= new Revision(second));

	[Fact]
	public void LesserOrEqualOperator_ShouldReturnTrue_WhenLeftIsNull()
		=> Assert.True(null <= new Revision());

	[Fact]
	public void LesserOrEqualOperator_ShouldReturnFalse_WhenRightIsNull()
		=> Assert.False(new Revision() <= null);

	private static void AssertCompareTo(int? first, int second, int result)
	{
		var firstRevision = first != null ? new Revision((int)first) : null;
		var secondRevision = new Revision(second);

		Assert.Equal(result, secondRevision.CompareTo(firstRevision));
	}
}
