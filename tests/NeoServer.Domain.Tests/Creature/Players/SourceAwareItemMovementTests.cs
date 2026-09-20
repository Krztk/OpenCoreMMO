using NeoServer.Domain.Common.Contracts.Creatures;
using NeoServer.Domain.Common.Contracts.Items;
using NeoServer.Domain.Common.Contracts.Items.Types;
using NeoServer.Domain.Common.Contracts.World.Tiles;
using NeoServer.Domain.Common.Location;
using NeoServer.Domain.Common.Location.Structs;
using NeoServer.Domain.Creatures.Player.Inventory;
using NeoServer.Domain.Tests.Helpers;
using NeoServer.Domain.Tests.Helpers.Player;
using NeoServer.Domain.World.Models.Tiles;

namespace NeoServer.Domain.Tests.Creature.Players;

public class SourceAwareItemMovementTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [Trait("Category", "Validation")]
    public void Player_cannot_equip_two_handed_weapon_from_ground_when_either_hand_is_occupied(
        bool equipWeapon,
        bool equipShield)
    {
        // Arrange
        var twoHandedWeapon = ItemTestDataBuilder.CreateWeaponItem(90, weaponType: "axe", twoHanded: true);
        var equippedWeapon = equipWeapon ? ItemTestDataBuilder.CreateWeaponItem(91) : null;
        var equippedShield = equipShield ? ItemTestDataBuilder.CreateBodyEquipmentItem(92, "", "shield") : null;
        var player = CreatePlayer(null, equippedWeapon, equippedShield);
        IDynamicTile tile = new DynamicTile(new Coordinate(100, 100, 7), TileFlag.None, null, [],
            [twoHandedWeapon]);

        // Act
        var result = player.MoveItem(twoHandedWeapon, tile, player.Inventory, 1, 0, (byte)Slot.Left);

        // Assert
        result.Failed.Should().BeTrue();
        player.Inventory[Slot.Left].Should().BeSameAs(equippedWeapon);
        player.Inventory[Slot.Right].Should().BeSameAs(equippedShield);
        tile.TopDownItemOnStack.Should().BeSameAs(twoHandedWeapon);
    }

    [Fact]
    [Trait("Category", "HappyPath")]
    public void Player_equips_two_handed_weapon_when_source_backpack_has_two_free_slots()
    {
        // Arrange
        var twoHandedWeapon = ItemTestDataBuilder.CreateWeaponItem(100, weaponType: "axe", twoHanded: true);
        var sword = ItemTestDataBuilder.CreateWeaponItem(101);
        var shield = ItemTestDataBuilder.CreateBodyEquipmentItem(102, "", "shield");
        var backpack = CreateBackpackWithItems(twoHandedWeapon, 17);
        var player = CreatePlayer(backpack, sword, shield);

        // Act
        var result = player.MoveItem(twoHandedWeapon, backpack, player.Inventory, 1,
            FindPosition(backpack, twoHandedWeapon), (byte)Slot.Left);

        // Assert
        result.Succeeded.Should().BeTrue();
        player.Inventory[Slot.Left].Should().BeSameAs(twoHandedWeapon);
        player.Inventory[Slot.Right].Should().BeNull();
        backpack.Items.Should().Contain(sword).And.Contain(shield);
        backpack.SlotsUsed.Should().Be(19);
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public void Player_only_unequips_shield_when_two_handed_source_backpack_has_one_free_slot()
    {
        // Arrange
        var twoHandedWeapon = ItemTestDataBuilder.CreateWeaponItem(110, weaponType: "axe", twoHanded: true);
        var sword = ItemTestDataBuilder.CreateWeaponItem(111);
        var shield = ItemTestDataBuilder.CreateBodyEquipmentItem(112, "", "shield");
        var backpack = CreateBackpackWithItems(twoHandedWeapon, 18);
        var player = CreatePlayer(backpack, sword, shield);

        // Act
        var result = player.MoveItem(twoHandedWeapon, backpack, player.Inventory, 1,
            FindPosition(backpack, twoHandedWeapon), (byte)Slot.Left);

        // Assert
        result.Failed.Should().BeTrue();
        player.Inventory[Slot.Left].Should().BeSameAs(sword);
        player.Inventory[Slot.Right].Should().BeNull();
        backpack.Items.Should().Contain(twoHandedWeapon).And.Contain(shield);
        backpack.Items.Should().NotContain(sword);
        backpack.SlotsUsed.Should().Be(20);
    }

    [Fact]
    [Trait("Category", "Validation")]
    public void Player_keeps_equipment_when_two_handed_source_backpack_is_full()
    {
        // Arrange
        var twoHandedWeapon = ItemTestDataBuilder.CreateWeaponItem(120, weaponType: "axe", twoHanded: true);
        var sword = ItemTestDataBuilder.CreateWeaponItem(121);
        var shield = ItemTestDataBuilder.CreateBodyEquipmentItem(122, "", "shield");
        var backpack = CreateBackpackWithItems(twoHandedWeapon, 19);
        var player = CreatePlayer(backpack, sword, shield);

        // Act
        var result = player.MoveItem(twoHandedWeapon, backpack, player.Inventory, 1,
            FindPosition(backpack, twoHandedWeapon), (byte)Slot.Left);

        // Assert
        result.Failed.Should().BeTrue();
        player.Inventory[Slot.Left].Should().BeSameAs(sword);
        player.Inventory[Slot.Right].Should().BeSameAs(shield);
        backpack.Items.Should().Contain(twoHandedWeapon);
    }

    [Fact]
    [Trait("Category", "HappyPath")]
    public void Player_swaps_one_handed_weapon_into_exact_source_container()
    {
        // Arrange
        var incomingWeapon = ItemTestDataBuilder.CreateWeaponItem(130);
        var equippedWeapon = ItemTestDataBuilder.CreateWeaponItem(131, weaponType: "axe");
        var backpack = CreateBackpackWithItems(incomingWeapon, 18);
        var player = CreatePlayer(backpack, equippedWeapon);

        // Act
        var result = player.MoveItem(incomingWeapon, backpack, player.Inventory, 1,
            FindPosition(backpack, incomingWeapon), (byte)Slot.Left);

        // Assert
        result.Succeeded.Should().BeTrue();
        player.Inventory[Slot.Left].Should().BeSameAs(incomingWeapon);
        backpack.Items.Should().Contain(equippedWeapon);
        backpack.Items.Should().NotContain(incomingWeapon);
    }

    [Fact]
    [Trait("Category", "Validation")]
    public void Player_does_not_use_sibling_container_for_equipment_exchange()
    {
        // Arrange
        var incomingWeapon = ItemTestDataBuilder.CreateWeaponItem(140);
        var equippedWeapon = ItemTestDataBuilder.CreateWeaponItem(141, weaponType: "axe");
        var exactSource = ItemTestDataBuilder.CreateContainer(2, children:
        [
            incomingWeapon,
            ItemTestDataBuilder.CreatePotion(142)
        ]);
        var siblingContainer = ItemTestDataBuilder.CreateContainer(2);
        var backpack = ItemTestDataBuilder.CreateBackpack(items: [exactSource, siblingContainer]);
        var player = CreatePlayer(backpack, equippedWeapon);

        // Act
        var result = player.MoveItem(incomingWeapon, exactSource, player.Inventory, 1,
            FindPosition(exactSource, incomingWeapon), (byte)Slot.Left);

        // Assert
        result.Failed.Should().BeTrue();
        player.Inventory[Slot.Left].Should().BeSameAs(equippedWeapon);
        exactSource.Items.Should().Contain(incomingWeapon);
        siblingContainer.SlotsUsed.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "HappyPath")]
    public void Player_can_exchange_weapon_from_nested_carried_container()
    {
        // Arrange
        var incomingWeapon = ItemTestDataBuilder.CreateWeaponItem(145);
        var equippedWeapon = ItemTestDataBuilder.CreateWeaponItem(146, weaponType: "axe");
        var nestedSource = ItemTestDataBuilder.CreateContainer(2, children: [incomingWeapon]);
        var backpack = ItemTestDataBuilder.CreateBackpack(items: [nestedSource]);
        var player = CreatePlayer(backpack, equippedWeapon);

        // Act
        var result = player.MoveItem(incomingWeapon, nestedSource, player.Inventory, 1,
            FindPosition(nestedSource, incomingWeapon), (byte)Slot.Left);

        // Assert
        result.Succeeded.Should().BeTrue();
        player.Inventory[Slot.Left].Should().BeSameAs(incomingWeapon);
        nestedSource.Items.Should().Contain(equippedWeapon);
    }

    [Fact]
    [Trait("Category", "HappyPath")]
    public void Player_can_exchange_weapon_from_depot_source()
    {
        // Arrange
        var incomingWeapon = ItemTestDataBuilder.CreateWeaponItem(150);
        var equippedWeapon = ItemTestDataBuilder.CreateWeaponItem(151, weaponType: "axe");
        var locker = ItemTestDataBuilder.CreateLocker(items: [incomingWeapon]);
        var player = CreatePlayer(null, equippedWeapon);

        // Act
        var result = player.MoveItem(incomingWeapon, locker, player.Inventory, 1,
            FindPosition(locker, incomingWeapon), (byte)Slot.Left);

        // Assert
        result.Succeeded.Should().BeTrue();
        player.Inventory[Slot.Left].Should().BeSameAs(incomingWeapon);
        locker.Items.Should().Contain(equippedWeapon);
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public void Player_rechecks_capacity_after_each_depot_equipment_displacement()
    {
        // Arrange
        var incomingWeapon = ItemTestDataBuilder.CreateWeaponItem(155, weaponType: "axe", twoHanded: true,
            weight: 80);
        var equippedWeapon = ItemTestDataBuilder.CreateWeaponItem(156, weight: 40);
        var equippedShield = ItemTestDataBuilder.CreateBodyEquipmentItem(157, "", "shield", weight: 30);
        var depotSource = ItemTestDataBuilder.CreateContainer(4, children: [incomingWeapon]);
        ItemTestDataBuilder.CreateLocker(items: [depotSource]);
        var player = CreatePlayer(null, equippedWeapon, equippedShield, capacity: 80);

        // Act
        var result = player.MoveItem(incomingWeapon, depotSource, player.Inventory, 1,
            FindPosition(depotSource, incomingWeapon), (byte)Slot.Left);

        // Assert
        result.Succeeded.Should().BeTrue("both displaced items free enough capacity; returned {0}", result.Error);
        player.Inventory[Slot.Left].Should().BeSameAs(incomingWeapon);
        player.Inventory[Slot.Right].Should().BeNull();
        player.Inventory.TotalWeight.Should().Be(80);
        depotSource.Items.Should().Contain(equippedWeapon).And.Contain(equippedShield);
    }

    [Fact]
    [Trait("Category", "Validation")]
    public void Player_cannot_exchange_weapon_from_ground_rooted_container()
    {
        // Arrange
        var incomingWeapon = ItemTestDataBuilder.CreateWeaponItem(160);
        var equippedWeapon = ItemTestDataBuilder.CreateWeaponItem(161, weaponType: "axe");
        var externalContainer = ItemTestDataBuilder.CreateContainer(2, children: [incomingWeapon]);
        IDynamicTile tile = new DynamicTile(new Coordinate(100, 100, 7), TileFlag.None, null, [], []);
        tile.AddItem(externalContainer);
        var player = CreatePlayer(null, equippedWeapon);

        // Act
        var result = player.MoveItem(incomingWeapon, externalContainer, player.Inventory, 1,
            FindPosition(externalContainer, incomingWeapon), (byte)Slot.Left);

        // Assert
        result.Failed.Should().BeTrue();
        player.Inventory[Slot.Left].Should().BeSameAs(equippedWeapon);
        externalContainer.Items.Should().Contain(incomingWeapon);
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public void Player_does_not_count_carried_item_weight_twice_when_equipping_from_backpack()
    {
        // Arrange
        var incomingWeapon = ItemTestDataBuilder.CreateWeaponItem(170, weight: 40);
        var backpack = ItemTestDataBuilder.CreateBackpack(weight: 20, items: [incomingWeapon]);
        var player = CreatePlayer(backpack, capacity: 60);

        // Act
        var result = player.MoveItem(incomingWeapon, backpack, player.Inventory, 1,
            FindPosition(backpack, incomingWeapon), (byte)Slot.Left);

        // Assert
        result.Succeeded.Should().BeTrue();
        player.Inventory[Slot.Left].Should().BeSameAs(incomingWeapon);
        player.Inventory.TotalWeight.Should().Be(60);
    }

    private static IPlayer CreatePlayer(IContainer backpack, IItem weapon = null, IItem shield = null,
        uint capacity = 1_000)
    {
        var inventory = new Dictionary<Slot, (IItem Item, ushort Id)>();
        if (backpack is not null) inventory.Add(Slot.Backpack, (backpack, backpack.ServerId));
        if (weapon is not null) inventory.Add(Slot.Left, (weapon, weapon.ServerId));
        if (shield is not null) inventory.Add(Slot.Right, (shield, shield.ServerId));

        return PlayerTestDataBuilder.Build(capacity: capacity, inventoryMap: inventory);
    }

    private static IContainer CreateBackpackWithItems(IItem movementItem, int fillerCount)
    {
        var items = new List<IItem>(fillerCount + 1) { movementItem };
        for (var i = 0; i < fillerCount; i++)
        {
            items.Add(ItemTestDataBuilder.CreatePotion((ushort)(1_000 + i)));
        }

        return ItemTestDataBuilder.CreateBackpack(items: items);
    }

    private static byte FindPosition(IContainer container, IItem item)
    {
        for (var position = 0; position < container.SlotsUsed; position++)
        {
            if (ReferenceEquals(container[position], item))
                return (byte)position;
        }

        throw new InvalidOperationException("Item was not found in the expected source container.");
    }
}
