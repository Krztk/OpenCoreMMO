using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NeoServer.Domain.Common.Contracts.Creatures;
using NeoServer.Domain.Common.Contracts.Items;
using NeoServer.Domain.Common.Contracts.Items.Types;
using NeoServer.Domain.Common.Contracts.World.Tiles;
using NeoServer.Domain.Common.Location.Structs;
using NeoServer.Domain.Creatures.Player.Inventory;
using NeoServer.Domain.Tests.Helpers;
using NeoServer.Domain.Tests.Helpers.Player;
using NeoServer.Networking.Packets.Incoming;
using NeoServer.Server.Commands.Movements.ToContainer;
using NeoServer.Server.Commands.Movements.ToInventory;
using NeoServer.Server.Common.Contracts.Network;
using NeoServer.Server.Common.Contracts.Scripts;
using NeoServer.Server.Common.Contracts.Scripts.Services;
using Xunit;

namespace NeoServer.Server.Tests.Commands.Movements;

public class EquipmentMovementCallbackTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Container_to_inventory_runs_callbacks_in_equipment_exchange_order()
    {
        // Arrange
        var incomingWeapon = ItemTestDataBuilder.CreateWeaponItem(100, weaponType: "axe", twoHanded: true);
        var equippedWeapon = ItemTestDataBuilder.CreateWeaponItem(101);
        var equippedShield = ItemTestDataBuilder.CreateBodyEquipmentItem(102, "", "shield");
        var backpack = ItemTestDataBuilder.CreateBackpack(items: [incomingWeapon]);
        var player = CreatePlayer(backpack, equippedWeapon, equippedShield);
        var callbacks = new RecordingMoveEventsScriptService
        {
            DeEquipResult = _ => true
        };
        var packet = CreateContainerToInventoryPacket(backpack, incomingWeapon, Slot.Left);

        player.Containers.OpenContainerAt(backpack, 0);

        // Act
        ContainerToInventoryMovementOperation.Execute(player, packet, new TestScriptManager(callbacks));

        // Assert
        callbacks.Invocations.Should().Equal(
            new CallbackInvocation("equip", incomingWeapon, Slot.Left),
            new CallbackInvocation("deequip", equippedShield, Slot.Right),
            new CallbackInvocation("deequip", equippedWeapon, Slot.Left));
        player.Inventory[Slot.Left].Should().BeSameAs(incomingWeapon);
        player.Inventory[Slot.Right].Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Validation")]
    public void Container_to_inventory_preserves_completed_displacement_when_later_callback_rejects_exchange()
    {
        // Arrange
        var incomingWeapon = ItemTestDataBuilder.CreateWeaponItem(110, weaponType: "axe", twoHanded: true);
        var equippedWeapon = ItemTestDataBuilder.CreateWeaponItem(111);
        var equippedShield = ItemTestDataBuilder.CreateBodyEquipmentItem(112, "", "shield");
        var backpack = ItemTestDataBuilder.CreateBackpack(items: [incomingWeapon]);
        var player = CreatePlayer(backpack, equippedWeapon, equippedShield);
        var callbacks = new RecordingMoveEventsScriptService
        {
            DeEquipResult = item => !ReferenceEquals(item, equippedWeapon)
        };
        var packet = CreateContainerToInventoryPacket(backpack, incomingWeapon, Slot.Left);

        player.Containers.OpenContainerAt(backpack, 0);

        // Act
        ContainerToInventoryMovementOperation.Execute(player, packet, new TestScriptManager(callbacks));

        // Assert
        player.Inventory[Slot.Right].Should().BeNull();
        player.Inventory[Slot.Left].Should().BeSameAs(equippedWeapon);
        backpack.Items.Should().Contain(incomingWeapon).And.Contain(equippedShield);
        callbacks.Invocations.Should().ContainInOrder(
            new CallbackInvocation("deequip", equippedShield, Slot.Right),
            new CallbackInvocation("deequip", equippedWeapon, Slot.Left));
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public void Container_to_inventory_does_not_run_callback_for_displacement_that_cannot_fit()
    {
        // Arrange
        var incomingWeapon = ItemTestDataBuilder.CreateWeaponItem(120, weaponType: "axe", twoHanded: true);
        var equippedWeapon = ItemTestDataBuilder.CreateWeaponItem(121);
        var equippedShield = ItemTestDataBuilder.CreateBodyEquipmentItem(122, "", "shield");
        var backpack = CreateBackpackWithItems(incomingWeapon, 18);
        var player = CreatePlayer(backpack, equippedWeapon, equippedShield);
        var callbacks = new RecordingMoveEventsScriptService();
        var packet = CreateContainerToInventoryPacket(backpack, incomingWeapon, Slot.Left);

        player.Containers.OpenContainerAt(backpack, 0);

        // Act
        ContainerToInventoryMovementOperation.Execute(player, packet, new TestScriptManager(callbacks));

        // Assert
        callbacks.Invocations.Should().Equal(
            new CallbackInvocation("equip", incomingWeapon, Slot.Left),
            new CallbackInvocation("deequip", equippedShield, Slot.Right));
        player.Inventory[Slot.Right].Should().BeNull();
        player.Inventory[Slot.Left].Should().BeSameAs(equippedWeapon);
    }

    [Fact]
    [Trait("Category", "Validation")]
    public void Container_to_inventory_keeps_items_unchanged_when_equip_callback_rejects_movement()
    {
        // Arrange
        var incomingWeapon = ItemTestDataBuilder.CreateWeaponItem(130);
        var equippedWeapon = ItemTestDataBuilder.CreateWeaponItem(131, weaponType: "axe");
        var backpack = ItemTestDataBuilder.CreateBackpack(items: [incomingWeapon]);
        var player = CreatePlayer(backpack, equippedWeapon);
        var callbacks = new RecordingMoveEventsScriptService { EquipResult = false };
        var packet = CreateContainerToInventoryPacket(backpack, incomingWeapon, Slot.Left);

        player.Containers.OpenContainerAt(backpack, 0);

        // Act
        ContainerToInventoryMovementOperation.Execute(player, packet, new TestScriptManager(callbacks));

        // Assert
        player.Inventory[Slot.Left].Should().BeSameAs(equippedWeapon);
        backpack.Items.Should().Contain(incomingWeapon);
        callbacks.Invocations.Should().ContainSingle()
            .Which.Should().Be(new CallbackInvocation("equip", incomingWeapon, Slot.Left));
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    public void Container_to_backpack_keeps_equipped_hands_unchanged()
    {
        // Arrange
        var incomingWeapon = ItemTestDataBuilder.CreateWeaponItem(135, weaponType: "axe", twoHanded: true);
        var equippedWeapon = ItemTestDataBuilder.CreateWeaponItem(136);
        var equippedShield = ItemTestDataBuilder.CreateBodyEquipmentItem(137, "", "shield");
        var sourceContainer = ItemTestDataBuilder.CreateBackpack(items: [incomingWeapon]);
        var backpack = ItemTestDataBuilder.CreateBackpack(items: [sourceContainer]);
        var player = CreatePlayer(backpack, equippedWeapon, equippedShield);
        var callbacks = new RecordingMoveEventsScriptService();

        player.Containers.OpenContainerAt(backpack, 0);
        player.Containers.OpenContainerAt(sourceContainer, 1);
        var packet = CreateContainerToInventoryPacket(sourceContainer, incomingWeapon, Slot.Backpack, 1);

        // Act
        ContainerToInventoryMovementOperation.Execute(player, packet, new TestScriptManager(callbacks));

        // Assert
        player.Inventory[Slot.Left].Should().BeSameAs(equippedWeapon);
        player.Inventory[Slot.Right].Should().BeSameAs(equippedShield);
        backpack.Items.Should().Contain(incomingWeapon);
        sourceContainer.Items.Should().NotContain(incomingWeapon);
        callbacks.Invocations.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Container_to_head_displaces_only_equipped_helmet()
    {
        // Arrange
        var incomingHelmet = ItemTestDataBuilder.CreateBodyEquipmentItem(145, "head");
        var equippedHelmet = ItemTestDataBuilder.CreateBodyEquipmentItem(146, "head");
        var equippedWeapon = ItemTestDataBuilder.CreateWeaponItem(147);
        var equippedShield = ItemTestDataBuilder.CreateBodyEquipmentItem(148, "", "shield");
        var backpack = ItemTestDataBuilder.CreateBackpack(items: [incomingHelmet]);
        var player = CreatePlayer(backpack, equippedWeapon, equippedShield, equippedHelmet);
        var callbacks = new RecordingMoveEventsScriptService();
        var packet = CreateContainerToInventoryPacket(backpack, incomingHelmet, Slot.Head);

        player.Containers.OpenContainerAt(backpack, 0);

        // Act
        ContainerToInventoryMovementOperation.Execute(player, packet, new TestScriptManager(callbacks));

        // Assert
        player.Inventory[Slot.Head].Should().BeSameAs(incomingHelmet);
        player.Inventory[Slot.Left].Should().BeSameAs(equippedWeapon);
        player.Inventory[Slot.Right].Should().BeSameAs(equippedShield);
        backpack.Items.Should().Contain(equippedHelmet).And.NotContain(incomingHelmet);
        callbacks.Invocations.Should().Equal(
            new CallbackInvocation("equip", incomingHelmet, Slot.Head),
            new CallbackInvocation("deequip", equippedHelmet, Slot.Head));
    }

    [Fact]
    [Trait("Category", "HappyPath")]
    public void Inventory_to_container_continues_when_deequip_callback_returns_true()
    {
        // Arrange
        var equippedWeapon = ItemTestDataBuilder.CreateWeaponItem(140);
        var backpack = ItemTestDataBuilder.CreateBackpack();
        var player = CreatePlayer(backpack, equippedWeapon);
        var callbacks = new RecordingMoveEventsScriptService
        {
            DeEquipResult = _ => true
        };
        var packet = CreateInventoryToContainerPacket(Slot.Left);

        player.Containers.OpenContainerAt(backpack, 0);

        // Act
        InventoryToContainerMovementOperation.Execute(player, packet, new TestScriptManager(callbacks));

        // Assert
        player.Inventory[Slot.Left].Should().BeNull();
        backpack.Items.Should().Contain(equippedWeapon);
        callbacks.Invocations.Should().ContainSingle()
            .Which.Should().Be(new CallbackInvocation("deequip", equippedWeapon, Slot.Left));
    }

    private static IPlayer CreatePlayer(IContainer backpack, IItem weapon = null, IItem shield = null,
        IItem helmet = null)
    {
        var inventory = new Dictionary<Slot, (IItem Item, ushort Id)>
        {
            [Slot.Backpack] = (backpack, backpack.ServerId)
        };

        if (weapon is not null)
        {
            inventory.Add(Slot.Left, (weapon, weapon.ServerId));
        }

        if (shield is not null)
        {
            inventory.Add(Slot.Right, (shield, shield.ServerId));
        }

        if (helmet is not null)
        {
            inventory.Add(Slot.Head, (helmet, helmet.ServerId));
        }

        return PlayerTestDataBuilder.Build(capacity: 1_000, inventoryMap: inventory);
    }

    private static IContainer CreateBackpackWithItems(IItem incomingItem, int fillerCount)
    {
        var items = new List<IItem>(fillerCount + 1) { incomingItem };
        for (var i = 0; i < fillerCount; i++)
        {
            items.Add(ItemTestDataBuilder.CreatePotion((ushort)(1_000 + i)));
        }

        return ItemTestDataBuilder.CreateBackpack(items: items);
    }

    private static ItemThrowPacket CreateContainerToInventoryPacket(IContainer source, IItem item,
        Slot destinationSlot, byte sourceContainerId = 0)
    {
        return new ItemThrowPacket(Mock.Of<IReadOnlyNetworkMessage>())
        {
            FromLocation = Location.Container(sourceContainerId, FindPosition(source, item)),
            ToLocation = Location.Inventory(destinationSlot),
            Count = item.Amount
        };
    }

    private static ItemThrowPacket CreateInventoryToContainerPacket(Slot sourceSlot)
    {
        return new ItemThrowPacket(Mock.Of<IReadOnlyNetworkMessage>())
        {
            FromLocation = Location.Inventory(sourceSlot),
            ToLocation = Location.Container(0, 0),
            Count = 1
        };
    }

    private static byte FindPosition(IContainer container, IItem item)
    {
        for (var position = 0; position < container.SlotsUsed; position++)
        {
            if (ReferenceEquals(container[position], item)) return (byte)position;
        }

        throw new InvalidOperationException("Item was not found in the expected source container.");
    }

    private readonly record struct CallbackInvocation(string Name, IItem Item, Slot Slot);

    private sealed class RecordingMoveEventsScriptService : IMoveEventsScriptService
    {
        public List<CallbackInvocation> Invocations { get; } = [];
        public bool? EquipResult { get; init; }
        public Func<IItem, bool?> DeEquipResult { get; init; } = _ => null;

        public void ItemMove(IItem item, ITile tile, bool isAdd)
        {
        }

        public bool? EquipItem(IPlayer player, IItem item, Slot slot, bool isChecks)
        {
            Invocations.Add(new CallbackInvocation("equip", item, slot));
            return EquipResult;
        }

        public bool? DeEquipItem(IPlayer player, IItem item, Slot slot, bool isChecks)
        {
            Invocations.Add(new CallbackInvocation("deequip", item, slot));
            return DeEquipResult(item);
        }
    }

    private sealed class TestScriptManager(IMoveEventsScriptService moveEvents) : IScriptManager
    {
        public IActionScriptService Actions => null;
        public ICreatureEventsScriptService CreatureEvents => null;
        public IGlobalEventsScriptService GlobalEvents => null;
        public IMoveEventsScriptService MoveEvents { get; } = moveEvents;
        public ITalkActionScriptService TalkActions => null;

        public void Initialize()
        {
        }
    }
}
