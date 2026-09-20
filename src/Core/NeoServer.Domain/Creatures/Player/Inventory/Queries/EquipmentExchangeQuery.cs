using NeoServer.Domain.Common.Contracts;
using NeoServer.Domain.Common.Contracts.Creatures;
using NeoServer.Domain.Common.Contracts.Items;
using NeoServer.Domain.Common.Contracts.Items.Types;
using NeoServer.Domain.Common.Contracts.Items.Types.Body;

namespace NeoServer.Domain.Creatures.Player.Inventory.Queries;

public static class EquipmentExchangeQuery
{
    public static bool IsEligibleSource(IPlayer actor, IHasItem source)
    {
        if (actor is null || source is not IContainer sourceContainer) return false;
        if (ReferenceEquals(sourceContainer.RootParent, actor)) return true;
        return sourceContainer.RootParent is Locker.Locker;
    }

    public static bool IsValidTarget(IInventory inventory, IItem item, Slot destinationSlot)
    {
        if (item is not IInventoryEquipment equipment) return false;
        if (item is IEquipmentRequirement requirement && !requirement.CanBeDressed(inventory.Owner)) return false;
        if (equipment is IWeapon) return destinationSlot == Slot.Left;
        return equipment.Slot == destinationSlot;
    }

    public static int CollectConflicts(IInventory inventory, IItem item, Slot destinationSlot,
        Span<Slot> conflictSlots)
    {
        if (conflictSlots.Length < 2)
        {
            throw new ArgumentException("At least two conflict slots are required.", nameof(conflictSlots));
        }

        var conflictCount = 0;
        if (item is IWeapon { TwoHanded: true })
        {
            AddConflict(inventory, item, Slot.Right, conflictSlots, ref conflictCount);
            AddConflict(inventory, item, Slot.Left, conflictSlots, ref conflictCount);
            return conflictCount;
        }

        if (destinationSlot == Slot.Right && inventory[Slot.Left] is IWeapon { TwoHanded: true })
        {
            AddConflict(inventory, item, Slot.Left, conflictSlots, ref conflictCount);
        }

        AddConflict(inventory, item, destinationSlot, conflictSlots, ref conflictCount);
        return conflictCount;
    }

    private static void AddConflict(IInventory inventory, IItem incomingItem, Slot slot, Span<Slot> conflictSlots,
        ref int conflictCount)
    {
        var equippedItem = inventory[slot];
        if (equippedItem is null || ReferenceEquals(equippedItem, incomingItem)) return;
        if (CanJoin(equippedItem, incomingItem)) return;

        for (var i = 0; i < conflictCount; i++)
        {
            if (conflictSlots[i] == slot)
                return;
        }

        conflictSlots[conflictCount++] = slot;
    }

    private static bool CanJoin(IItem existingItem, IItem incomingItem)
    {
        return existingItem is ICumulative existingCumulative && incomingItem is ICumulative &&
               existingItem.ClientId == incomingItem.ClientId && existingCumulative.Amount < 100;
    }
}
