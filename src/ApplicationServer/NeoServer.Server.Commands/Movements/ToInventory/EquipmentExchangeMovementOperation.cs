using System;
using NeoServer.Domain.Common.Contracts;
using NeoServer.Domain.Common.Contracts.Creatures;
using NeoServer.Domain.Common.Contracts.Items;
using NeoServer.Domain.Common.Creatures.Structs;
using NeoServer.Domain.Creatures.Player.Inventory;
using NeoServer.Domain.Creatures.Player.Inventory.Queries;
using NeoServer.Server.Common.Contracts.Scripts.Services;

namespace NeoServer.Server.Commands.Movements.ToInventory;

public static class EquipmentExchangeMovementOperation
{
    public static bool TryPrepare(IPlayer player, IItem incomingItem, IHasItem source, Slot destinationSlot,
        IMoveEventsScriptService moveEvents)
    {
        if (destinationSlot == Slot.Backpack && player.Inventory.BackpackSlot is not null) return true;

        if (moveEvents.EquipItem(player, incomingItem, destinationSlot, false) is false) return false;

        if (!EquipmentExchangeQuery.IsValidTarget(player.Inventory, incomingItem, destinationSlot)) return true;
        if (!EquipmentExchangeQuery.IsEligibleSource(player, source)) return true;

        Span<Slot> conflictSlots = stackalloc Slot[2];
        var conflictCount = EquipmentExchangeQuery.CollectConflicts(player.Inventory, incomingItem, destinationSlot,
            conflictSlots);

        for (var i = 0; i < conflictCount; i++)
        {
            var conflictSlot = conflictSlots[i];
            var displacedItem = player.Inventory[conflictSlot];
            if (displacedItem is null) continue;

            var displacement = new ItemMovementContext(
                player,
                displacedItem,
                player.Inventory,
                source,
                displacedItem.Amount,
                (byte)conflictSlot,
                null);

            if (source.CanAddItem(displacement).Failed) return false;
            if (source.PossibleAmountToAdd(displacement) == 0) return false;
            if (moveEvents.DeEquipItem(player, displacedItem, conflictSlot, false) is false) return false;

            var displacementResult = player.MoveItem(displacedItem, player.Inventory, source, displacedItem.Amount,
                (byte)conflictSlot, null);
            if (displacementResult.Failed) return false;
        }

        return true;
    }
}
