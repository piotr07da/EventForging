// ReSharper disable InconsistentNaming

using EventForging.CosmosDb.EventHandling;
using Xunit;

namespace EventForging.CosmosDb.Tests;

public class GroupsMerger_tests
{
    [Fact]
    public void empty_group_gives_empty_result()
    {
        var sut = CreateSut();
        var groups = new List<Group>();

        var result = sut.Merge(groups);

        Assert.Empty(result);
    }

    [Fact]
    public void single_group_gives_single_result()
    {
        var sut = CreateSut();
        var groups = new List<Group>
        {
            CreateGroup("group1", "item1", "item2"),
        };

        var result = sut.Merge(groups).ToArray();

        Assert.Single(result);
        AssertGroupHas(result, 0, "item1", "item2");
    }

    [Fact]
    public void two_groups_with_same_key_gives_single_result()
    {
        var sut = CreateSut();
        var groups = new List<Group>
        {
            CreateGroup("group1", "item1", "item2"),
            CreateGroup("group1", "item3", "item4"),
        };

        var result = sut.Merge(groups).ToArray();

        Assert.Single(result);
        AssertGroupHas(result, 0, "item1", "item2", "item3", "item4");
    }

    [Fact]
    public void two_groups_with_different_keys_gives_two_results()
    {
        var sut = CreateSut();
        var groups = new List<Group>
        {
            CreateGroup("group1", "item1", "item2"),
            CreateGroup("group2", "item3", "item4"),
        };

        var result = sut.Merge(groups).ToArray();

        Assert.Equal(2, result.Length);
        AssertGroupHas(result, 0, "item1", "item2");
        AssertGroupHas(result, 1, "item3", "item4");
    }

    [Fact]
    public void two_groups_with_same_key_and_one_empty_gives_single_result()
    {
        var sut = CreateSut();
        var groups = new List<Group>
        {
            CreateGroup("group1", "item1", "item2"),
            CreateGroup("group1"),
        };

        var result = sut.Merge(groups).ToArray();

        Assert.Single(result);
        AssertGroupHas(result, 0, "item1", "item2");
    }

    [Fact]
    public void two_groups_with_same_key_and_one_group_with_different_key_between_them_gives_two_results_from_two_separated_groups_being_merged_and_the_separating_group_at_the_end()
    {
        var sut = CreateSut();
        var groups = new List<Group>
        {
            CreateGroup("group1", "item1", "item2"),
            CreateGroup("group2", "item3", "item4"),
            CreateGroup("group1", "item5", "item6"),
        };

        var result = sut.Merge(groups).ToArray();

        Assert.Equal(2, result.Length);
        AssertGroupHas(result, 0, "item1", "item2", "item5", "item6");
        AssertGroupHas(result, 1, "item3", "item4");
    }

    [Fact]
    public void two_groups_with_same_key_and_one_group_with_different_key_after_them_gives_two_first_groups_merged_and_one_last_group()
    {
        var sut = CreateSut();
        var groups = new List<Group>
        {
            CreateGroup("group1", "item1", "item2"),
            CreateGroup("group1", "item3", "item4"),
            CreateGroup("group2", "item5", "item6"),
        };

        var result = sut.Merge(groups).ToArray();

        Assert.Equal(2, result.Length);
        AssertGroupHas(result, 0, "item1", "item2", "item3", "item4");
        AssertGroupHas(result, 1, "item5", "item6");
    }

    private GroupsMerger<Group, Item> CreateSut()
    {
        return new GroupsMerger<Group, Item>(g => g.Id, g => g.Items);
    }

    private Group CreateGroup(string id, params string[] items)
    {
        return new Group { Id = id, Items = items.Select(i => new Item { Id = i, }).ToArray(), };
    }

    private void AssertGroupHas(IEnumerable<IReadOnlyList<Item>> result, int groupIndex, params string[] items)
    {
        var group = result.ElementAt(groupIndex);
        Assert.Equal(items.Length, group.Count);
        for (var i = 0; i < items.Length; i++)
        {
            Assert.Equal(items[i], group[i].Id);
        }
    }

    private class Group
    {
        public string Id { get; set; } = string.Empty;
        public IReadOnlyList<Item> Items { get; set; } = Array.Empty<Item>();
    }

    private class Item
    {
        public string Id { get; set; } = string.Empty;
    }
}
