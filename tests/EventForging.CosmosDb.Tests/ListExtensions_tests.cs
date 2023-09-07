// ReSharper disable InconsistentNaming

using Xunit;

namespace EventForging.CosmosDb.Tests;

public class ListExtensions_tests
{
    [Fact]
    public void when_SplitEvenly_called_with_empty_list_then_empty_list_returned()
    {
        var list = new List<int>();

        var result = list.SplitEvenly(1);

        Assert.Single(result);
        Assert.Empty(result[0]);
    }

    [Fact]
    public void when_SplitEvenly_called_with_list_of_1_element_and_target_amount_of_lists_1_then_list_with_1_element_returned()
    {
        var list = new List<int> { 1, };

        var result = list.SplitEvenly(1);

        Assert.Single(result);
        Assert.Single(result[0], 1);
    }

    [Fact]
    public void when_SplitEvenly_called_with_list_of_2_elements_and_target_amount_of_lists_1_then_list_with_2_elements_returned()
    {
        var list = new List<int> { 1, 2, };

        var result = list.SplitEvenly(1);

        Assert.Single(result);
        Assert.Equal(new[] { 1, 2, }, result[0]);
    }

    [Fact]
    public void when_SplitEvenly_called_with_list_of_2_elements_and_target_amount_of_lists_2_then_list_with_2_lists_each_with_1_element_returned()
    {
        var list = new List<int> { 1, 2, };

        var result = list.SplitEvenly(2);

        Assert.Equal(2, result.Count);
        Assert.Single(result[0], 1);
        Assert.Single(result[1], 2);
    }

    [Fact]
    public void when_SplitEvenly_called_with_list_of_6_elements_and_target_amount_of_lists_4_then_2_lists_with_2_elements_each_and_2_lists_with_1_element_each_returned()
    {
        var list = new List<int> { 1, 2, 3, 4, 5, 6, };

        var result = list.SplitEvenly(4);

        Assert.Equal(4, result.Count);
        Assert.Equal(new[] { 1, 2, }, result[0]);
        Assert.Equal(new[] { 3, 4, }, result[1]);
        Assert.Single(result[2], 5);
        Assert.Single(result[3], 6);
    }
}
