using Punkartuchera.Core.Matches;

namespace Punkartuchera.Core.Tests;

public class DiceRollTests
{
    [Fact]
    public void Roll_GivesValuesFrom1To10_AndEveryFaceAppears()
    {
        var random = new Random(1234);
        var seen = new HashSet<int>();
        for (var i = 0; i < 2000; i++)
        {
            foreach (var value in DiceRoll.Roll(1, random))
            {
                Assert.InRange(value, 1, DiceRoll.Faces);
                seen.Add(value);
            }
        }

        Assert.Equal(Enumerable.Range(1, 10), seen.Order());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(6, 6)]
    [InlineData(40, 6)]
    public void Roll_ClampsTheNumberOfDice(int asked, int expected)
    {
        Assert.Equal(expected, DiceRoll.Roll(asked, new Random(7)).Count);
    }
}
