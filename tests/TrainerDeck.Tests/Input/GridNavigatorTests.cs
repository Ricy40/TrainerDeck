using TrainerDeck.Core.Input;

namespace TrainerDeck.Tests.Input;

public class GridNavigatorTests
{
    // 4 columns, 10 items:
    //  0  1  2  3
    //  4  5  6  7
    //  8  9
    [Theory]
    [InlineData(5, InputAction.Left, 4)]
    [InlineData(4, InputAction.Left, 4)]
    [InlineData(2, InputAction.Right, 3)]
    [InlineData(3, InputAction.Right, 3)]
    [InlineData(9, InputAction.Right, 9)]
    [InlineData(1, InputAction.Up, 1)]
    [InlineData(5, InputAction.Up, 1)]
    [InlineData(1, InputAction.Down, 5)]
    [InlineData(5, InputAction.Down, 9)]
    [InlineData(6, InputAction.Down, 9)]  // partial last row: land on last item
    [InlineData(7, InputAction.Down, 9)]
    [InlineData(9, InputAction.Down, 9)]
    [InlineData(9, InputAction.PageUp, 1)]
    [InlineData(6, InputAction.PageUp, 2)]
    [InlineData(1, InputAction.PageDown, 9)]
    [InlineData(2, InputAction.PageDown, 9)]
    [InlineData(0, InputAction.PageDown, 8)]
    [InlineData(6, InputAction.Home, 0)]
    [InlineData(2, InputAction.End, 9)]
    [InlineData(2, InputAction.Activate, 2)]
    public void Moves_in_grid(int from, InputAction action, int expected) =>
        Assert.Equal(expected, GridNavigator.Move(from, 10, 4, action));

    [Fact]
    public void Empty_grid_has_no_focus() => Assert.Equal(-1, GridNavigator.Move(0, 0, 4, InputAction.Down));

    [Fact]
    public void Out_of_range_index_is_clamped()
    {
        Assert.Equal(0, GridNavigator.Move(-1, 5, 3, InputAction.Left));
        Assert.Equal(4, GridNavigator.Move(99, 5, 3, InputAction.Right));
    }

    [Fact]
    public void Single_column_behaves_like_a_list()
    {
        Assert.Equal(3, GridNavigator.Move(2, 5, 1, InputAction.Down));
        Assert.Equal(2, GridNavigator.Move(2, 5, 1, InputAction.Right));
        Assert.Equal(2, GridNavigator.Move(2, 5, 0, InputAction.Left));
    }
}
