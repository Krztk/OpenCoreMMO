using NeoServer.Domain.Common;
using NeoServer.Domain.Common.Contracts;
using NeoServer.Domain.Common.Contracts.Creatures;
using NeoServer.Domain.Common.Contracts.Items;
using NeoServer.Domain.Common.Contracts.Items.Types;
using NeoServer.Domain.Common.Contracts.Services;
using NeoServer.Domain.Common.Contracts.World;
using NeoServer.Domain.Common.Contracts.World.Tiles;
using NeoServer.Domain.Common.Creatures.Structs;
using NeoServer.Domain.Common.Location;
using NeoServer.Domain.Common.Location.Structs;
using NeoServer.Domain.Common.Results;
using NeoServer.Domain.Common.Services;
using NeoServer.Domain.Common.Texts;
using NeoServer.Domain.Items.Services;
using NeoServer.Domain.Mail;

namespace NeoServer.Domain.Creatures.Services;

public class ItemMovementService(IWalkToMechanism walkToMechanism, IMailService mailService) : IItemMovementService
{
    public Result<OperationResultList<IItem>> Move(IPlayer player, IItem item, IHasItem from, IHasItem destination,
        byte amount,
        byte fromPosition, byte? toPosition, bool walkTo = true)
    {
        if (player is null) return Result<OperationResultList<IItem>>.NotPossible;

        if (item.Location.Type == LocationType.Ground)
        {
            if (item.Location.Z < player.Location.Z)
            {
                OperationFailService.Send(player.CreatureId, TextConstants.FIRST_GO_UPSTAIRS);
                return Result<OperationResultList<IItem>>.NotPossible;
            }

            if (item.Location.Z > player.Location.Z)
            {
                OperationFailService.Send(player.CreatureId, TextConstants.FIRST_GO_DOWNSTAIRS);
                return Result<OperationResultList<IItem>>.NotPossible;
            }
        }

        // if (destination is ITile tile)
        // {
        //     if (map.GetFinalDestination(tile.Location) is IDynamicTile dynamicTile)
        //     {
        //         destination = dynamicTile;
        //     }
        // }

        if (!item.IsCloseTo(player) && walkTo)
        {
            walkToMechanism.WalkTo(player,
                () => player.MoveItem(item, from, destination, amount, fromPosition, toPosition), item.Location);
            return Result<OperationResultList<IItem>>.Success;
        }

        if (destination is IDynamicTile finalTile && finalTile.HasFlag(TileFlags.MailBox) && !item.IsMailable)
        {
            OperationFailService.Send(player, InvalidOperation.NotPossible);
            return Result<OperationResultList<IItem>>.NotPossible;
        }

        return Move(player, item, from, destination, amount, fromPosition, toPosition);
    }


    public Result<OperationResultList<IItem>> Move(IItem item, IHasItem from, IHasItem destination, byte amount,
        byte fromPosition, byte? toPosition)
    {
        var movement = new ItemMovementContext(null, item, from, destination, amount, fromPosition, toPosition);
        return ItemTransferOperation.Execute(movement);
    }

    private Result<OperationResultList<IItem>> Move(IPlayer player, IItem item, IHasItem from, IHasItem destination,
        byte amount,
        byte fromPosition, byte? toPosition)
    {
        if (!item.CanBeMoved) return Result<OperationResultList<IItem>>.NotPossible;

        if (!item.IsCloseTo(player)) return new Result<OperationResultList<IItem>>(InvalidOperation.TooFar);

        if (destination is not IDynamicTile { } finalTile || !finalTile.HasFlag(TileFlags.MailBox))
        {
            var coreMovement = new ItemMovementContext(player, item, from, destination, amount, fromPosition,
                toPosition);
            return ItemTransferOperation.Execute(coreMovement);
        }

        var movement = new ItemMovementContext(player, item, from, destination, amount, fromPosition, toPosition);
        var canAdd = destination.CanAddItem(movement);
        if (canAdd.Failed) return new Result<OperationResultList<IItem>>(canAdd.Reason);

        var possibleAmountToAdd = destination.PossibleAmountToAdd(movement);
        if (possibleAmountToAdd == 0) return new Result<OperationResultList<IItem>>(InvalidOperation.NotEnoughRoom);

        var amountToRemove = item is ICumulative
            ? (byte)Math.Min(amount, possibleAmountToAdd)
            : (byte)1;
        var removeResult = from.RemoveItem(item, amountToRemove, fromPosition, out var removedItem);
        if (removeResult.Failed || removedItem is null)
        {
            var reason = removeResult.Failed ? removeResult.Error : InvalidOperation.NotPossible;
            return new Result<OperationResultList<IItem>>(reason);
        }

        var sendMailResult = mailService.Send(player, removedItem);
        if (sendMailResult.Succeeded)
        {
            removedItem.SetNewLocation(Location.Zero);
            if (removedItem is IMovableThing movableThing && destination is IThing destinationThing)
            {
                movableThing.OnMoved(destinationThing);
            }

            var remainingAmount = (byte)(amount - amountToRemove);
            return remainingAmount > 0
                ? Move(player, item, from, destination, remainingAmount, fromPosition, toPosition)
                : Result<OperationResultList<IItem>>.Success;
        }

        var addition = movement with { Item = removedItem, Amount = removedItem.Amount };
        var addResult = destination.AddItem(removedItem, addition);
        if (addResult.Failed)
        {
            var restoreResult = RestoreRemovedItem(player, removedItem, from, destination, fromPosition, toPosition);
            if (restoreResult.Failed) return restoreResult;
            return addResult;
        }

        if (removedItem is IMovableThing fallbackMovableItem && destination is IThing fallbackDestination)
        {
            fallbackMovableItem.OnMoved(fallbackDestination);
        }

        var fallbackRemainingAmount = (byte)(amount - amountToRemove);
        return fallbackRemainingAmount > 0
            ? Move(player, item, from, destination, fallbackRemainingAmount, fromPosition, toPosition)
            : addResult;
    }

    private static Result<OperationResultList<IItem>> RestoreRemovedItem(IPlayer player, IItem item,
        IHasItem source, IHasItem failedDestination, byte sourcePosition, byte? failedDestinationPosition)
    {
        var destinationPosition = source is IInventory ? sourcePosition : (byte?)null;
        var restoreMovement = new ItemMovementContext(
            player,
            item,
            failedDestination,
            source,
            item.Amount,
            failedDestinationPosition ?? 0,
            destinationPosition);
        var canRestore = source.CanAddItem(restoreMovement);
        if (canRestore.Failed)
            return new Result<OperationResultList<IItem>>(canRestore.Reason);

        return source.AddItem(item, restoreMovement);
    }
}
