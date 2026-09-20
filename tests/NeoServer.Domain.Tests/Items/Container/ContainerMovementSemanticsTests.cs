using NeoServer.Domain.Common.Contracts.Items;
using NeoServer.Domain.Common.Contracts.Items.Types;
using NeoServer.Domain.Common.Contracts.World.Tiles;
using NeoServer.Domain.Common.Location;
using NeoServer.Domain.Common.Location.Structs;
using NeoServer.Domain.Tests.Helpers;
using NeoServer.Domain.Tests.Helpers.Player;
using NeoServer.Domain.World.Models.Tiles;

namespace NeoServer.Domain.Tests.Items.Container;

public class ContainerMovementSemanticsTests
{
    [Fact]
    [Trait("Category", "EdgeCase")]
    public void Player_splits_stack_inside_same_container_without_rejoining_source()
    {
        // Arrange
        var sourceStack = ItemTestDataBuilder.CreateCumulativeItem(100, 10);
        var container = ItemTestDataBuilder.CreateContainer(4);
        container.AddItem(sourceStack);
        var player = PlayerTestDataBuilder.Build();

        // Act
        var result = player.MoveItem(sourceStack, container, container, 2,
            FindPosition(container, sourceStack), 1);

        // Assert
        result.Succeeded.Should().BeTrue();
        container.SlotsUsed.Should().Be(2);
        container[0].Amount.Should().Be(2);
        container[1].Should().BeSameAs(sourceStack);
        sourceStack.Amount.Should().Be(8);
    }

    [Fact]
    [Trait("Category", "HappyPath")]
    public void Player_merges_same_container_split_when_targeting_compatible_stack()
    {
        // Arrange
        var sourceStack = ItemTestDataBuilder.CreateCumulativeItem(110, 10);
        var container = ItemTestDataBuilder.CreateContainer(4);
        container.AddItem(sourceStack);
        var player = PlayerTestDataBuilder.Build();
        player.MoveItem(sourceStack, container, container, 2, FindPosition(container, sourceStack), 1);
        var targetStack = container[0];

        // Act
        var result = player.MoveItem(sourceStack, container, container, 3,
            FindPosition(container, sourceStack), FindPosition(container, targetStack));

        // Assert
        result.Succeeded.Should().BeTrue();
        targetStack.Amount.Should().Be(5);
        sourceStack.Amount.Should().Be(5);
        container.SlotsUsed.Should().Be(2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(ContainerIndex.Wherever)]
    [InlineData(10)]
    [Trait("Category", "HappyPath")]
    public void Player_auto_merges_first_compatible_stack_when_moving_between_containers(byte destinationPosition)
    {
        // Arrange
        var sourceStack = ItemTestDataBuilder.CreateCumulativeItem(120, 10);
        var targetStack = ItemTestDataBuilder.CreateCumulativeItem(120, 90);
        var source = ItemTestDataBuilder.CreateContainer(4, children: [sourceStack]);
        var destination = ItemTestDataBuilder.CreateContainer(4, children: [targetStack]);
        var player = PlayerTestDataBuilder.Build();

        // Act
        var result = player.MoveItem(sourceStack, source, destination, 5,
            FindPosition(source, sourceStack), destinationPosition);

        // Assert
        result.Succeeded.Should().BeTrue();
        targetStack.Amount.Should().Be(95);
        sourceStack.Amount.Should().Be(5);
        destination.SlotsUsed.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public void Player_does_not_auto_merge_same_container_stack_when_destination_is_wherever()
    {
        // Arrange
        var sourceStack = ItemTestDataBuilder.CreateCumulativeItem(130, 10);
        var container = ItemTestDataBuilder.CreateContainer(4, children: [sourceStack]);
        var player = PlayerTestDataBuilder.Build();

        // Act
        var result = player.MoveItem(sourceStack, container, container, 2,
            FindPosition(container, sourceStack), ContainerIndex.Wherever);

        // Assert
        result.Succeeded.Should().BeTrue();
        container.SlotsUsed.Should().Be(2);
        container[0].Amount.Should().Be(2);
        sourceStack.Amount.Should().Be(8);
    }

    [Fact]
    [Trait("Category", "HappyPath")]
    public void Player_moves_item_to_parent_container_with_move_up_index()
    {
        // Arrange
        var child = ItemTestDataBuilder.CreateContainer(4);
        var parent = ItemTestDataBuilder.CreateContainer(4, children: [child]);
        var item = ItemTestDataBuilder.CreatePotion(140);
        var source = ItemTestDataBuilder.CreateContainer(4, children: [item]);
        var player = PlayerTestDataBuilder.Build();

        // Act
        var result = player.MoveItem(item, source, child, 1,
            FindPosition(source, item), ContainerIndex.MoveUp);

        // Assert
        result.Succeeded.Should().BeTrue();
        parent.Items.Should().Contain(item);
        child.SlotsUsed.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "HappyPath")]
    public void Player_keeps_move_up_destination_in_current_container_when_it_has_no_container_parent()
    {
        // Arrange
        var item = ItemTestDataBuilder.CreatePotion(145);
        var source = ItemTestDataBuilder.CreateContainer(4, children: [item]);
        var destination = ItemTestDataBuilder.CreateContainer(4);
        var player = PlayerTestDataBuilder.Build();

        // Act
        var result = player.MoveItem(item, source, destination, 1,
            FindPosition(source, item), ContainerIndex.MoveUp);

        // Assert
        result.Succeeded.Should().BeTrue();
        destination.Items.Should().Contain(item);
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public void Player_merges_then_creates_another_stack_when_target_stack_overflows()
    {
        // Arrange
        var sourceStack = ItemTestDataBuilder.CreateCumulativeItem(146, 100);
        var targetStack = ItemTestDataBuilder.CreateCumulativeItem(146, 90);
        var source = ItemTestDataBuilder.CreateContainer(4, children: [sourceStack]);
        var destination = ItemTestDataBuilder.CreateContainer(2, children: [targetStack]);
        var player = PlayerTestDataBuilder.Build();

        // Act
        var result = player.MoveItem(sourceStack, source, destination, 100,
            FindPosition(source, sourceStack), ContainerIndex.Wherever);

        // Assert
        result.Succeeded.Should().BeTrue();
        source.SlotsUsed.Should().Be(0);
        destination.SlotsUsed.Should().Be(2);
        targetStack.Amount.Should().Be(100);
        destination[0].Amount.Should().Be(90);
    }

    [Fact]
    [Trait("Category", "Validation")]
    public void Player_does_not_remove_item_when_destination_container_is_full()
    {
        // Arrange
        var item = ItemTestDataBuilder.CreatePotion(147);
        var source = ItemTestDataBuilder.CreateContainer(2, children: [item]);
        var destination = ItemTestDataBuilder.CreateContainer(1,
            children: [ItemTestDataBuilder.CreatePotion(148)]);
        var player = PlayerTestDataBuilder.Build();

        // Act
        var result = player.MoveItem(item, source, destination, 1,
            FindPosition(source, item), ContainerIndex.Wherever);

        // Assert
        result.Failed.Should().BeTrue();
        source.Items.Should().Contain(item);
        destination.Items.Should().NotContain(item);
    }

    [Fact]
    [Trait("Category", "HappyPath")]
    public void Player_redirects_item_into_targeted_child_container()
    {
        // Arrange
        var child = ItemTestDataBuilder.CreateContainer(4);
        var parent = ItemTestDataBuilder.CreateContainer(4, children: [child]);
        var item = ItemTestDataBuilder.CreatePotion(150);
        var source = ItemTestDataBuilder.CreateContainer(4, children: [item]);
        var player = PlayerTestDataBuilder.Build();

        // Act
        var result = player.MoveItem(item, source, parent, 1,
            FindPosition(source, item), FindPosition(parent, child));

        // Assert
        result.Succeeded.Should().BeTrue();
        child.Items.Should().Contain(item);
        parent.Items.Should().Contain(child);
    }

    [Fact]
    [Trait("Category", "Validation")]
    public void Player_cannot_move_container_into_its_descendant()
    {
        // Arrange
        var child = ItemTestDataBuilder.CreateContainer(4);
        var parent = ItemTestDataBuilder.CreateContainer(4, children: [child]);
        IDynamicTile tile = new DynamicTile(new Coordinate(100, 100, 7), TileFlag.None, null, [], []);
        tile.AddItem(parent);
        var player = PlayerTestDataBuilder.Build();

        // Act
        var result = player.MoveItem(parent, tile, child, 1, 0, null);

        // Assert
        result.Failed.Should().BeTrue();
        tile.TopDownItemOnStack.Should().BeSameAs(parent);
        child.Items.Should().NotContain(parent);
    }

    private static byte FindPosition(IContainer container, IItem item)
    {
        for (var position = 0; position < container.SlotsUsed; position++)
        {
            if (ReferenceEquals(container[position], item))
                return (byte)position;
        }

        throw new InvalidOperationException("Item was not found in the expected container.");
    }
}
