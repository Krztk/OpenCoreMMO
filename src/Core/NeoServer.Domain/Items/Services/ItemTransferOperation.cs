using NeoServer.Domain.Common;
using NeoServer.Domain.Common.Contracts;
using NeoServer.Domain.Common.Contracts.Creatures;
using NeoServer.Domain.Common.Contracts.Items;
using NeoServer.Domain.Common.Contracts.Items.Types;
using NeoServer.Domain.Common.Contracts.Items.Types.Body;
using NeoServer.Domain.Common.Creatures.Structs;
using NeoServer.Domain.Common.Location;
using NeoServer.Domain.Common.Results;
using NeoServer.Domain.Creatures.Player.Inventory;
using NeoServer.Domain.Creatures.Player.Inventory.Queries;

namespace NeoServer.Domain.Items.Services;

public static class ItemTransferOperation
{
    public static Result<OperationResultList<IItem>> Execute(in ItemMovementContext movement)
    {
        if (movement.Item is null || movement.Source is null || movement.Destination is null)
            return Result<OperationResultList<IItem>>.NotPossible;

        if (!movement.Item.CanBeMoved) return Result<OperationResultList<IItem>>.NotPossible;

        var resolvedMovement = ResolveDestination(movement);
        if (TargetsSameItem(resolvedMovement)) return Result<OperationResultList<IItem>>.Success;

        var equipmentResult = PrepareEquipmentDestination(resolvedMovement);
        if (equipmentResult.Failed) return equipmentResult;

        resolvedMovement = RefreshSourcePosition(resolvedMovement);
        return Transfer(resolvedMovement);
    }

    private static Result<OperationResultList<IItem>> Transfer(ItemMovementContext movement)
    {
        var remainingAmount = movement.Item is ICumulative
            ? Math.Min(movement.Amount, movement.Item.Amount)
            : 1;
        var result = Result<OperationResultList<IItem>>.Success;

        while (remainingAmount > 0)
        {
            var iteration = movement with { Amount = (byte)remainingAmount };
            var canAdd = iteration.Destination.CanAddItem(iteration);
            if (canAdd.Failed) return new Result<OperationResultList<IItem>>(canAdd.Reason);

            var possibleAmountToAdd = iteration.Destination.PossibleAmountToAdd(iteration);
            if (possibleAmountToAdd == 0)
                return new Result<OperationResultList<IItem>>(InvalidOperation.NotEnoughRoom);

            var amountToMove = iteration.Item is ICumulative
                ? (byte)Math.Min(remainingAmount, (int)possibleAmountToAdd)
                : (byte)1;

            var targetedItem = GetTargetedItem(iteration);

            var removeResult = iteration.Source.RemoveItem(iteration.Item, amountToMove,
                iteration.SourcePosition, out var removedItem);
            if (removeResult.Failed || removedItem is null)
            {
                var reason = removeResult.Failed ? removeResult.Error : InvalidOperation.NotPossible;
                return new Result<OperationResultList<IItem>>(reason);
            }

            var addition = RefreshDestinationPosition(
                iteration with { Item = removedItem, Amount = removedItem.Amount }, targetedItem);
            var canReturnTargetedItem = CanReturnTargetedItem(targetedItem, addition);
            if (canReturnTargetedItem.Failed)
            {
                var restoreResult = RestoreToSource(removedItem, addition);
                if (restoreResult.Failed) return restoreResult;
                return canReturnTargetedItem;
            }

            result = addition.Destination.AddItem(removedItem, addition);
            if (result.Failed)
            {
                var restoreResult = RestoreToSource(removedItem, addition);
                if (restoreResult.Failed) return restoreResult;
                return result;
            }

            var displacedResult = ReturnDisplacedItems(result, addition);
            if (displacedResult.Failed) return displacedResult;

            if (removedItem is IMovableThing movableThing && addition.Destination is IThing destinationThing)
            {
                movableThing.OnMoved(destinationThing);
            }

            remainingAmount -= amountToMove;
        }

        return result;
    }

    private static ItemMovementContext ResolveDestination(ItemMovementContext movement)
    {
        if (movement.Destination is IInventory inventory &&
            movement.DestinationPosition == (byte)Slot.Backpack && inventory.BackpackSlot is { } backpack)
            return movement with { Destination = backpack, DestinationPosition = null };

        if (movement.Destination is not IContainer container) return movement;

        var destinationPosition = movement.DestinationPosition;
        if (destinationPosition == ContainerIndex.MoveUp)
        {
            var parent = container.Parent as IContainer ?? container;
            return movement with { Destination = parent, DestinationPosition = null };
        }

        if (destinationPosition is null || destinationPosition == ContainerIndex.Wherever ||
            destinationPosition >= container.Capacity)
            return movement with { DestinationPosition = null };

        if (!container.GetContainerAt(destinationPosition.Value, out var childContainer)) return movement;

        return movement with { Destination = childContainer, DestinationPosition = null };
    }

    private static bool TargetsSameItem(in ItemMovementContext movement)
    {
        if (movement.DestinationPosition is not { } destinationPosition) return false;

        if (movement.Destination is IInventory inventory)
            return ReferenceEquals(inventory[(Slot)destinationPosition], movement.Item);

        return movement.Destination is IContainer container &&
               ReferenceEquals(container[destinationPosition], movement.Item);
    }

    private static ItemMovementContext RefreshSourcePosition(ItemMovementContext movement)
    {
        if (movement.Source is not IContainer sourceContainer) return movement;

        for (var position = 0; position < sourceContainer.SlotsUsed; position++)
        {
            if (ReferenceEquals(sourceContainer[position], movement.Item))
                return movement with { SourcePosition = (byte)position };
        }

        return movement;
    }

    private static Result<OperationResultList<IItem>> CanReturnTargetedItem(IItem targetedItem,
        ItemMovementContext movement)
    {
        if (targetedItem is null || movement.Destination is not IInventory)
            return Result<OperationResultList<IItem>>.Success;

        if (CanJoin(targetedItem, movement.Item)) return Result<OperationResultList<IItem>>.Success;

        var destinationPosition = movement.Source is IInventory
            ? movement.SourcePosition
            : (byte?)null;
        var returnMovement = new ItemMovementContext(
            movement.Actor,
            targetedItem,
            movement.Destination,
            movement.Source,
            targetedItem.Amount,
            movement.DestinationPosition ?? 0,
            destinationPosition);
        var canReturn = movement.Source.CanAddItem(returnMovement);
        return canReturn.Failed
            ? new Result<OperationResultList<IItem>>(canReturn.Reason)
            : Result<OperationResultList<IItem>>.Success;
    }

    private static IItem GetTargetedItem(in ItemMovementContext movement)
    {
        if (movement.DestinationPosition is not { } destinationPosition) return null;
        if (movement.Destination is IInventory inventory) return inventory[(Slot)destinationPosition];
        return movement.Destination is IContainer container ? container[destinationPosition] : null;
    }

    private static ItemMovementContext RefreshDestinationPosition(ItemMovementContext movement, IItem targetedItem)
    {
        if (targetedItem is null || movement.Destination is not IContainer destinationContainer) return movement;

        for (var position = 0; position < destinationContainer.SlotsUsed; position++)
        {
            if (ReferenceEquals(destinationContainer[position], targetedItem))
                return movement with { DestinationPosition = (byte)position };
        }

        return movement;
    }

    private static Result<OperationResultList<IItem>> PrepareEquipmentDestination(ItemMovementContext movement)
    {
        if (movement.Destination is not IInventory inventory ||
            movement.DestinationPosition is not { } destinationPosition ||
            movement.Item is not IInventoryEquipment)
            return Result<OperationResultList<IItem>>.Success;

        var destinationSlot = (Slot)destinationPosition;
        if (!EquipmentExchangeQuery.IsValidTarget(inventory, movement.Item, destinationSlot))
            return new Result<OperationResultList<IItem>>(InvalidOperation.CannotDress);

        if (ReferenceEquals(movement.Source, inventory))
            return Result<OperationResultList<IItem>>.Success;

        Span<Slot> conflictSlots = stackalloc Slot[2];
        var conflictCount = EquipmentExchangeQuery.CollectConflicts(inventory, movement.Item, destinationSlot,
            conflictSlots);
        if (conflictCount == 0) return Result<OperationResultList<IItem>>.Success;

        if (!EquipmentExchangeQuery.IsEligibleSource(movement.Actor, movement.Source))
        {
            var reason = HasTwoHandConflict(inventory, movement.Item, destinationSlot)
                ? InvalidOperation.BothHandsNeedToBeFree
                : InvalidOperation.NotEnoughRoom;
            return new Result<OperationResultList<IItem>>(reason);
        }

        for (var i = 0; i < conflictCount; i++)
        {
            var conflictSlot = conflictSlots[i];
            var displacedItem = inventory[conflictSlot];
            if (displacedItem is null) continue;

            var displacement = new ItemMovementContext(
                movement.Actor,
                displacedItem,
                inventory,
                movement.Source,
                displacedItem.Amount,
                (byte)conflictSlot,
                null);

            var displacementResult = Execute(displacement);
            if (displacementResult.Failed) return displacementResult;
        }

        return Result<OperationResultList<IItem>>.Success;
    }

    private static bool CanJoin(IItem existingItem, IItem incomingItem)
    {
        return existingItem is ICumulative existingCumulative && incomingItem is ICumulative &&
               existingItem.ClientId == incomingItem.ClientId && existingCumulative.Amount < 100;
    }

    private static bool HasTwoHandConflict(IInventory inventory, IItem item, Slot destinationSlot)
    {
        if (item is IWeapon { TwoHanded: true })
            return inventory[Slot.Left] is not null || inventory[Slot.Right] is not null;

        return destinationSlot == Slot.Right && inventory[Slot.Left] is IWeapon { TwoHanded: true };
    }

    private static Result<OperationResultList<IItem>> ReturnDisplacedItems(
        Result<OperationResultList<IItem>> result,
        ItemMovementContext movement)
    {
        if (!(result.Value?.HasAnyOperation ?? false)) return result;

        foreach (var operation in result.Value.Operations)
        {
            if (operation.Item2 != Operation.Removed) continue;

            var destinationPosition = movement.Source is IInventory
                ? movement.SourcePosition
                : (byte?)null;
            var returnMovement = new ItemMovementContext(
                movement.Actor,
                operation.Item1,
                movement.Destination,
                movement.Source,
                operation.Item1.Amount,
                movement.DestinationPosition ?? 0,
                destinationPosition);

            var canReturn = movement.Source.CanAddItem(returnMovement);
            if (canReturn.Failed)
                return new Result<OperationResultList<IItem>>(canReturn.Reason);

            var returnResult = movement.Source.AddItem(operation.Item1, returnMovement);
            if (returnResult.Failed) return returnResult;
        }

        return result;
    }

    private static Result<OperationResultList<IItem>> RestoreToSource(IItem removedItem,
        ItemMovementContext movement)
    {
        var destinationPosition = movement.Source is IInventory
            ? movement.SourcePosition
            : (byte?)null;
        var restoreMovement = new ItemMovementContext(
            movement.Actor,
            removedItem,
            movement.Destination,
            movement.Source,
            removedItem.Amount,
            movement.DestinationPosition ?? 0,
            destinationPosition);

        var canRestore = movement.Source.CanAddItem(restoreMovement);
        if (canRestore.Failed)
            return new Result<OperationResultList<IItem>>(canRestore.Reason);

        return movement.Source.AddItem(removedItem, restoreMovement);
    }
}
