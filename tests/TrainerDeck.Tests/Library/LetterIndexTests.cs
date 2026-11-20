using TrainerDeck.Core.Library;

namespace TrainerDeck.Tests.Library;

public class LetterIndexTests
{
    private static readonly char[] Items = ['#', 'B', 'B', 'E', 'S', 'W'];

    [Fact]
    public void Steps_skip_letters_without_items()
    {
        var available = LetterIndex.Available(Items);
        Assert.Equal('E', LetterIndex.Step('B', 1, available));
        Assert.Equal('B', LetterIndex.Step('E', -1, available));
        Assert.Equal('W', LetterIndex.Step('S', 5, available));
        Assert.Equal('#', LetterIndex.Step('#', -1, available));
        Assert.Equal('B', LetterIndex.Step('A', 1, available));
    }

    [Theory]
    [InlineData('B', 1)]
    [InlineData('S', 4)]
    [InlineData('C', 3)]
    [InlineData('Z', 5)]
    [InlineData('#', 0)]
    public void Finds_first_item_for_letter(char letter, int expected) =>
        Assert.Equal(expected, LetterIndex.FirstIndex(Items, letter));

    [Fact]
    public void Empty_list_has_no_index() => Assert.Equal(-1, LetterIndex.FirstIndex([], 'A'));
}
